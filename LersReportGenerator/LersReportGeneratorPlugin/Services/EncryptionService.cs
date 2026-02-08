using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Сервис для шифрования/расшифровки данных с использованием мастер-пароля
    /// Использует AES-256 + PBKDF2 для генерации ключа из пароля
    /// </summary>
    public static class EncryptionService
    {
        private const int KeySize = 256; // AES-256
        private const int SaltSize = 32; // 32 байта для salt
        private const int IVSize = 16;   // 16 байт для IV (AES block size)
        private const int Iterations = 10000; // Итерации для PBKDF2

        /// <summary>
        /// Шифрует текст с использованием мастер-пароля
        /// </summary>
        /// <param name="plainText">Текст для шифрования</param>
        /// <param name="masterPassword">Мастер-пароль</param>
        /// <param name="salt">Сгенерированный salt (Base64)</param>
        /// <param name="iv">Сгенерированный IV (Base64)</param>
        /// <returns>Зашифрованный текст (Base64)</returns>
        public static string Encrypt(string plainText, string masterPassword, out string salt, out string iv)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                salt = null;
                iv = null;
                return null;
            }

            if (string.IsNullOrEmpty(masterPassword))
            {
                throw new ArgumentException("Мастер-пароль не может быть пустым", nameof(masterPassword));
            }

            // Генерируем случайный salt
            byte[] saltBytes = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }

            // Генерируем ключ из пароля с использованием PBKDF2 (SHA256)
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(masterPassword, saltBytes, Iterations, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(KeySize / 8); // 256 бит = 32 байта
            }

            // Шифруем с AES
            byte[] encrypted;
            byte[] ivBytes;

            using (var aes = new AesCryptoServiceProvider())
            {
                aes.KeySize = KeySize;
                aes.Key = key;
                aes.GenerateIV();
                ivBytes = aes.IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var writer = new StreamWriter(cs))
                {
                    writer.Write(plainText);
                    writer.Flush();
                    cs.FlushFinalBlock();
                    encrypted = ms.ToArray();
                }
            }

            // Возвращаем результаты в Base64
            salt = Convert.ToBase64String(saltBytes);
            iv = Convert.ToBase64String(ivBytes);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Расшифровывает текст с использованием мастер-пароля
        /// </summary>
        /// <param name="encryptedText">Зашифрованный текст (Base64)</param>
        /// <param name="masterPassword">Мастер-пароль</param>
        /// <param name="salt">Salt (Base64)</param>
        /// <param name="iv">IV (Base64)</param>
        /// <returns>Расшифрованный текст</returns>
        /// <exception cref="CryptographicException">Неверный пароль или повреждённые данные</exception>
        public static string Decrypt(string encryptedText, string masterPassword, string salt, string iv)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return null;
            }

            if (string.IsNullOrEmpty(masterPassword))
            {
                throw new ArgumentException("Мастер-пароль не может быть пустым", nameof(masterPassword));
            }

            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(iv))
            {
                throw new ArgumentException("Salt и IV обязательны для расшифровки");
            }

            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] ivBytes = Convert.FromBase64String(iv);
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            // Генерируем ключ из пароля (тот же алгоритм, что при шифровании)
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(masterPassword, saltBytes, Iterations, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(KeySize / 8);
            }

            // Расшифровываем
            string plainText;
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.KeySize = KeySize;
                aes.Key = key;
                aes.IV = ivBytes;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(encryptedBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs))
                {
                    plainText = reader.ReadToEnd();
                }
            }

            return plainText;
        }

        /// <summary>
        /// Проверяет корректность мастер-пароля путём попытки расшифровки
        /// </summary>
        public static bool ValidatePassword(string encryptedText, string masterPassword, string salt, string iv)
        {
            try
            {
                Decrypt(encryptedText, masterPassword, salt, iv);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
