namespace Purfle.Runtime.Lifecycle;

public enum LoadFailureReason
{
    MalformedJson,
    SchemaValidationFailed,
    KeyNotFound,
    KeyRevoked,
    SignatureInvalid,
    ManifestExpired,
    CapabilityMissing,
    IoSchemaInvalid,
    EngineNotSupported,
    AssemblyLoadFailed,
    AssemblyEntryPointMissing,
    InitFailed,
}
