// [Path]: Pulsar.Tests/TestHelpers/PulsarContextFactory.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.TestHelpers
{
    /// <summary>
    /// 测试辅助类 - 用于创建 PulsarContext 实例
    /// </summary>
    public static class PulsarContextFactory
    {
        /// <summary>
        /// 创建一个用于测试的 PulsarContext 实例
        /// </summary>
        /// <param name="targetWindowHandle">目标窗口句柄（默认 IntPtr.Zero）</param>
        /// <param name="targetProcessName">目标进程名（默认 "TESTPROCESS"）</param>
        /// <param name="targetProcessId">目标进程 ID（默认 1234）</param>
        /// <returns>PulsarContext 实例</returns>
        public static PulsarContext CreateTestContext(
            IntPtr? targetWindowHandle = null,
            string? targetProcessName = null,
            int? targetProcessId = null)
        {
            // 创建 Mock IWindowService
            var mockWindowService = new Mock<IWindowService>();
            
            // 设置默认值
            var hwnd = targetWindowHandle ?? IntPtr.Zero;
            var processName = targetProcessName ?? "TESTPROCESS";
            var pid = targetProcessId ?? 1234;
            
            // 配置 Mock 行为
            mockWindowService.Setup(x => x.GetPreviousWindow()).Returns(hwnd);
            mockWindowService.Setup(x => x.GetProcessWindowsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ProcessWindowInfo>());
            mockWindowService.Setup(x => x.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns((List<ProcessWindowInfo> windows, WindowSelectionRequest? request) => new WindowSelectionResult
                {
                    Request = request ?? new WindowSelectionRequest(),
                    SelectedWindow = windows.Count > 0 ? windows[0] : null,
                    DecisionReason = "Test default"
                });
            mockWindowService.Setup(x => x.SelectTargetWindowOrDefault(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns((List<ProcessWindowInfo> windows, WindowSelectionRequest? _) => windows.Count > 0 ? windows[0] : null);
            mockWindowService.Setup(x => x.ActivateWindow(It.IsAny<ProcessWindowInfo>())).Returns(true);
            mockWindowService.Setup(x => x.ActivateWindowDetailed(It.IsAny<ProcessWindowInfo>()))
                .Returns((ProcessWindowInfo window) => new WindowActivationResult { Window = window, Success = true, FailureReason = WindowActivationFailureReason.None });
            
            // 使用 Capture 方法创建上下文
            return PulsarContext.Capture(mockWindowService.Object, logger: null);
        }
        
        /// <summary>
        /// 创建一个带有自定义窗口列表的 PulsarContext 实例
        /// </summary>
        public static PulsarContext CreateTestContextWithWindows(
            List<ProcessWindowInfo> windows,
            IntPtr? targetWindowHandle = null,
            string? targetProcessName = null,
            int? targetProcessId = null)
        {
            var mockWindowService = new Mock<IWindowService>();
            
            var hwnd = targetWindowHandle ?? new IntPtr(12345);
            var pid = targetProcessId ?? 1234;
            
            mockWindowService.Setup(x => x.GetPreviousWindow()).Returns(hwnd);
            mockWindowService.Setup(x => x.GetProcessWindowsAsync(pid))
                .ReturnsAsync(windows);
            mockWindowService.Setup(x => x.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns((List<ProcessWindowInfo> candidates, WindowSelectionRequest? request) => new WindowSelectionResult
                {
                    Request = request ?? new WindowSelectionRequest(),
                    SelectedWindow = candidates.Count > 0 ? candidates[0] : null,
                    DecisionReason = "Test default"
                });
            mockWindowService.Setup(x => x.SelectTargetWindowOrDefault(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns((List<ProcessWindowInfo> candidates, WindowSelectionRequest? _) => candidates.Count > 0 ? candidates[0] : null);
            mockWindowService.Setup(x => x.ActivateWindow(It.IsAny<ProcessWindowInfo>())).Returns(true);
            mockWindowService.Setup(x => x.ActivateWindowDetailed(It.IsAny<ProcessWindowInfo>()))
                .Returns((ProcessWindowInfo window) => new WindowActivationResult { Window = window, Success = true, FailureReason = WindowActivationFailureReason.None });
            
            return PulsarContext.Capture(mockWindowService.Object, logger: null);
        }
    }
}
