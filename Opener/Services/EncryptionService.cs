using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Opener.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class DpapiEncryptionService : IEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OpenerTool_Entropy_2026");

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            throw new InvalidOperationException("Failed to decrypt data. Ensure you are running as the same user who created the key.");
        }
    }
}

/// <summary>
/// AES-256-GCM encryption with password-derived key (PBKDF2).
/// Portable across machines - requires password.
/// </summary>
public class PortableEncryptionService : IEncryptionService
{
    private readonly string _password;
    private const int SaltSize = 16;
    private const int KeySize = 32; // 256 bits
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 100000;

    public PortableEncryptionService(string password)
    {
        _password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = DeriveKey(_password, salt);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Format: salt + nonce + tag + ciphertext
        byte[] result = new byte[SaltSize + NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, SaltSize + NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            byte[] data = Convert.FromBase64String(cipherText);
            
            byte[] salt = new byte[SaltSize];
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            int cipherLen = data.Length - SaltSize - NonceSize - TagSize;
            byte[] cipherBytes = new byte[cipherLen];
            byte[] plainBytes = new byte[cipherLen];

            Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(data, SaltSize, nonce, 0, NonceSize);
            Buffer.BlockCopy(data, SaltSize + NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(data, SaltSize + NonceSize + TagSize, cipherBytes, 0, cipherLen);

            byte[] key = DeriveKey(_password, salt);

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Failed to decrypt. Invalid password or corrupted data.");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}

/// <summary>
/// Factory to create appropriate encryption service based on config.
/// </summary>
public static class EncryptionServiceFactory
{
    public static IEncryptionService Create(IConfigService configService, ICredentialService credentialService)
    {
        if (configService.IsPortableMode())
        {
            string? password = credentialService.GetPassword();
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Portable mode requires a password. Run 'o config set encryption portable' to set one.");
            }
            return new PortableEncryptionService(password);
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new DpapiEncryptionService();
        }

        // Cross-platform 'Local' fallback: Use a persistent generated machine key
        return new MachineLocalEncryptionService();
    }
}

/// <summary>
/// Provides 'Local' encryption on non-Windows platforms by generating and storing
/// a unique machine/user key in the home directory.
/// </summary>
public class MachineLocalEncryptionService : IEncryptionService
{
    private readonly string _keyPath;
    private readonly PortableEncryptionService _aesService;

    public MachineLocalEncryptionService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(home, ".opener");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        _keyPath = Path.Combine(dir, ".machine_key");

        string key = GetOrCreateKey();
        _aesService = new PortableEncryptionService(key);
    }

    private string GetOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            return File.ReadAllText(_keyPath);
        }

        string newKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        File.WriteAllText(_keyPath, newKey);
        
        // Try to set permissions to 600 on Linux/macOS
        try 
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("chmod", $"600 {_keyPath}");
            }
        } catch { /* ignore if chmod fails */ }

        return newKey;
    }

    public string Encrypt(string plainText) => _aesService.Encrypt(plainText);
    public string Decrypt(string cipherText) => _aesService.Decrypt(cipherText);
}


