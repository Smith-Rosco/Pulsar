using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件运行时「执行面」seam：槽位触发一条插件 Action，走完整执行管线
    /// （per-plugin 互斥、禁用门闩、权限评估、熔断、超时）。
    ///
    /// 只有 Slot 执行路径的调用方（<see cref="Pulsar.ViewModels.Strategies.PluginActionStrategy"/>
    /// 等）需要它 —— 不携带发现 / 安装 / 卸载等非执行关注点。
    /// 由 <see cref="Pulsar.Core.Plugin.Runtime.PluginRuntimeKernel"/> 实现。
    /// </summary>
    public interface IPluginExecutor
    {
        /// <summary>执行指定插件的 Action。返回 PluginResult，异常由管线收敛为结果。</summary>
        Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default);
    }
}
