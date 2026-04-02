using System.Runtime.InteropServices;

namespace Purfle.Runtime.Platform;

/// <summary>
/// Returns the appropriate credential store for the current platform.
/// Falls back to in-memory store for unsupported platforms.
/// </summary>
public static class CredentialStoreFactory
{
    public static ICredentialStore Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsCredentialStore();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSCredentialStore();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxCredentialStore();
        return new InMemoryCredentialStore();
    }
}
