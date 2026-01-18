using System;
using System.Threading.Tasks;
using System.Windows.Forms; // 需要引用 System.Windows.Forms
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

            // 1. 解密数据 (Fail Fast)
            string password = _credentialsManager.Decrypt(secret.EncryptedData);
            if (string.IsNullOrEmpty(password)) return;

            // ========================================================
            // 🌀 Focus Boomerang (焦点回旋镖)
            // 目标: Hide Pulsar -> Restore Target Window -> Wait -> Inject
            // ========================================================

            // 2. 隐藏 Pulsar 窗口
            // 必须先隐藏，否则 Pulsar 可能会因为 TopMost 属性遮挡目标窗口，
            // 或者 SetForegroundWindow 调用后焦点又被 Pulsar 抢回来。
            _windowService.HideMainWindow();

            // 3. 归还焦点
            var targetHwnd = _windowService.GetPreviousWindow();
            if (targetHwnd != IntPtr.Zero)
            {
                WindowHelper.SetForegroundWindow(targetHwnd);
            }

            // 4. [关键] 等待焦点切换完成 (Buffer Time)
            // Windows 的窗口切换动画和输入队列处理需要时间。
            // 经验值：50ms 极速模式，100-150ms 稳健模式。
            await Task.Delay(100);

            // 5. 执行注入
            // TODO: 未来可以使用 InputSimulator 库来获得更好的兼容性
            // 目前使用 SendKeys 作为 MVP 实现

            // 发送账号 (如果配置了)
            if (!string.IsNullOrEmpty(secret.Account))
            {
                SendKeys.SendWait(EscapeSendKeys(secret.Account));
                await Task.Delay(10);
                SendKeys.SendWait("{TAB}");
                await Task.Delay(10);
            }

            // 发送密码
            SendKeys.SendWait(EscapeSendKeys(password));

            // 自动回车
            if (secret.AutoEnter)
            {
                await Task.Delay(10);
                SendKeys.SendWait("{ENTER}");
            }
        }

        /// <summary>
        /// 转义 SendKeys 的特殊字符 (+, ^, %, ~, {}, [], etc.)
        /// </summary>
        private string EscapeSendKeys(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // SendKeys 使用 {} 来转义特殊字符
            // 注意替换顺序
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