namespace Purfle.Runtime.Lifecycle;

public enum LoadFailureReason
{
    MalformedJson,
    SchemaValidationFailed,
    KeyNotFound,
    KeyRevoked,
    SignatureInvalid,
    ManifestExpired,
    IdentityExpired,
    CapabilityMissing,
    IoSchemaInvalid,
    EngineNotSupported,
    AssemblyLoadFailed,
    AssemblyEntryPointMissing,
    InitFailed,
}
