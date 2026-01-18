// [Path]: Pulsar/Pulsar/Features/Pki/PkiHandler.cs

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms; // 确保引用了 WinForms
using Pulsar.Core.Interfaces;
using Pulsar.Features.Pki.Models;
using Pulsar.Features.Pki.Services;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Pki
{
    public class PkiHandler : IActionHandler
    {
        private readonly CredentialsManager _credentialsManager;
        private readonly IWindowService _windowService;

        public PkiHandler(CredentialsManager credentialsManager, IWindowService windowService)
        {
            _credentialsManager = credentialsManager;
            _windowService = windowService;
        }

        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item is not SecretItem secret) return;
            Debug.WriteLine($"[PKI] Starting injection for: {secret.Label} (ID: {secret.Id})");

            // [Diagnostic] 检查数据完整性
            if (string.IsNullOrEmpty(secret.EncryptedData))
            {
                Debug.WriteLine($"[PKI] ❌ ERROR: EncryptedData is EMPTY. This indicates a hydration failure.");
                Debug.WriteLine($"[PKI]    - Possible cause: ID mismatch between appsettings.json and secrets.json.");
                Debug.WriteLine($"[PKI]    - Suggestion: Remove this slot in Settings and re-add it.");
                return;
            }

            // 1. 解密数据
            string password = _credentialsManager.Decrypt(secret.EncryptedData);
            if (string.IsNullOrEmpty(password))
            {
                Debug.WriteLine("[PKI] ❌ Decryption failed. Data exists but DPAPI failed (Scope/User mismatch).");
                return;
            }

            // 2. 隐藏 Pulsar 窗口
            _windowService.HideMainWindow();

            // 3. 归还焦点 (Focus Boomerang)
            var targetHwnd = _windowService.GetPreviousWindow();
            if (targetHwnd != IntPtr.Zero)
            {
                WindowHelper.SetForegroundWindow(targetHwnd);
            }

            // 4. 等待窗口切换缓冲
            await Task.Delay(100);

            // 5. 注入序列 (动态构建)
            // 如果有账号，发送 账号 + TAB
            if (!string.IsNullOrEmpty(secret.Account))
            {
                SendKeys.SendWait(EscapeSendKeys(secret.Account));
                await Task.Delay(10);
                SendKeys.SendWait("{TAB}");
                await Task.Delay(10);
            }

            // 始终发送密码
            SendKeys.SendWait(EscapeSendKeys(password));

            // 6. 自动回车
            if (secret.AutoEnter)
            {
                await Task.Delay(10);
                SendKeys.SendWait("{ENTER}");
            }

            Debug.WriteLine("[PKI] Injection sequence finished.");
        }

        private string EscapeSendKeys(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // SendKeys 特殊字符转义
            return input
               .Replace("{", "{{}")
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