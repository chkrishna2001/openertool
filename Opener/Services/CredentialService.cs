using System;
using System.IO;
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

public static class CredentialServiceFactory
{
    public static ICredentialService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsCredentialService();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new SecretToolCredentialService();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacKeychainCredentialService();
        }
        return new FileCredentialService();
    }
}

/// <summary>
/// Fallback for platforms/environments where no OS keychain integration is available
/// (e.g. secret-tool/security missing, or an unrecognized OS).
/// Stores the password encrypted (AES-256-GCM, via the same machine-derived-key pattern
/// used by <see cref="MachineLocalEncryptionService"/>) in a file in the user's home
/// directory - never in plain text.
/// </summary>
public class FileCredentialService : ICredentialService
{
    private readonly string _path;
    private readonly string _keyPath;

    public FileCredentialService(string? basePathOverride = null)
    {
        var home = basePathOverride ?? ExecutionContextHelper.GetExecutionContextPath();
        var dir = Path.Combine(home, ".opener");
        _path = Path.Combine(dir, ".internal_pass");
        _keyPath = Path.Combine(dir, ".cred_key");
    }

    public string? GetPassword()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var key = MachineKeyStore.GetOrCreateKey(_keyPath);
            var encryptor = new PortableEncryptionService(key);
            var cipherText = File.ReadAllText(_path);
            return encryptor.Decrypt(cipherText);
        }
        catch
        {
            return null;
        }
    }

    public void SetPassword(string password)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var key = MachineKeyStore.GetOrCreateKey(_keyPath);
        var encryptor = new PortableEncryptionService(key);
        File.WriteAllText(_path, encryptor.Encrypt(password));
        MachineKeyStore.SecureFilePermissions(_path);
    }

    public void ClearPassword()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

/// <summary>
/// Linux credential store backed by the freedesktop.org Secret Service, via the
/// `secret-tool` CLI (part of libsecret; typically ships with GNOME Keyring/KWallet
/// integration). Falls back to the encrypted file-based store when `secret-tool`
/// is not present on PATH, or when a keychain call unexpectedly fails.
/// </summary>
public class SecretToolCredentialService : ICredentialService
{
    private const string CommandName = "secret-tool";
    private const string ServiceAttribute = "opener";

    private readonly IProcessRunner _runner;
    private readonly ICredentialService _fallback;
    private readonly string _account;

    public SecretToolCredentialService(IProcessRunner? runner = null, ICredentialService? fallback = null)
    {
        _runner = runner ?? new SystemProcessRunner();
        _fallback = fallback ?? new FileCredentialService();
        _account = Environment.UserName;
    }

    private bool IsAvailable => _runner.CommandExists(CommandName);

    public string? GetPassword()
    {
        if (!IsAvailable) return _fallback.GetPassword();

        try
        {
            var result = _runner.Run(CommandName, new[] { "lookup", "service", ServiceAttribute, "username", _account });
            if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.StandardOutput))
            {
                return result.StandardOutput.TrimEnd('\r', '\n');
            }
            return null;
        }
        catch
        {
            return _fallback.GetPassword();
        }
    }

    public void SetPassword(string password)
    {
        if (!IsAvailable)
        {
            _fallback.SetPassword(password);
            return;
        }

        try
        {
            var result = _runner.Run(
                CommandName,
                new[] { "store", "--label=Opener CLI", "service", ServiceAttribute, "username", _account },
                password);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"secret-tool store failed: {result.StandardError}");
            }
        }
        catch
        {
            _fallback.SetPassword(password);
        }
    }

    public void ClearPassword()
    {
        if (IsAvailable)
        {
            try
            {
                _runner.Run(CommandName, new[] { "clear", "service", ServiceAttribute, "username", _account });
            }
            catch
            {
                // Best-effort; fall through to also clear any stale fallback file below.
            }
        }

        _fallback.ClearPassword();
    }
}

/// <summary>
/// macOS credential store backed by the login Keychain, via the `security` CLI.
/// Falls back to the encrypted file-based store when `security` is not present
/// on PATH, or when a keychain call unexpectedly fails.
/// </summary>
public class MacKeychainCredentialService : ICredentialService
{
    private const string CommandName = "security";
    private const string ServiceName = "opener";

    private readonly IProcessRunner _runner;
    private readonly ICredentialService _fallback;
    private readonly string _account;

    public MacKeychainCredentialService(IProcessRunner? runner = null, ICredentialService? fallback = null)
    {
        _runner = runner ?? new SystemProcessRunner();
        _fallback = fallback ?? new FileCredentialService();
        _account = Environment.UserName;
    }

    private bool IsAvailable => _runner.CommandExists(CommandName);

    public string? GetPassword()
    {
        if (!IsAvailable) return _fallback.GetPassword();

        try
        {
            var result = _runner.Run(CommandName, new[] { "find-generic-password", "-a", _account, "-s", ServiceName, "-w" });
            if (result.ExitCode == 0)
            {
                var value = result.StandardOutput.TrimEnd('\r', '\n');
                return string.IsNullOrEmpty(value) ? null : value;
            }
            return null;
        }
        catch
        {
            return _fallback.GetPassword();
        }
    }

    public void SetPassword(string password)
    {
        if (!IsAvailable)
        {
            _fallback.SetPassword(password);
            return;
        }

        try
        {
            // -U: update the item in place if one already exists for this account/service.
            var result = _runner.Run(
                CommandName,
                new[] { "add-generic-password", "-a", _account, "-s", ServiceName, "-w", password, "-U" });

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"security add-generic-password failed: {result.StandardError}");
            }
        }
        catch
        {
            _fallback.SetPassword(password);
        }
    }

    public void ClearPassword()
    {
        if (IsAvailable)
        {
            try
            {
                _runner.Run(CommandName, new[] { "delete-generic-password", "-a", _account, "-s", ServiceName });
            }
            catch
            {
                // Best-effort; fall through to also clear any stale fallback file below.
            }
        }

        _fallback.ClearPassword();
    }
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
