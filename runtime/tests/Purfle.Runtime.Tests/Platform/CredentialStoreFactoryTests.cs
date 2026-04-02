using System.Runtime.InteropServices;
using Purfle.Runtime.Platform;

namespace Purfle.Runtime.Tests.Platform;

public sealed class CredentialStoreFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNull()
    {
        var store = CredentialStoreFactory.Create();

        Assert.NotNull(store);
    }

    [SkippableFact]
    public void Create_OnWindows_ReturnsWindowsCredentialStore()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Only runs on Windows");

        var store = CredentialStoreFactory.Create();

        Assert.IsType<WindowsCredentialStore>(store);
    }

    [Fact]
    public void Create_ReturnsICredentialStore()
    {
        var store = CredentialStoreFactory.Create();

        Assert.IsAssignableFrom<ICredentialStore>(store);
    }
}
