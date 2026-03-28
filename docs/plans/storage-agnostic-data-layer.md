# Plan: Storage-Agnostic Data Layer

**Date:** 2026-03-27
**Status:** Approved — ready for implementation
**Goal:** Replace EF Core / SQLite with JSON file storage + Azure Blob Storage, behind clean abstractions (factory pattern) so the storage backend is a configuration choice, not an architecture choice.

---

## Context

The current marketplace data layer has `MarketplaceDbContext` injected directly into every controller. ASP.NET Identity and OpenIddict are both wired to EF Core stores. This couples business logic to a specific storage provider.

We're replacing it with:
- **JSON files** for all persistent data (publishers, keys, listings, auth)
- **Azure Blob Storage** for agent packages (manifest JSON)
- **Factory pattern** — storage backend selected by configuration, swappable without touching business logic

---

## Project Structure

```
marketplace/src/
  Purfle.Marketplace.Core/              # NEW — POCOs, repository interfaces, storage abstractions
  Purfle.Marketplace.Storage.Json/      # NEW — JSON file-backed implementations
  Purfle.Marketplace.Shared/            # UNCHANGED — DTOs
  Purfle.Marketplace.Api/               # MODIFIED — depends on interfaces, not DbContext
  Purfle.Marketplace.Data/              # DELETED after migration
```

Dependency graph:
```
Api  -->  Core  <--  Storage.Json
Api  -->  Shared
Api  -->  Purfle.Runtime (for IKeyRegistry)
Storage.Json  -->  Core
```

`Core` has zero infrastructure dependencies. `Storage.Json` references `System.Text.Json` and Identity interfaces. Neither references EF Core.

---

## Entity POCOs (Core/Entities/)

Publisher drops `IdentityUser` inheritance, becomes a clean POCO with Identity-required properties as plain fields:

| Entity | Key Changes |
|--------|-------------|
| **Publisher** | Id, Email, NormalizedEmail, UserName, NormalizedUserName, PasswordHash, DisplayName, CreatedAt, IsVerified, SecurityStamp, ConcurrencyStamp — all plain properties, no IdentityUser base |
| **SigningKey** | Unchanged fields, navigation properties removed |
| **AgentListing** | Unchanged fields, navigation properties removed |
| **AgentVersion** | `ManifestJson` → `ManifestBlobRef` (string pointing to blob storage) |

No navigation properties on any entity. Repositories handle joins explicitly.

---

## Repository Interfaces (Core/Repositories/)

Derived from every data access pattern in the current controllers:

**IPublisherRepository**
- `FindByIdAsync`, `FindByEmailAsync`, `FindByNameAsync`
- `CreateAsync`, `UpdateAsync`, `DeleteAsync`

**ISigningKeyRepository**
- `FindByKeyIdAsync`, `ExistsByKeyIdAsync`, `FindByPublisherIdAsync`
- `CreateAsync`, `UpdateAsync`

**IAgentListingRepository**
- `FindByAgentIdAsync`
- `SearchAsync(term, page, pageSize)` → returns `AgentSearchPage` (items + total count, with publisher name and latest version pre-joined)
- `CreateAsync`, `UpdateAsync`

**IAgentVersionRepository**
- `FindByAgentIdAndVersionAsync`, `FindLatestByAgentIdAsync`, `FindByListingIdAsync`
- `ExistsAsync(listingId, version)`
- `CreateAsync`, `IncrementDownloadsAsync`

**IManifestBlobStore** (Core/Storage/)
- `StoreAsync(agentId, version, manifestJson)` → returns blob reference key
- `RetrieveAsync(blobRef)` → returns manifest JSON
- `DeleteAsync(blobRef)`

---

## JSON File Storage (Storage.Json/)

### File structure on disk

```
data/
  publishers.json
  signing-keys.json
  agent-listings.json
  agent-versions.json
  openiddict/
    applications.json
    authorizations.json
    scopes.json
    tokens.json
```

One file per collection. Entire collection is memory-resident — loaded on startup, written through on every mutation. JSON is the native format for the entire platform.

### Thread-safety: JsonDocumentStore<T>

Internal to `Storage.Json` (not exposed to Core):

- `SemaphoreSlim(1,1)` for async-safe locking (not ReaderWriterLockSlim — it doesn't support await)
- In-memory `List<T>` cache, deserialized from JSON file on startup
- **Write-through**: every mutation serializes and saves immediately
- **Atomic writes**: write to temp file, then `File.Move` with overwrite to prevent corruption on crash
- Data volumes are small (hundreds to low thousands of entities) — in-memory LINQ is fast

```
Storage.Json/
  Infrastructure/
    JsonDocumentStore.cs          # Generic in-memory cached store with SemaphoreSlim
    JsonSerializerOptions.cs      # Shared System.Text.Json options
  Repositories/
    JsonPublisherRepository.cs
    JsonSigningKeyRepository.cs
    JsonAgentListingRepository.cs
    JsonAgentVersionRepository.cs
  Identity/
    JsonUserStore.cs              # IUserStore, IUserPasswordStore, IUserEmailStore, IUserSecurityStampStore
    JsonRoleStore.cs              # IRoleStore (minimal — roles not used yet but Identity requires it)
  OpenIddict/
    JsonApplicationStore.cs       # IOpenIddictApplicationStore
    JsonAuthorizationStore.cs     # IOpenIddictAuthorizationStore
    JsonScopeStore.cs             # IOpenIddictScopeStore
    JsonTokenStore.cs             # IOpenIddictTokenStore
    OpenIddictModels.cs           # POCO models for OpenIddict entities
  BlobStorage/
    LocalFileBlobStore.cs         # Dev: stores in data/blobs/{agentId}/{version}.json
    AzureBlobStore.cs             # Prod: Azure.Storage.Blobs SDK
  ServiceCollectionExtensions.cs  # DI wiring: AddJsonStorage(config)
```

### Azure Blob Storage

Two implementations of `IManifestBlobStore`:
- **LocalFileBlobStore** — dev/test, stores in `data/blobs/`
- **AzureBlobStore** — production, uses `Azure.Storage.Blobs`, container `purfle-manifests`, blob path `manifests/{agentId}/{version}.json`

Selected by configuration:
```json
{
  "Storage": {
    "Backend": "Json",
    "DataDirectory": "./data",
    "ManifestStore": "Local"
  },
  "AzureBlobStorage": {
    "ConnectionString": "...",
    "ContainerName": "purfle-manifests"
  }
}
```

---

## Identity Stores (JSON-backed)

`JsonUserStore` implements:
- `IUserStore<Publisher>` — CRUD + find by id/name
- `IUserPasswordStore<Publisher>` — get/set password hash
- `IUserEmailStore<Publisher>` — find by email, get/set email
- `IUserSecurityStampStore<Publisher>` — required for token generation

`JsonRoleStore` implements `IRoleStore<IdentityRole>` minimally (Identity requires it even if roles aren't used).

DI changes from `.AddEntityFrameworkStores<MarketplaceDbContext>()` to:
```csharp
.AddUserStore<JsonUserStore>()
.AddRoleStore<JsonRoleStore>()
```

---

## OpenIddict Stores (JSON-backed)

Four stores, ~25 methods each. Reference: OpenIddict MongoDB stores solve the same problem.

DI changes from:
```csharp
options.UseEntityFrameworkCore().UseDbContext<MarketplaceDbContext>();
```
to custom model + store registration:
```csharp
options.AddApplication<OpenIddictJsonApplication>()
       .AddAuthorization<OpenIddictJsonAuthorization>()
       .AddScope<OpenIddictJsonScope>()
       .AddToken<OpenIddictJsonToken>();
// + register each store as singleton
```

---

## Factory / DI Pattern

Configuration-driven extension method (idiomatic .NET):

```csharp
// Program.cs
var backend = builder.Configuration["Storage:Backend"];
switch (backend)
{
    case "Json":
        builder.Services.AddJsonStorage(builder.Configuration);
        break;
    // Future: "Cosmos", "Postgres", etc.
}
```

Future backends: create `Purfle.Marketplace.Storage.Postgres` with its own `AddPostgresStorage()`. Zero changes to Core or Api.

---

## Migration Steps (each step is a buildable, runnable commit)

### Step 1: Create Core project + repository interfaces
- New `Purfle.Marketplace.Core` project
- Move entity POCOs from `Data/Entities/` → `Core/Entities/`
- Strip `IdentityUser` from Publisher, add Identity properties as plain fields
- Remove navigation properties
- Define all repository interfaces + `IManifestBlobStore`
- Both `Data` and `Api` reference `Core`

### Step 2: Refactor controllers to use interfaces (still backed by EF Core)
- Create thin EF-Core-backed repository implementations in `Data` project (wrapping DbContext)
- Register in DI
- Refactor all controllers to inject interfaces instead of `MarketplaceDbContext`
- **Verify everything still works** — this is the safety step

### Step 3: Create Storage.Json project + implement repositories
- `JsonDocumentStore<T>` infrastructure
- All four JSON repository classes
- `LocalFileBlobStore`
- Unit tests for serialization round-trips

### Step 4: Implement Identity + OpenIddict JSON stores
- `JsonUserStore`, `JsonRoleStore`
- All four OpenIddict stores + POCO models
- This is the most tedious step but mostly mechanical

### Step 5: Wire up DI and switch
- `AddJsonStorage()` extension method
- Update Program.cs to configuration-driven backend
- Switch Identity to `AddUserStore<JsonUserStore>()`
- Switch OpenIddict to custom stores
- Remove EF Core startup code
- **Full integration test: register → login → publish → search → download**

### Step 6: Azure Blob Storage
- Add `Azure.Storage.Blobs` package
- Implement `AzureBlobStore`
- Configuration for connection string

### Step 7: Delete Data project + EF Core packages
- Remove `Purfle.Marketplace.Data`
- Remove all EF Core NuGet references
- Clean up

---

## Known Challenges

| Challenge | Mitigation |
|-----------|------------|
| OpenIddict stores (~100 methods total) | Study OpenIddict MongoDB stores as reference |
| File corruption on crash | Atomic writes: temp file + `File.Move` with overwrite |
| Download counter contention | SemaphoreSlim is fine at marketplace scale; batch if needed later |
| Search performance | In-memory LINQ over cached collections; add indexes if >10K listings |

---

## Verification

After each step, verify:
1. `dotnet build` — solution compiles
2. `dotnet test` — runtime tests still pass (52 tests)
3. After Step 5: manual end-to-end flow
   - `purfle init "Test Agent"` → `purfle sign --generate-key` → start marketplace API → `purfle login` → `purfle publish` → `purfle search "Test"` → `purfle install <id>`

---

## Critical Files

| File | Role |
|------|------|
| `marketplace/src/Purfle.Marketplace.Api/Program.cs` | DI composition root — must be rewired |
| `marketplace/src/Purfle.Marketplace.Api/Controllers/AgentsController.cs` | Most complex controller — search/pagination/joins |
| `marketplace/src/Purfle.Marketplace.Data/MarketplaceDbContext.cs` | Current EF Core context — relationships replicated as repo logic |
| `marketplace/src/Purfle.Marketplace.Data/Entities/Publisher.cs` | IdentityUser subclass → clean POCO |
| `runtime/src/Purfle.Runtime/Identity/IKeyRegistry.cs` | Architectural template for the abstraction pattern |
