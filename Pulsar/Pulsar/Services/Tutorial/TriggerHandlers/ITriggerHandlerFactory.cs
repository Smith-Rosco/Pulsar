// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/ITriggerHandlerFactory.cs

using System;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial.TriggerHandlers
{
    /// <summary>
    /// 触发器处理器工厂接口
    /// 负责创建和管理触发器处理器实例
    /// </summary>
    public interface ITriggerHandlerFactory
    {
        /// <summary>
        /// 根据触发器类型创建对应的处理器
        /// </summary>
        /// <param name="type">触发器类型</param>
        /// <returns>触发器处理器实例，如果类型不支持则返回 null</returns>
        ITriggerHandler? CreateHandler(TutorialTriggerType type);

        /// <summary>
        /// 注册自定义触发器处理器
        /// </summary>
        /// <param name="type">触发器类型</param>
        /// <param name="factory">处理器工厂方法</param>
        void RegisterHandler(TutorialTriggerType type, Func<ITriggerHandler> factory);

        /// <summary>
        /// 检查是否支持指定的触发器类型
        /// </summary>
        /// <param name="type">触发器类型</param>
        /// <returns>如果支持返回 true，否则返回 false</returns>
        bool IsSupported(TutorialTriggerType type);
    }
}
