// [Path]: Pulsar/Pulsar/Services/Interfaces/ITutorialService.cs

using System;
using System.Threading.Tasks;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 教程服务接口
    /// 负责管理交互式教程的生命周期
    /// </summary>
    public interface ITutorialService
    {
        /// <summary>
        /// 教程是否正在运行
        /// </summary>
        bool IsTutorialActive { get; }

        /// <summary>
        /// 用户是否已完成教程
        /// </summary>
        bool HasCompletedTutorial { get; }

        /// <summary>
        /// 当前教程步骤
        /// </summary>
        TutorialStep? CurrentStep { get; }

        /// <summary>
        /// 启动教程
        /// </summary>
        Task StartTutorialAsync();

        /// <summary>
        /// 暂停教程
        /// </summary>
        void PauseTutorial();

        /// <summary>
        /// 恢复教程
        /// </summary>
        void ResumeTutorial();

        /// <summary>
        /// 跳过教程
        /// </summary>
        Task SkipTutorialAsync();

        /// <summary>
        /// 完成教程
        /// </summary>
        Task CompleteTutorialAsync();

        /// <summary>
        /// 跳转到指定步骤
        /// </summary>
        /// <param name="stepId">步骤 ID</param>
        Task GoToStepAsync(string stepId);

        /// <summary>
        /// 检查是否需要恢复教程
        /// </summary>
        Task CheckResumeAsync();

        /// <summary>
        /// 教程步骤变化事件
        /// </summary>
        event EventHandler<TutorialStepChangedEventArgs>? StepChanged;

        /// <summary>
        /// 教程完成事件
        /// </summary>
        event EventHandler? TutorialCompleted;

        /// <summary>
        /// 教程跳过事件
        /// </summary>
        event EventHandler? TutorialSkipped;
    }

    /// <summary>
    /// 教程步骤变化事件参数
    /// </summary>
    public class TutorialStepChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 前一个步骤
        /// </summary>
        public TutorialStep? PreviousStep { get; set; }

        /// <summary>
        /// 当前步骤
        /// </summary>
        public TutorialStep CurrentStep { get; set; }

        public TutorialStepChangedEventArgs(TutorialStep currentStep, TutorialStep? previousStep = null)
        {
            CurrentStep = currentStep;
            PreviousStep = previousStep;
        }
    }
}
