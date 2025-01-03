using System.Security.Cryptography;

namespace Secrets_Exporter;

public static class CryptoUtils
{
    public static string Decrypt(string encryptedData, string password)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedData);
        using var aesAlg = Aes.Create();
        if (aesAlg == null)
        {
            throw new InvalidOperationException("Failed to create an AES algorithm instance...");
        }

        aesAlg.Key = GetKeyFromPassword(password);
        aesAlg.IV = new byte[16];

        using var msDecrypt = new MemoryStream(encryptedBytes);
        using var csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV),
            CryptoStreamMode.Read);
        using var reader = new StreamReader(csDecrypt);

        return reader.ReadToEnd();
    }


    private static byte[] GetKeyFromPassword(string password)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, new byte[16], 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }
}