using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YAInventory.Services
{
    /// <summary>
    /// AES-256 encryption helper.
    /// Derives a 256-bit key from the given password via PBKDF2 (SHA-256, 100 000 iterations).
    /// File format on disk:  [16-byte IV] + [AES-CBC ciphertext]
    /// </summary>
    public static class CryptoHelper
    {
        // Fixed salt so the same password always produces the same key.
        // Safe here because the password is application-embedded, not user-facing.
        private static readonly byte[] Salt =
            Encoding.UTF8.GetBytes("YAInventory_FixedSalt_2026");

        private const int Iterations = 100_000;
        private const int KeySize    = 256; // bits

        // ── Encrypt plain-text → file ──────────────────────────────────────
        public static void EncryptToFile(string filePath, string plainText, string password)
        {
            byte[] key = DeriveKey(password);

            using var aes = Aes.Create();
            aes.Key     = key;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            byte[] encrypted;
            using (var encryptor = aes.CreateEncryptor())
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }

            // Write [IV | ciphertext]
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            fs.Write(aes.IV, 0, aes.IV.Length);
            fs.Write(encrypted, 0, encrypted.Length);
        }

        // ── Decrypt file → plain-text ──────────────────────────────────────
        public static string DecryptFromFile(string filePath, string password)
        {
            byte[] allBytes = File.ReadAllBytes(filePath);
            if (allBytes.Length < 17)          // IV (16) + at least 1 byte of cipher
                return string.Empty;

            byte[] key = DeriveKey(password);

            byte[] iv   = allBytes[..16];
            byte[] data = allBytes[16..];

            using var aes = Aes.Create();
            aes.Key     = key;
            aes.IV      = iv;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes   = decryptor.TransformFinalBlock(data, 0, data.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        // ── Key derivation ─────────────────────────────────────────────────
        private static byte[] DeriveKey(string password)
        {
            using var kdf = new Rfc2898DeriveBytes(
                password, Salt, Iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(KeySize / 8);
        }
    }
}
