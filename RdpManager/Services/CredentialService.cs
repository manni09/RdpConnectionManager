using System;
using System.Security.Cryptography;
using System.Text;

namespace RdpManager.Services
{
    /// <summary>
    /// Provides secure credential encryption using Windows DPAPI.
    /// Credentials are encrypted with user-specific keys and cannot be decrypted by other users.
    /// </summary>
    public static class CredentialService
    {
        private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("RdpManager_v1_Salt");

        /// <summary>
        /// Encrypts a password using Windows Data Protection API (DPAPI).
        /// The encrypted data is tied to the current Windows user account.
        /// </summary>
        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            try
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] encryptedBytes = ProtectedData.Protect(
                    passwordBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to encrypt password.", ex);
            }
        }

        /// <summary>
        /// Decrypts a password that was encrypted with EncryptPassword.
        /// </summary>
        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to decrypt password. The credential may have been created by a different user.", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Invalid encrypted password format.", ex);
            }
        }

        /// <summary>
        /// Stores credentials in Windows Credential Manager for use with mstsc.
        /// </summary>
        public static void StoreWindowsCredential(string target, string username, string password)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(username))
                return;

            try
            {
                // Remove port from target if present (credential manager uses just hostname)
                string hostname = target.Contains(":") ? target.Split(':')[0] : target;
                
                // First, delete any existing credential
                var deleteProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmdkey.exe",
                        Arguments = $"/delete:TERMSRV/{hostname}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                deleteProcess.Start();
                deleteProcess.WaitForExit(3000);

                // Escape special characters in password for command line
                string escapedPassword = password.Replace("\"", "\\\"");
                
                // Use cmdkey to store credentials for RDP
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmdkey.exe",
                        Arguments = $"/add:TERMSRV/{hostname} /user:\"{username}\" /pass:\"{escapedPassword}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
                
                if (process.ExitCode != 0)
                {
                    LoggingService.Warn($"cmdkey returned exit code {process.ExitCode}: {error}");
                }
                else
                {
                    LoggingService.Debug($"Credentials stored for TERMSRV/{hostname}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Failed to store Windows credential: {ex.Message}");
                // Connection will still work, just won't auto-login
            }
        }

        /// <summary>
        /// Removes stored Windows credentials for a target.
        /// </summary>
        public static void RemoveWindowsCredential(string target)
        {
            if (string.IsNullOrEmpty(target))
                return;

            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmdkey.exe",
                        Arguments = $"/delete:TERMSRV/{target}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(5000);
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
