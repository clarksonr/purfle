using System.Runtime.InteropServices;
using System.Text;

namespace Purfle.Runtime.Platform;

/// <summary>
/// Credential store backed by Windows Credential Manager via P/Invoke to advapi32.dll.
/// Credentials are stored as generic credentials with target name "Purfle:{key}".
/// </summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string TargetPrefix = "Purfle:";
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var target = TargetPrefix + key;
        var result = CredRead(target, CredTypeGeneric, 0, out var credentialPtr);

        if (!result)
            return Task.FromResult<string?>(null);

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                return Task.FromResult<string?>(null);

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, (int)credential.CredentialBlobSize);
            var value = Encoding.UTF8.GetString(bytes);
            return Task.FromResult<string?>(value);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var target = TargetPrefix + key;
        var bytes = Encoding.UTF8.GetBytes(value);

        var credential = new NativeCredential
        {
            Type = CredTypeGeneric,
            TargetName = target,
            CredentialBlobSize = (uint)bytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(bytes.Length),
            Persist = CredPersistLocalMachine,
            UserName = key
        };

        try
        {
            Marshal.Copy(bytes, 0, credential.CredentialBlob, bytes.Length);

            if (!CredWrite(ref credential, 0))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to write credential '{key}' to Windows Credential Manager. Win32 error: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var target = TargetPrefix + key;

        // CredDelete returns false if the credential doesn't exist; we treat that as success.
        if (!CredDelete(target, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            const int errorNotFound = 1168; // ERROR_NOT_FOUND
            if (error != errorNotFound)
            {
                throw new InvalidOperationException(
                    $"Failed to delete credential '{key}' from Windows Credential Manager. Win32 error: {error}");
            }
        }

        return Task.CompletedTask;
    }

    #region P/Invoke

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string target,
        int type,
        int reservedFlag,
        out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(
        ref NativeCredential credential,
        int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(
        string target,
        int type,
        int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    #endregion
}
