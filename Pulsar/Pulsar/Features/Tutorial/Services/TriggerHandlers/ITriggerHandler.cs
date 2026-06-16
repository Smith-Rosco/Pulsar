// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/ITriggerHandler.cs

using System;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    /// <summary>
    /// 教程触发器处理器接口
    /// 负责监听特定事件并在条件满足时触发回调
    /// </summary>
    public interface ITriggerHandler
    {
        /// <summary>
        /// 设置触发器监听
        /// </summary>
        /// <param name="trigger">触发器定义</param>
        /// <param name="onTriggered">触发时的回调</param>
        void Setup(TutorialTrigger trigger, Action onTriggered);

        /// <summary>
        /// 清理触发器监听（取消事件订阅）
        /// </summary>
        void Cleanup();
    }
}
