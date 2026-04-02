namespace Purfle.Runtime.Platform;

/// <summary>
/// Abstraction over platform-specific credential storage.
/// Implementations use Windows Credential Manager, macOS Keychain, or Linux Secret Service.
/// </summary>
public interface ICredentialStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task DeleteAsync(string key);
}
