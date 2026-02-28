// [Path]: Pulsar/Pulsar/Plugins/Core/Pki/PkiPlugin.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Core.Pki
{
    /// <summary>
    /// PKI 插件 - 处理密码自动填充和凭据注入
    /// </summary>
    public class PkiPlugin : IPulsarPlugin
    {
        private CredentialsManager? _credentialsManager;
        private IWindowService? _windowService;
        private SecretRepository? _secretRepository;
        private ILogger<PkiPlugin>? _logger;

        public string Id => "com.pulsar.pki";
        public string DisplayName => "PKI Credentials Manager";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Securely manages and injects credentials into applications.";
        public string Icon => "\uE72E"; // Lock Icon
        public bool CanDisable => false; // Core Plugin

        public void Initialize(IServiceProvider services)
        {
            _credentialsManager = services.GetService(typeof(CredentialsManager)) as CredentialsManager;
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
            _secretRepository = services.GetService(typeof(SecretRepository)) as SecretRepository;
            _logger = services.GetService(typeof(ILogger<PkiPlugin>)) as ILogger<PkiPlugin>;

            if (_credentialsManager == null)
            {
                throw new InvalidOperationException("CredentialsManager service is not available");
            }
            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService service is not available");
            }
            if (_secretRepository == null)
            {
                throw new InvalidOperationException("SecretRepository service is not available");
            }

            _logger?.LogInformation("[PkiPlugin] Initialized successfully");
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (_credentialsManager == null || _windowService == null)
            {
                return PluginResult.Error("Plugin not initialized");
            }

            return action.ToLowerInvariant() switch
            {
                "fill" => await FillCredentialsAsync(args, context),
                "inject" => await FillCredentialsAsync(args, context), // 别名
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        private async Task<PluginResult> FillCredentialsAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (_credentialsManager == null || _windowService == null)
            {
                return PluginResult.Error("Services not initialized");
            }

            if (_secretRepository == null)
            {
                return PluginResult.Error("Secret repository not initialized");
            }

            // 1. 验证参数
            if (!args.TryGetValue("secretId", out var secretId) || string.IsNullOrEmpty(secretId))
            {
                return PluginResult.Error("Missing required parameter: secretId");
            }

            _logger?.LogInformation("[PkiPlugin] Starting injection for secret: {SecretId}", secretId);

            try
            {
                // 2. 加载并解密凭据
                if (!Guid.TryParse(secretId, out var secretGuid))
                {
                    _logger?.LogWarning("[PkiPlugin] Invalid secret ID format: {SecretId}", secretId);
                    return PluginResult.Error($"Invalid secret ID format: {secretId}");
                }

                var secrets = await _secretRepository.LoadAsync();

                if (!secrets.TryGetValue(secretGuid, out var payload))
                {
                    _logger?.LogWarning("[PkiPlugin] Secret not found: {SecretId}", secretId);
                    return PluginResult.Error($"Secret not found: {secretId}");
                }

                if (string.IsNullOrEmpty(payload.EncryptedData))
                {
                    _logger?.LogWarning("[PkiPlugin] EncryptedData is empty for: {SecretId}", secretId);
                    return PluginResult.Error("Secret data is empty");
                }

                string password = _credentialsManager.Decrypt(payload.EncryptedData);
                if (string.IsNullOrEmpty(password))
                {
                    _logger?.LogWarning("[PkiPlugin] Decryption failed");
                    return PluginResult.Error("Decryption failed");
                }

                // 3. 隐藏 Pulsar 窗口
                _windowService.HideMainWindow();

                // 4. 归还焦点到目标窗口 (使用上下文中捕获的句柄)
                var targetHwnd = context.TargetWindowHandle;
                if (targetHwnd != IntPtr.Zero)
                {
                    WindowHelper.SetForegroundWindow(targetHwnd);
                    _logger?.LogDebug("[PkiPlugin] Focus returned to window: {Hwnd}", targetHwnd);
                }
                else
                {
                    _logger?.LogWarning("[PkiPlugin] TargetWindowHandle is Zero");
                }

                // 5. 等待窗口切换缓冲
                await Task.Delay(100);

                // 6. 注入序列 (UIA -> SendKeys fallback)
                // Try UIA first: set text into the currently focused element without touching clipboard.
                if (!string.IsNullOrEmpty(payload.Account))
                {
                    if (!UiaHelper.TrySetFocusedElementText(payload.Account))
                    {
                        SendKeys.SendWait(EscapeSendKeys(payload.Account));
                    }

                    await Task.Delay(10);
                    SendKeys.SendWait("{TAB}");
                    await Task.Delay(10);
                }

                if (!UiaHelper.TrySetFocusedElementText(password))
                {
                    SendKeys.SendWait(EscapeSendKeys(password));
                }

                // 7. 自动回车 (从 args 读取，默认为 false)
                bool autoEnter = args.TryGetValue("autoEnter", out var autoEnterStr) 
                    && bool.TryParse(autoEnterStr, out var result) && result;

                if (autoEnter)
                {
                    await Task.Delay(10);
                    SendKeys.SendWait("{ENTER}");
                }

                _logger?.LogInformation("[PkiPlugin] Injection sequence finished");
                return PluginResult.Ok("Credentials injected successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PkiPlugin] Injection failed");
                return PluginResult.Error($"Injection failed: {ex.Message}");
            }
        }

        private string EscapeSendKeys(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // SendKeys 特殊字符转义
            return input
                .Replace("{", "{{}}")
                .Replace("}", "{}}")
                .Replace("[", "{[}")
                .Replace("]", "]}")
                .Replace("+", "{+}")
                .Replace("^", "{^}")
                .Replace("%", "{%}")
                .Replace("~", "{~}")
                .Replace("(", "{(}")
                .Replace(")", "{)}");
        }
    }
}
