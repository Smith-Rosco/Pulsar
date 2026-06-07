using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Pulsar.Plugins.Core.Pki.Contracts;

namespace Pulsar.Plugins.Core.Pki.Services
{
    /// <summary>
    /// 负责凭据的安全存储与读取 (基于 Windows DPAPI)
    /// </summary>
    public class CredentialsManager : ISecretProtector
    {
        // 使用 CurrentUser 作用域，这样只有当前登录的 Windows 用户才能解密
        private const DataProtectionScope Scope = DataProtectionScope.CurrentUser;

        private readonly ILogger<CredentialsManager> _logger;

        public CredentialsManager(ILogger<CredentialsManager> logger)
        {
            _logger = logger;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, Scope);
            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, Scope);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CredentialsManager] Decryption failed ({ExceptionType}: {Message})",
                    ex.GetType().Name, ex.Message);
                return string.Empty;
            }
        }
    }
}
