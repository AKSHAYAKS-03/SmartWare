using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly string _key;

    public EncryptionService(IConfiguration configuration)
    {
        _key = configuration["SecuritySettings:AadhaarEncryptionKey"] 
               ?? throw new InvalidOperationException("Encryption key is missing in configuration.");
        
        // Ensure the key is exactly 32 bytes for AES-256
        var keyBytes = Encoding.UTF8.GetBytes(_key);
        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException("Encryption key must be exactly 32 bytes long.");
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        byte[] iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }

        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length);
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var fullCipher = Convert.FromBase64String(cipherText);

        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
