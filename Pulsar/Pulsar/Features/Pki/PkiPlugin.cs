// [Path]: Pulsar/Pulsar/Features/Pki/PkiPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pulsar.Core.Plugin;
using Pulsar.Features.Pki.Services;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Pki
{
    /// <summary>
    /// PKI 插件 - 处理密码自动填充和凭据注入
    /// </summary>
    public class PkiPlugin : IPulsarPlugin
    {
        private CredentialsManager? _credentialsManager;
        private IWindowService? _windowService;

        public string Id => "com.pulsar.pki";
        public string DisplayName => "PKI Credentials Manager";

        public void Initialize(IServiceProvider services)
        {
            _credentialsManager = services.GetService(typeof(CredentialsManager)) as CredentialsManager;
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;

            if (_credentialsManager == null)
            {
                throw new InvalidOperationException("CredentialsManager service is not available");
            }
            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService service is not available");
            }

            Debug.WriteLine("[PkiPlugin] Initialized successfully");
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
            // 1. 验证参数
            if (!args.TryGetValue("secretId", out var secretId) || string.IsNullOrEmpty(secretId))
            {
                return PluginResult.Error("Missing required parameter: secretId");
            }

            Debug.WriteLine($"[PkiPlugin] Starting injection for secret: {secretId}");

            try
            {
                // 2. 加载并解密凭据
                if (!Guid.TryParse(secretId, out var secretGuid))
                {
                    Debug.WriteLine($"[PkiPlugin] ❌ Invalid secret ID format: {secretId}");
                    return PluginResult.Error($"Invalid secret ID format: {secretId}");
                }

                var secretRepo = new SecretRepository();
                var secrets = await secretRepo.LoadAsync();

                if (!secrets.TryGetValue(secretGuid, out var payload))
                {
                    Debug.WriteLine($"[PkiPlugin] ❌ Secret not found: {secretId}");
                    return PluginResult.Error($"Secret not found: {secretId}");
                }

                if (string.IsNullOrEmpty(payload.EncryptedData))
                {
                    Debug.WriteLine($"[PkiPlugin] ❌ EncryptedData is empty for: {secretId}");
                    return PluginResult.Error("Secret data is empty");
                }

                string password = _credentialsManager.Decrypt(payload.EncryptedData);
                if (string.IsNullOrEmpty(password))
                {
                    Debug.WriteLine("[PkiPlugin] ❌ Decryption failed");
                    return PluginResult.Error("Decryption failed");
                }

                // 3. 隐藏 Pulsar 窗口
                _windowService.HideMainWindow();

                // 4. 归还焦点到目标窗口 (使用上下文中捕获的句柄)
                var targetHwnd = context.TargetWindowHandle;
                if (targetHwnd != IntPtr.Zero)
                {
                    WindowHelper.SetForegroundWindow(targetHwnd);
                    Debug.WriteLine($"[PkiPlugin] Focus returned to window: {targetHwnd}");
                }
                else
                {
                    Debug.WriteLine("[PkiPlugin] ⚠️ Warning: TargetWindowHandle is Zero");
                }

                // 5. 等待窗口切换缓冲
                await Task.Delay(100);

                // 6. 注入序列
                // 如果有账号，发送 账号 + TAB
                if (!string.IsNullOrEmpty(payload.Account))
                {
                    SendKeys.SendWait(EscapeSendKeys(payload.Account));
                    await Task.Delay(10);
                    SendKeys.SendWait("{TAB}");
                    await Task.Delay(10);
                }

                // 始终发送密码
                SendKeys.SendWait(EscapeSendKeys(password));

                // 7. 自动回车 (从 args 读取，默认为 false)
                bool autoEnter = args.TryGetValue("autoEnter", out var autoEnterStr) 
                    && bool.TryParse(autoEnterStr, out var result) && result;

                if (autoEnter)
                {
                    await Task.Delay(10);
                    SendKeys.SendWait("{ENTER}");
                }

                Debug.WriteLine("[PkiPlugin] ✓ Injection sequence finished");
                return PluginResult.Ok("Credentials injected successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PkiPlugin] ❌ Exception: {ex.Message}");
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
                .Replace("]", "{]}")
                .Replace("+", "{+}")
                .Replace("^", "{^}")
                .Replace("%", "{%}")
                .Replace("~", "{~}")
                .Replace("(", "{(}")
                .Replace(")", "{)}");
        }
    }
}
