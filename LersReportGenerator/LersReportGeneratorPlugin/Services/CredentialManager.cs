using System;
using System.Security.Cryptography;
using System.Text;
using LersReportCommon;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Secure credential storage using Windows DPAPI
    /// </summary>
    public static class CredentialManager
    {
        /// <summary>
        /// Encrypts a password using DPAPI (CurrentUser scope)
        /// </summary>
        /// <param name="password">Plain text password</param>
        /// <returns>Base64-encoded encrypted password</returns>
        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return null;

            try
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] encryptedBytes = ProtectedData.Protect(
                    passwordBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to encrypt password: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decrypts a password using DPAPI
        /// </summary>
        /// <param name="encryptedPassword">Base64-encoded encrypted password</param>
        /// <returns>Plain text password, or null if decryption fails</returns>
        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return null;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException ex)
            {
                // This can happen if the password was encrypted by a different user
                Logger.Error($"Failed to decrypt password (wrong user?): {ex.Message}");
                return null;
            }
            catch (FormatException ex)
            {
                // Invalid Base64
                Logger.Error($"Invalid encrypted password format: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to decrypt password: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates that a password can be encrypted and decrypted
        /// </summary>
        public static bool ValidateDpapiAvailable()
        {
            try
            {
                const string testPassword = "test";
                string encrypted = EncryptPassword(testPassword);
                if (encrypted == null)
                    return false;

                string decrypted = DecryptPassword(encrypted);
                return decrypted == testPassword;
            }
            catch
            {
                return false;
            }
        }
    }
}
