using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Crypto
{
    internal class EncryptString
    {
        static readonly int KeySize = 32; 
        static readonly int IvSize = 16;  
        static readonly int SaltSize = 16;
        static readonly int Iterations = 10000;
        public static string Encrypt(string plainText, string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    using var key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
#else
            using var key = new Rfc2898DeriveBytes(password, salt, Iterations);
#endif
            byte[] keyBytes = key.GetBytes(KeySize);
            byte[] iv = key.GetBytes(IvSize);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = iv;

            using var ms = new MemoryStream();
            ms.Write(salt, 0, salt.Length);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                cs.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText, string password)
        {
            byte[] bytes = Convert.FromBase64String(cipherText);
            byte[] salt = new byte[SaltSize];
            Array.Copy(bytes, 0, salt, 0, salt.Length);

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    using var key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
#else
            using var key = new Rfc2898DeriveBytes(password, salt, Iterations);
#endif
            byte[] keyBytes = key.GetBytes(KeySize);
            byte[] iv = key.GetBytes(IvSize);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = iv;

            using var ms = new MemoryStream(bytes, SaltSize, bytes.Length - SaltSize);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }
}
