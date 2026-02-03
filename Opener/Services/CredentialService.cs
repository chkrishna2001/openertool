using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Opener.Services;

public interface ICredentialService
{
    string? GetPassword();
    void SetPassword(string password);
    void ClearPassword();
}

[SupportedOSPlatform("windows")]
public class WindowsCredentialService : ICredentialService
{
    private const string CredentialTarget = "OpenerTool_PortableKey";

    public string? GetPassword()
    {
        bool success = CredRead(CredentialTarget, CRED_TYPE_GENERIC, 0, out IntPtr credentialPtr);
        if (!success) return null;

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob != IntPtr.Zero && credential.CredentialBlobSize > 0)
            {
                byte[] passwordBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
                return Encoding.UTF8.GetString(passwordBytes);
            }
            return null;
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void SetPassword(string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        var credential = new CREDENTIAL
        {
            Type = CRED_TYPE_GENERIC,
            TargetName = CredentialTarget,
            CredentialBlobSize = (uint)passwordBytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(passwordBytes.Length),
            Persist = CRED_PERSIST_LOCAL_MACHINE,
            UserName = Environment.UserName
        };

        try
        {
            Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
            bool success = CredWrite(ref credential, 0);
            if (!success)
            {
                throw new Exception($"Failed to save credential. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    public void ClearPassword()
    {
        CredDelete(CredentialTarget, CRED_TYPE_GENERIC, 0);
    }

    // P/Invoke declarations
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CredFree(IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, int type, int flags);
}
