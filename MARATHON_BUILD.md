# Purfle — Marathon Build Session

**This document is designed to run for 8-12+ hours with strategic `/compact` commands.**

**Instructions for Claude Code:**
1. Read this entire document first
2. Execute each phase in order
3. Run `/compact` when instructed (marked with 🔄)
4. Update `CLAUDE.md` status section after each phase
5. Commit after each phase completes
6. Continue to next phase automatically
7. If you hit an error, fix it and continue — don't stop
8. Push to main after final phase

## SESSION NOTES (updated by Claude Code — do not delete)

- **Python: SKIP.** Python is not installed on the primary dev machine. All "× 3 languages" agents should be built as **C# + TypeScript only** (2 implementations each). A future session can add Python once it's available.
- **Last verified stop state:** All phases A–I COMPLETE and pushed to main.
- **Current phase:** ALL PHASES COMPLETE
- **Last commit pushed:** 480f243 (2026-04-01)
- **Git log of marathon commits (newest first):**
  - `480f243` ci+docs+tests: Phase H+I — GitHub Actions, docs updates, new tests
  - `4cffed6` feat: Phase E+F+G — IdentityHub, Dashboard, SDK CLI commands
  - `2a143ef` feat(tools): all 10 new MCP servers (ports 8101-8110)
  - `b092d7b` feat(agents): 5 more polyglot agents (C# + TypeScript)
  - `6cdddd8` feat(agents): polyglot implementations of 5 core agents (C# + TypeScript)
  - `14274b2` feat(runtime): Phase A — IPC protocol, inference adapters, credential stores

### Phase completion checklist
- [x] Phase A: IPC protocol, OpenAI/Ollama adapters, credential stores (Win/Mac/Linux)
- [x] Phase B: 5 polyglot agents — file-assistant, purfle-pet, email-priority, news-digest, api-guardian
- [x] Phase C: 5 more polyglot agents — code-reviewer, meeting-assistant, db-assistant, research-assistant, cli-generator
- [x] Phase D: 11 MCP servers (ports 8100–8110), all TypeScript + Express
- [x] Phase E: IdentityHub — agent registry, key revocation, trust attestations, JSON storage
- [x] Phase F: Dashboard — ASP.NET Core API + static HTML/JS, SignalR real-time, dark theme
- [x] Phase G: SDK CLI — validate, run, security-scan commands added
- [x] Phase H: CI/CD — GitHub Actions (ci.yml, release.yml, dependabot.yml), docs updated
- [x] Phase I: Tests — 16 new tests (IPC protocol, credential store, factory), all passing
- [ ] Final: CLAUDE.md status update, v0.1.0 tag (remaining)

### What to do if resuming from another machine
1. `git pull origin main` — all work is pushed
2. `dotnet build runtime/Purfle.Runtime.slnx` — verify runtime builds
3. Check this SESSION NOTES section for current state
4. Remaining work: update CLAUDE.md status section, tag v0.1.0

---

## CORE PHILOSOPHY

| Principle | Implementation |
|-----------|----------------|
| **Spec is universal** | JSON manifest — any runtime reads it |
| **Agents are polyglot** | C#, TypeScript, Python examples for every agent |
| **Cross-platform from day 1** | .NET MAUI, Node.js, Python — all first-class |
| **Inference is pluggable** | Gemini, Anthropic, OpenAI, Ollama — all supported |
| **Loose coupling** | Process-based isolation with simple IPC |

---

## AGENT IPC PROTOCOL

Agents are **standalone processes**. The runtime communicates via stdin/stdout JSON.

### Request (Runtime → Agent)

```json
{
  "type": "execute",
  "id": "req-001",
  "input": "List files in the workspace",
  "context": {
    "conversationId": "conv-123",
    "previousMessages": []
  }
}
```

### Response (Agent → Runtime)

```json
{
  "type": "response",
  "id": "req-001",
  "output": "Here are the files...",
  "toolCalls": [
    {
      "id": "tc-001",
      "tool": "mcp://localhost:8100/files/list",
      "arguments": { "path": "./workspace" }
    }
  ],
  "done": false
}
```

### Tool Result (Runtime → Agent)

```json
{
  "type": "toolResult",
  "id": "tc-001",
  "result": { "files": ["readme.txt", "notes.md"] }
}
```

### Final Response

```json
{
  "type": "response",
  "id": "req-001",
  "output": "Found 2 files: readme.txt and notes.md",
  "toolCalls": [],
  "done": true
}
```

**Any process that speaks this protocol is a valid Purfle agent.**

---

## INFERENCE ADAPTERS

All four adapters are fully implemented:

| Provider | Model Examples | API Endpoint |
|----------|---------------|--------------|
| **Gemini** | gemini-1.5-flash, gemini-1.5-pro | generativelanguage.googleapis.com |
| **Anthropic** | claude-sonnet-4-20250514, claude-3-haiku | api.anthropic.com |
| **OpenAI** | gpt-4o, gpt-4o-mini | api.openai.com |
| **Ollama** | llama3, mistral, codellama | localhost:11434 |

Credentials stored in platform credential store:
- Windows: Credential Manager
- macOS: Keychain
- Linux: Secret Service

---

## MCP SERVER PORT ASSIGNMENTS

| Port | MCP Server | Used By Agent | Purpose |
|------|------------|---------------|---------|
| 8100 | mcp-file-server | file-assistant | Local file operations |
| 8101 | mcp-microsoft-email | email-priority | Microsoft Graph API |
| 8102 | mcp-gmail | email-priority | Gmail API |
| 8103 | mcp-news | news-digest | NewsAPI.org |
| 8104 | mcp-pet | purfle-pet | Pet state management |
| 8105 | mcp-api-tools | api-guardian | API health/docs |
| 8106 | mcp-code-tools | code-reviewer | Code analysis |
| 8107 | mcp-meeting | meeting-assistant | Transcript processing |
| 8108 | mcp-db-tools | db-assistant | SQL parsing |
| 8109 | mcp-research | research-assistant | Web search |
| 8110 | mcp-cli-gen | cli-generator | CLI scaffolding |

---

## PRE-FLIGHT

```bash
# Verify toolchains
dotnet --version    # .NET 10.x
node --version      # 20.x+
python --version    # 3.11+

# Build runtime
dotnet build runtime/Purfle.Runtime.sln
dotnet build identityhub/src/Purfle.IdentityHub.Api/

# Build SDK
cd sdk && npm install && npm run build && cd ..
```

Read `CLAUDE.md` and `AGENT_MODEL.md`.

---

# PHASE A: Runtime Foundation

## A1: Cross-Platform Runtime with MAUI

Create `runtime/src/Purfle.Runtime.Maui/`:

```
runtime/src/Purfle.Runtime.Maui/
├── Purfle.Runtime.Maui.csproj
├── Platforms/
│   ├── Windows/
│   ├── MacCatalyst/
│   ├── iOS/
│   └── Android/
├── Services/
│   ├── AgentHostService.cs
│   ├── ProcessAgentRunner.cs
│   └── IpcProtocol.cs
├── Views/
│   ├── MainPage.xaml
│   └── AgentConsole.xaml
└── MauiProgram.cs
```

### Purfle.Runtime.Maui.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Maui">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Purfle.Runtime/Purfle.Runtime.csproj" />
  </ItemGroup>
</Project>
```

### ProcessAgentRunner.cs

```csharp
namespace Purfle.Runtime.Maui.Services;

public class ProcessAgentRunner : IAgentRunner
{
    public async Task<AgentResponse> ExecuteAsync(
        AgentManifest manifest,
        string input,
        CancellationToken ct)
    {
        var entrypoint = manifest.Runtime.Entrypoint;
        var workingDir = Path.GetDirectoryName(manifest.Path);
        
        // Determine how to run based on entrypoint extension
        var (executable, args) = entrypoint switch
        {
            var e when e.EndsWith(".dll") => ("dotnet", e),
            var e when e.EndsWith(".js") => ("node", e),
            var e when e.EndsWith(".py") => ("python", e),
            var e when e.EndsWith(".exe") => (e, ""),
            _ => throw new NotSupportedException($"Unknown entrypoint: {entrypoint}")
        };
        
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        
        // Send request
        var request = new IpcRequest
        {
            Type = "execute",
            Id = Guid.NewGuid().ToString(),
            Input = input
        };
        
        await process.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(request));
        await process.StandardInput.FlushAsync();
        
        // Read responses until done
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(ct);
            var response = JsonSerializer.Deserialize<IpcResponse>(line);
            
            if (response.ToolCalls?.Any() == true)
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    // Check permission gate
                    if (!PermissionGate.Check(manifest, toolCall.Tool))
                        throw new PermissionDeniedException(toolCall.Tool);
                    
                    // Execute via MCP
                    var result = await McpDispatcher.CallAsync(toolCall, ct);
                    
                    // Send result back
                    var toolResult = new IpcToolResult
                    {
                        Type = "toolResult",
                        Id = toolCall.Id,
                        Result = result
                    };
                    await process.StandardInput.WriteLineAsync(
                        JsonSerializer.Serialize(toolResult));
                }
            }
            
            if (response.Done)
                return new AgentResponse(response.Output);
        }
    }
}
```

## A2: Inference Adapters (All 4)

Create `runtime/src/Purfle.Runtime/Inference/`:

```
Inference/
├── IInferenceAdapter.cs
├── InferenceAdapterFactory.cs
├── GeminiAdapter.cs
├── AnthropicAdapter.cs
├── OpenAIAdapter.cs
└── OllamaAdapter.cs
```

### IInferenceAdapter.cs

```csharp
namespace Purfle.Runtime.Inference;

public interface IInferenceAdapter
{
    string Provider { get; }
    Task<InferenceResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct);
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct);
}
```

### GeminiAdapter.cs

```csharp
namespace Purfle.Runtime.Inference;

public class GeminiAdapter : IInferenceAdapter
{
    public string Provider => "gemini";
    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;
    
    public GeminiAdapter(ICredentialStore credentials)
    {
        _credentials = credentials;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/")
        };
    }
    
    public async Task<InferenceResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct)
    {
        var apiKey = await _credentials.GetAsync("purfle:gemini:key");
        var model = options.Model ?? "gemini-1.5-flash";
        
        var request = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            },
            generationConfig = new
            {
                maxOutputTokens = options.MaxTokens ?? 4096
            }
        };
        
        var response = await _http.PostAsJsonAsync(
            $"models/{model}:generateContent?key={apiKey}",
            request, ct);
        
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
        return new InferenceResponse(
            result.Candidates[0].Content.Parts[0].Text,
            result.UsageMetadata.TotalTokenCount);
    }
}
```

### AnthropicAdapter.cs

```csharp
namespace Purfle.Runtime.Inference;

public class AnthropicAdapter : IInferenceAdapter
{
    public string Provider => "anthropic";
    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;
    
    public AnthropicAdapter(ICredentialStore credentials)
    {
        _credentials = credentials;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/v1/")
        };
    }
    
    public async Task<InferenceResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct)
    {
        var apiKey = await _credentials.GetAsync("purfle:anthropic:key");
        var model = options.Model ?? "claude-sonnet-4-20250514";
        
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        
        var request = new
        {
            model = model,
            max_tokens = options.MaxTokens ?? 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };
        
        var response = await _http.PostAsJsonAsync("messages", request, ct);
        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(ct);
        
        return new InferenceResponse(
            result.Content[0].Text,
            result.Usage.InputTokens + result.Usage.OutputTokens);
    }
}
```

### OpenAIAdapter.cs

```csharp
namespace Purfle.Runtime.Inference;

public class OpenAIAdapter : IInferenceAdapter
{
    public string Provider => "openai";
    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;
    
    public OpenAIAdapter(ICredentialStore credentials)
    {
        _credentials = credentials;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
    }
    
    public async Task<InferenceResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct)
    {
        var apiKey = await _credentials.GetAsync("purfle:openai:key");
        var model = options.Model ?? "gpt-4o";
        
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        var request = new
        {
            model = model,
            max_tokens = options.MaxTokens ?? 4096,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        
        var response = await _http.PostAsJsonAsync("chat/completions", request, ct);
        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(ct);
        
        return new InferenceResponse(
            result.Choices[0].Message.Content,
            result.Usage.TotalTokens);
    }
}
```

### OllamaAdapter.cs

```csharp
namespace Purfle.Runtime.Inference;

public class OllamaAdapter : IInferenceAdapter
{
    public string Provider => "ollama";
    private readonly HttpClient _http;
    
    public OllamaAdapter()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };
    }
    
    public async Task<InferenceResponse> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        InferenceOptions options,
        CancellationToken ct)
    {
        var model = options.Model ?? "llama3";
        
        var request = new
        {
            model = model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false
        };
        
        var response = await _http.PostAsJsonAsync("api/chat", request, ct);
        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct);
        
        return new InferenceResponse(
            result.Message.Content,
            result.EvalCount ?? 0);
    }
}
```

### InferenceAdapterFactory.cs

```csharp
namespace Purfle.Runtime.Inference;

public class InferenceAdapterFactory
{
    private readonly Dictionary<string, IInferenceAdapter> _adapters;
    
    public InferenceAdapterFactory(ICredentialStore credentials)
    {
        _adapters = new()
        {
            ["gemini"] = new GeminiAdapter(credentials),
            ["anthropic"] = new AnthropicAdapter(credentials),
            ["openai"] = new OpenAIAdapter(credentials),
            ["ollama"] = new OllamaAdapter()
        };
    }
    
    public IInferenceAdapter GetAdapter(InferenceConfig config)
    {
        // Try primary provider
        if (_adapters.TryGetValue(config.Provider, out var adapter))
        {
            if (IsAvailable(adapter))
                return adapter;
        }
        
        // Try fallbacks
        foreach (var fallback in config.Fallback ?? [])
        {
            var (provider, _) = ParseProviderModel(fallback);
            if (_adapters.TryGetValue(provider, out adapter) && IsAvailable(adapter))
                return adapter;
        }
        
        throw new NoAvailableInferenceException(
            $"No available inference provider. Tried: {config.Provider}, {string.Join(", ", config.Fallback ?? [])}");
    }
    
    private static (string provider, string model) ParseProviderModel(string s)
    {
        var parts = s.Split(':');
        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }
    
    private bool IsAvailable(IInferenceAdapter adapter)
    {
        // Ollama is always "available" (local)
        if (adapter.Provider == "ollama")
            return true;
        
        // Others need API keys
        // In practice, we'd check credential store here
        return true;
    }
}
```

## A3: Platform Credential Stores

```
runtime/src/Purfle.Runtime/Platform/
├── ICredentialStore.cs
├── CredentialStoreFactory.cs
├── Windows/
│   └── WindowsCredentialStore.cs
├── MacOS/
│   └── MacOSCredentialStore.cs
├── Linux/
│   └── LinuxCredentialStore.cs
└── InMemory/
    └── InMemoryCredentialStore.cs
```

### WindowsCredentialStore.cs

```csharp
using System.Runtime.InteropServices;
using Windows.Security.Credentials;

namespace Purfle.Runtime.Platform.Windows;

public class WindowsCredentialStore : ICredentialStore
{
    private readonly PasswordVault _vault = new();
    
    public Task<string?> GetAsync(string key)
    {
        try
        {
            var credential = _vault.Retrieve("Purfle", key);
            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch (Exception)
        {
            return Task.FromResult<string?>(null);
        }
    }
    
    public Task SetAsync(string key, string value)
    {
        var credential = new PasswordCredential("Purfle", key, value);
        _vault.Add(credential);
        return Task.CompletedTask;
    }
    
    public Task DeleteAsync(string key)
    {
        var credential = _vault.Retrieve("Purfle", key);
        _vault.Remove(credential);
        return Task.CompletedTask;
    }
}
```

### MacOSCredentialStore.cs

```csharp
using System.Diagnostics;

namespace Purfle.Runtime.Platform.MacOS;

public class MacOSCredentialStore : ICredentialStore
{
    public async Task<string?> GetAsync(string key)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "security",
            Arguments = $"find-generic-password -a purfle -s \"{key}\" -w",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return process.ExitCode == 0 ? output.Trim() : null;
    }
    
    public async Task SetAsync(string key, string value)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "security",
            Arguments = $"add-generic-password -a purfle -s \"{key}\" -w \"{value}\" -U",
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        await process.WaitForExitAsync();
    }
    
    public async Task DeleteAsync(string key)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "security",
            Arguments = $"delete-generic-password -a purfle -s \"{key}\"",
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        await process.WaitForExitAsync();
    }
}
```

### LinuxCredentialStore.cs

```csharp
using System.Diagnostics;

namespace Purfle.Runtime.Platform.Linux;

public class LinuxCredentialStore : ICredentialStore
{
    // Uses secret-tool (libsecret)
    
    public async Task<string?> GetAsync(string key)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "secret-tool",
            Arguments = $"lookup application purfle key \"{key}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return process.ExitCode == 0 ? output.Trim() : null;
    }
    
    public async Task SetAsync(string key, string value)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "secret-tool",
            Arguments = $"store --label=\"Purfle: {key}\" application purfle key \"{key}\"",
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        await process.StandardInput.WriteAsync(value);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
    }
    
    public async Task DeleteAsync(string key)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "secret-tool",
            Arguments = $"clear application purfle key \"{key}\"",
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        await process.WaitForExitAsync();
    }
}
```

## A4: Commit Phase A

```bash
git add -A
git commit -m "feat(runtime): cross-platform foundation with all inference adapters

Runtime:
- MAUI-based cross-platform runtime
- Process-based agent isolation with IPC protocol
- Support for .dll, .js, .py agent entrypoints

Inference Adapters:
- Gemini (generativelanguage.googleapis.com)
- Anthropic (api.anthropic.com)
- OpenAI (api.openai.com)
- Ollama (localhost:11434)

Credential Stores:
- Windows (Credential Manager)
- macOS (Keychain via security CLI)
- Linux (Secret Service via secret-tool)"
```

Update `CLAUDE.md`: Phase A complete.

---

🔄 **RUN `/compact` NOW — Phase A complete**

---

# PHASE B: Polyglot Agent Examples (5 agents × 3 languages)

Each agent is implemented in **C#**, **TypeScript**, and **Python** to demonstrate polyglot support.

## B1: File Assistant (3 implementations)

### Agent Structure

```
agents/file-assistant/
├── manifest.agent.json          ← Universal (used by all implementations)
├── prompts/
│   └── system.md                ← Shared prompt
├── csharp/
│   ├── FileAssistant.csproj
│   ├── Program.cs               ← Entrypoint (reads stdin, writes stdout)
│   └── Agent.cs
├── typescript/
│   ├── package.json
│   ├── tsconfig.json
│   └── src/
│       └── index.ts             ← Entrypoint
├── python/
│   ├── requirements.txt
│   └── agent.py                 ← Entrypoint
└── README.md
```

### manifest.agent.json

```json
{
  "$schema": "https://purfle.dev/schema/agent.manifest.schema.json",
  "purfle": "0.1",
  "agent": {
    "id": "dev.purfle.file-assistant",
    "name": "File Assistant",
    "version": "1.0.0",
    "description": "Reads, summarizes, and searches local files"
  },
  "identity": {
    "type": "jws",
    "publicKey": {},
    "signature": ""
  },
  "capabilities": [
    { "id": "read-file", "mcpTool": "mcp://localhost:8100/files/read" },
    { "id": "list-directory", "mcpTool": "mcp://localhost:8100/files/list" },
    { "id": "search-files", "mcpTool": "mcp://localhost:8100/files/search" },
    { "id": "summarize-file" }
  ],
  "permissions": {
    "filesystem": { "read": ["./workspace"] },
    "network": { "required": ["generativelanguage.googleapis.com"] },
    "tools": { "mcp": ["mcp://localhost:8100/files/*"] }
  },
  "runtime": {
    "inference": {
      "provider": "gemini",
      "model": "gemini-1.5-flash",
      "fallback": ["anthropic:claude-sonnet-4-20250514", "openai:gpt-4o", "ollama:llama3"]
    },
    "entrypoints": {
      "dotnet": "csharp/bin/Release/net10.0/FileAssistant.dll",
      "node": "typescript/dist/index.js",
      "python": "python/agent.py"
    },
    "resources": { "memory": "256MB", "timeout": 30000 }
  }
}
```

### C# Implementation

**csharp/Program.cs:**

```csharp
using System.Text.Json;

var agent = new FileAssistantAgent();

while (true)
{
    var line = Console.ReadLine();
    if (line == null) break;
    
    var request = JsonSerializer.Deserialize<IpcRequest>(line);
    var response = await agent.HandleAsync(request);
    
    Console.WriteLine(JsonSerializer.Serialize(response));
    
    if (response.Done) break;
}

class FileAssistantAgent
{
    public async Task<IpcResponse> HandleAsync(IpcRequest request)
    {
        if (request.Type == "execute")
        {
            // Initial request - would call inference here
            // For now, return a tool call
            return new IpcResponse
            {
                Type = "response",
                Id = request.Id,
                Output = "Let me list those files for you.",
                ToolCalls = new[]
                {
                    new ToolCall
                    {
                        Id = Guid.NewGuid().ToString(),
                        Tool = "mcp://localhost:8100/files/list",
                        Arguments = new { path = "./workspace" }
                    }
                },
                Done = false
            };
        }
        else if (request.Type == "toolResult")
        {
            // Got tool result - return final response
            return new IpcResponse
            {
                Type = "response",
                Id = request.Id,
                Output = $"Here are the files: {request.Result}",
                Done = true
            };
        }
        
        throw new InvalidOperationException($"Unknown request type: {request.Type}");
    }
}
```

### TypeScript Implementation

**typescript/src/index.ts:**

```typescript
import * as readline from 'readline';

interface IpcRequest {
  type: 'execute' | 'toolResult';
  id: string;
  input?: string;
  result?: any;
}

interface IpcResponse {
  type: 'response';
  id: string;
  output: string;
  toolCalls?: { id: string; tool: string; arguments: any }[];
  done: boolean;
}

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

rl.on('line', async (line) => {
  const request: IpcRequest = JSON.parse(line);
  const response = await handleRequest(request);
  console.log(JSON.stringify(response));
  
  if (response.done) {
    process.exit(0);
  }
});

async function handleRequest(request: IpcRequest): Promise<IpcResponse> {
  if (request.type === 'execute') {
    return {
      type: 'response',
      id: request.id,
      output: 'Let me list those files for you.',
      toolCalls: [
        {
          id: crypto.randomUUID(),
          tool: 'mcp://localhost:8100/files/list',
          arguments: { path: './workspace' }
        }
      ],
      done: false
    };
  } else if (request.type === 'toolResult') {
    return {
      type: 'response',
      id: request.id,
      output: `Here are the files: ${JSON.stringify(request.result)}`,
      done: true
    };
  }
  
  throw new Error(`Unknown request type: ${request.type}`);
}
```

### Python Implementation

**python/agent.py:**

```python
#!/usr/bin/env python3
import sys
import json
import uuid

def handle_request(request: dict) -> dict:
    if request['type'] == 'execute':
        return {
            'type': 'response',
            'id': request['id'],
            'output': 'Let me list those files for you.',
            'toolCalls': [
                {
                    'id': str(uuid.uuid4()),
                    'tool': 'mcp://localhost:8100/files/list',
                    'arguments': {'path': './workspace'}
                }
            ],
            'done': False
        }
    elif request['type'] == 'toolResult':
        return {
            'type': 'response',
            'id': request['id'],
            'output': f"Here are the files: {json.dumps(request['result'])}",
            'done': True
        }
    else:
        raise ValueError(f"Unknown request type: {request['type']}")

def main():
    for line in sys.stdin:
        request = json.loads(line.strip())
        response = handle_request(request)
        print(json.dumps(response), flush=True)
        
        if response['done']:
            break

if __name__ == '__main__':
    main()
```

---

## B2: Purfle Pet (3 implementations)

Same structure — C#, TypeScript, Python implementations of the Tamagotchi agent.

```
agents/purfle-pet/
├── manifest.agent.json
├── prompts/
│   └── system.md
├── csharp/
│   ├── PurflePet.csproj
│   ├── Program.cs
│   ├── Agent.cs
│   └── AsciiArt.cs
├── typescript/
│   ├── package.json
│   └── src/
│       ├── index.ts
│       └── ascii-art.ts
├── python/
│   ├── requirements.txt
│   ├── agent.py
│   └── ascii_art.py
└── state/
    └── pet.json
```

**Key features:**
- Time-based stat decay
- ASCII art mood faces
- Persistent state via MCP server
- Personality development

---

## B3-B5: Remaining Agents

Similarly implement:
- **email-priority** (3 implementations)
- **news-digest** (3 implementations)  
- **api-guardian** (3 implementations)

Each with C#, TypeScript, Python versions.

---

## B6: Build All Polyglot Agents

```bash
# C# agents
dotnet build agents/file-assistant/csharp/
dotnet build agents/purfle-pet/csharp/
dotnet build agents/email-priority/csharp/
dotnet build agents/news-digest/csharp/
dotnet build agents/api-guardian/csharp/

# TypeScript agents
for agent in file-assistant purfle-pet email-priority news-digest api-guardian; do
  cd agents/$agent/typescript && npm install && npm run build && cd ../../..
done

# Python agents (no build needed, just validate syntax)
for agent in file-assistant purfle-pet email-priority news-digest api-guardian; do
  python -m py_compile agents/$agent/python/agent.py
done
```

## B7: Commit Phase B

```bash
git add -A
git commit -m "feat(agents): polyglot implementations of 5 core agents

Each agent implemented in:
- C# (.NET 10)
- TypeScript (Node.js 20)
- Python (3.11+)

Agents:
- file-assistant
- purfle-pet (tamagotchi)
- email-priority
- news-digest
- api-guardian

All use shared manifest and IPC protocol."
```

Update `CLAUDE.md`: Phase B complete.

---

🔄 **RUN `/compact` NOW — Phase B complete**

---

# PHASE C: Five More Agents (Polyglot)

## C1-C5: Build Remaining Agents

Each with C#, TypeScript, Python implementations:

- **code-reviewer** (8106)
- **meeting-assistant** (8107)
- **db-assistant** (8108)
- **research-assistant** (8109)
- **cli-generator** (8110)

## C6: Commit Phase C

```bash
git add -A
git commit -m "feat(agents): 5 more polyglot agents

- code-reviewer: code analysis + security scanning
- meeting-assistant: transcript summarization
- db-assistant: SQL schema analysis
- research-assistant: web research with citations
- cli-generator: CLI scaffolding

Total: 10 agents × 3 languages = 30 implementations"
```

Update `CLAUDE.md`: Phase C complete.

---

🔄 **RUN `/compact` NOW — Phase C complete**

---

# PHASE D: MCP Servers (All 11)

Build all MCP tool servers in TypeScript:

```bash
for server in \
  mcp-file-server \
  mcp-microsoft-email \
  mcp-gmail \
  mcp-news \
  mcp-pet \
  mcp-api-tools \
  mcp-code-tools \
  mcp-meeting \
  mcp-db-tools \
  mcp-research \
  mcp-cli-gen
do
  cd tools/$server && npm install && npm run build && cd ../..
done
```

## D1: Commit Phase D

```bash
git add -A
git commit -m "feat(tools): all 11 MCP servers

Ports 8100-8110:
- mcp-file-server (8100)
- mcp-microsoft-email (8101)
- mcp-gmail (8102)
- mcp-news (8103)
- mcp-pet (8104)
- mcp-api-tools (8105)
- mcp-code-tools (8106)
- mcp-meeting (8107)
- mcp-db-tools (8108)
- mcp-research (8109)
- mcp-cli-gen (8110)"
```

Update `CLAUDE.md`: Phase D complete.

---

🔄 **RUN `/compact` NOW — Phase D complete**

---

# PHASE E: IdentityHub

Rename `marketplace/` → `identityhub/` and implement:

- Agent registry
- Publisher verification
- Key registry (revocation)
- Attestation service

## E1: Commit Phase E

```bash
git add -A
git commit -m "feat(identityhub): registry and trust services

- Renamed from marketplace
- Agent registry with publish/search/download
- Publisher verification via DNS TXT
- Key registry with revocation
- Attestation service"
```

Update `CLAUDE.md`: Phase E complete.

---

🔄 **RUN `/compact` NOW — Phase E complete**

---

# PHASE F: Dashboard

Web UI for agent management.

## F1: Commit Phase F

```bash
git add -A
git commit -m "feat(dashboard): web UI for agent management

- .NET 10 API with SignalR
- React + Vite + Tailwind
- Real-time agent status
- Log streaming
- MCP server health"
```

Update `CLAUDE.md`: Phase F complete.

---

🔄 **RUN `/compact` NOW — Phase F complete**

---

# PHASE G: SDK + CLI

TypeScript SDK with commands:

- `purfle init`
- `purfle build`
- `purfle sign`
- `purfle validate`
- `purfle run`
- `purfle simulate`
- `purfle publish`
- `purfle search`
- `purfle install`
- `purfle security-scan`

## G1: Commit Phase G

```bash
git add -A
git commit -m "feat(sdk): TypeScript SDK and CLI

Commands: init, build, sign, validate, run, simulate, publish, search, install, security-scan

Templates: basic, tool-user, enterprise, mcp-server"
```

Update `CLAUDE.md`: Phase G complete.

---

🔄 **RUN `/compact` NOW — Phase G complete**

---

# PHASE H: CI/CD + Documentation

## H1: GitHub Actions

- CI workflow (.NET 10, Node 20, Python 3.11)
- Release workflow
- Dependabot

## H2: Documentation Site

Docusaurus with guides for all 10 agents, all 3 languages.

## H3: Commit Phase H

```bash
git add -A
git commit -m "ci+docs: GitHub Actions and documentation site

CI:
- Build/test all platforms
- .NET 10, Node 20, Python 3.11

Documentation:
- Getting started (C#, TypeScript, Python)
- All 10 agents documented
- IPC protocol reference
- Inference adapter guide"
```

Update `CLAUDE.md`: Phase H complete.

---

🔄 **RUN `/compact` NOW — Phase H complete**

---

# PHASE I: Tests + Polish

## I1: Comprehensive Tests

- Runtime tests
- Agent IPC tests
- SDK tests
- Integration tests
- Inference adapter tests (all 4)

## I2: Final Polish

- Fix all test failures
- Lint cleanup
- Update CHANGELOG.md
- Update README.md

## I3: Final Commit and Tag

```bash
git add -A
git commit -m "chore: v0.1.0 release

- 10 agents × 3 languages = 30 implementations
- 11 MCP servers
- 4 inference adapters (Gemini, Anthropic, OpenAI, Ollama)
- Cross-platform runtime (MAUI)
- IdentityHub for trust
- Dashboard for management
- Full test suite"

git push origin main

git tag -a v0.1.0 -m "Purfle v0.1.0 - Polyglot AI Agent Platform"
git push origin v0.1.0
```

---

# FINAL STATUS

```markdown
## Current Status

**v0.1.0 Released**

### Platform
- Cross-platform runtime via MAUI
- Process-based agent isolation (IPC protocol)
- Inference: Gemini, Anthropic, OpenAI, Ollama

### Agents (10 agents × 3 languages = 30 implementations)
| Agent | C# | TypeScript | Python |
|-------|-----|------------|--------|
| file-assistant | ✅ | ✅ | ✅ |
| purfle-pet | ✅ | ✅ | ✅ |
| email-priority | ✅ | ✅ | ✅ |
| news-digest | ✅ | ✅ | ✅ |
| api-guardian | ✅ | ✅ | ✅ |
| code-reviewer | ✅ | ✅ | ✅ |
| meeting-assistant | ✅ | ✅ | ✅ |
| db-assistant | ✅ | ✅ | ✅ |
| research-assistant | ✅ | ✅ | ✅ |
| cli-generator | ✅ | ✅ | ✅ |

### MCP Servers (11)
Ports 8100-8110

### Infrastructure
- IdentityHub (registry + trust)
- Dashboard (web UI)
- SDK + CLI
- CI/CD
- Documentation

**Next:** Phase 5 — Mobile runtimes (iOS/Android native)
```

---

**END OF MARATHON BUILD DOCUMENT**
