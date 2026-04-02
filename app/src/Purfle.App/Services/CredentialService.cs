namespace Purfle.App.Services;

/// <summary>
/// Retrieves API keys from MAUI SecureStorage.
/// </summary>
public sealed class CredentialService
{
    private const string AnthropicKeyName = "anthropic_api_key";
    private const string GeminiKeyName    = "gemini_api_key";

    public async Task<string?> GetAnthropicKeyAsync()
    {
        try { return await SecureStorage.Default.GetAsync(AnthropicKeyName); }
        catch { return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"); }
    }

    public async Task<string?> GetGeminiKeyAsync()
    {
        try { return await SecureStorage.Default.GetAsync(GeminiKeyName); }
        catch { return Environment.GetEnvironmentVariable("GEMINI_API_KEY"); }
    }
}
