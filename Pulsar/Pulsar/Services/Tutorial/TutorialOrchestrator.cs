// [Path]: Pulsar/Pulsar/Services/Tutorial/TutorialOrchestrator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Helpers.Tutorial;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Views.Tutorial;

namespace Pulsar.Services.Tutorial
{
    /// <summary>
    /// 教程编排器 - 管理教程流程的状态机
    /// </summary>
    public class TutorialOrchestrator
    {
        private readonly ILocalizationService _loc;

        private readonly IConfigService _configService;
        private readonly ILogger<TutorialOrchestrator> _logger;
        private readonly TutorialStepLoader _stepLoader;
        private readonly IOverlayManager _overlayManager;
        private readonly ITutorialTriggerEngine _triggerEngine;
        private readonly ITutorialSpotlightController _spotlightController;
        private readonly IWaitStepHintTimeout _waitStepHintTimeout;

        private string DefaultWaitHintText => _loc["Tutorial.NoActionDetectedHint"];
        
        private readonly List<TutorialStep> _steps;
        private int _currentStepIndex = -1;
        private TutorialStepCard? _stepCard;
        
        // [P0-3 Fix] 防止竞态条件的标志位
        private bool _isTransitioning = false;

        public TutorialStep? CurrentStep => _currentStepIndex >= 0 && _currentStepIndex < _steps.Count 
            ? _steps[_currentStepIndex] 
            : null;

        public event EventHandler<TutorialStep>? StepChanged;
        public event EventHandler? TutorialCompleted;
        public event EventHandler? TutorialSkipped;

        public TutorialOrchestrator(
            ILocalizationService loc,
            IConfigService configService,
            ILogger<TutorialOrchestrator> logger,
            TutorialStepLoader stepLoader,
            IOverlayManager overlayManager,
            ITutorialTriggerEngine triggerEngine,
            ITutorialSpotlightController spotlightController,
            IWaitStepHintTimeout waitStepHintTimeout)
        {
            _loc = loc;
            _configService = configService;
            _logger = logger;
            _stepLoader = stepLoader;
            _overlayManager = overlayManager;

            _triggerEngine = triggerEngine;
            _spotlightController = spotlightController;
            _waitStepHintTimeout = waitStepHintTimeout;
            
            _steps = InitializeSteps();
        }

        /// <summary>
        /// 初始化所有教程步骤
        /// </summary>
        private List<TutorialStep> InitializeSteps()
        {
            _logger.LogInformation("[TutorialOrchestrator] Initializing tutorial steps");
            
            // 使用配置加载器从 JSON 文件加载步骤
            var steps = _stepLoader.LoadSteps();
            
            _logger.LogInformation("[TutorialOrchestrator] Loaded {Count} tutorial steps", steps.Count);
            
            return steps;
        }

        /// <summary>
        /// 启动教程
        /// </summary>
        public async Task StartAsync()
        {
            // [P0-3 Fix] 防止重入
            if (_isTransitioning)
            {
                _logger.LogWarning("[TutorialOrchestrator] StartAsync called while transitioning, ignoring");
                return;
            }
            
            try
            {
                _isTransitioning = true;
                _logger.LogInformation("[TutorialOrchestrator] Starting tutorial");
                
                // 始终从第一步开始，清除上次进度
                _currentStepIndex = 0;
                await UpdateConfigAsync(s => s.LastTutorialStep = null);
                
                await ShowStepAsync(CurrentStep!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error starting tutorial");
                await HandleErrorAsync(ex);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 进入下一步
        /// </summary>
        public async Task NextStepAsync()
        {
            // [P0-3 Fix] 防止重入（快速双击 Next 按钮）
            if (_isTransitioning)
            {
                _logger.LogWarning("[TutorialOrchestrator] NextStepAsync called while transitioning, ignoring");
                return;
            }
            
            try
            {
                _isTransitioning = true;

                _waitStepHintTimeout.Cancel();
                
                if (_currentStepIndex < _steps.Count - 1)
                {
                    _currentStepIndex++;
                    await ShowStepAsync(CurrentStep!);
                }
                else
                {
                    await CompleteAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error advancing to next step");
                await HandleErrorAsync(ex);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 跳转到指定步骤
        /// </summary>
        public async Task GoToStepAsync(int stepIndex)
        {
            // [P0-3 Fix] 防止重入
            if (_isTransitioning)
            {
                _logger.LogWarning("[TutorialOrchestrator] GoToStepAsync called while transitioning, ignoring");
                return;
            }
            
            if (stepIndex < 0 || stepIndex >= _steps.Count)
            {
                _logger.LogError("[TutorialOrchestrator] Invalid step index: {Index}", stepIndex);
                throw new ArgumentOutOfRangeException(nameof(stepIndex), 
                    $"Step index must be between 0 and {_steps.Count - 1}");
            }
            
            try
            {
                _isTransitioning = true;
                
                _logger.LogInformation("[TutorialOrchestrator] Jumping to step {Index}: {StepId}", 
                    stepIndex, _steps[stepIndex].Id);
                
                _currentStepIndex = stepIndex;
                await ShowStepAsync(CurrentStep!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error jumping to step {Index}", stepIndex);
                await HandleErrorAsync(ex);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 根据步骤 ID 查找索引
        /// </summary>
        public int FindStepIndex(string stepId)
        {
            var index = _steps.FindIndex(s => s.Id == stepId);
            
            if (index == -1)
            {
                _logger.LogWarning("[TutorialOrchestrator] Step not found: {StepId}", stepId);
            }
            
            return index;
        }

        /// <summary>
        /// 获取所有步骤 ID
        /// </summary>
        public List<string> GetAllStepIds()
        {
            return _steps.Select(s => s.Id).ToList();
        }

        /// <summary>
        /// 显示指定步骤
        /// </summary>
        private async Task ShowStepAsync(TutorialStep step)
        {
            try
            {
                _logger.LogInformation("[TutorialOrchestrator] Showing step: {StepId}", step.Id);

                // 清理上一步的触发器
                _triggerEngine.Cleanup();
                _waitStepHintTimeout.Cancel();
                   
                // 清理上一步的卡片
                CleanupStepCard();

                // 更新配置中的当前步骤
                await UpdateConfigAsync(s => s.LastTutorialStep = step.Id);

                // 创建或更新遮罩窗口
                _overlayManager.EnsureOverlayWindow();

                // 根据 FocusMode 确定初始状态
                var initialState = DetermineFocusState(step);

                _spotlightController.ApplyForStep(step);

                // 创建并显示步骤卡片
                _stepCard = new TutorialStepCard();
                _stepCard.SetStep(step, _currentStepIndex, _steps.Count);
                _stepCard.BackClicked += OnStepCardBackClicked;
                _stepCard.NextClicked += OnStepCardNextClicked;
                _stepCard.SkipClicked += OnStepCardSkipClicked;

                _overlayManager.SetCardContent(_stepCard);

                // 配置卡片大小模式
                var sizeMode = step.Layout?.CardSizeMode ?? Models.Tutorial.CardSizeMode.Auto;
                var fixedWidth = step.Layout?.FixedCardWidth ?? 450;
                var fixedHeight = step.Layout?.FixedCardHeight ?? 300;
                _overlayManager.SetCardSizeMode(sizeMode, fixedWidth, fixedHeight);

                // [Fix] 根据初始状态设置窗口（只调用一次，传入最终的 CardPosition）
                var finalCardPosition = step.Layout?.CardPosition ?? Models.Tutorial.CardPosition.TopRight;
                
                if (initialState == Views.Tutorial.OverlayState.Focused)
                {
                    _overlayManager.EnterFocusedState();
                }
                else
                {
                    // [Fix] 只调用一次 EnterObservingState，传入最终位置
                    _overlayManager.EnterObservingState(finalCardPosition);
                }

                // 先显示窗口
                _overlayManager.Show();
                
                // [Fix] 移除窗口布局调整，避免闪动
                // 不再调用 _layoutManager.ApplyLayoutAsync，让用户自由调整窗口

                // 设置触发器
                _triggerEngine.Setup(step, OnTriggerFired);

                // Non-blocking timeout for wait steps (UX-only).
                _waitStepHintTimeout.Start(
                    step,
                    getCurrentStepId: () => CurrentStep?.Id,
                    onTimeoutAsync: () => System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (CurrentStep?.Id != step.Id)
                        {
                            return;
                        }

                        _stepCard?.SetWaitHintText(DefaultWaitHintText);
                    }).Task);

                // 触发事件
                StepChanged?.Invoke(this, step);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error showing step: {StepId}", step.Id);
                throw; // 重新抛出，让调用者的 try-catch 处理
            }
        }

        /// <summary>
        /// 完成教程
        /// </summary>
        private async Task CompleteAsync()
        {
            try
            {
                _logger.LogInformation("[TutorialOrchestrator] Tutorial completed");

                _waitStepHintTimeout.Cancel();

                await UpdateConfigAsync(s =>
                {
                    s.HasCompletedTutorial = true;
                    s.OnboardingState = "Complete";
                    s.LastTutorialStep = null;
                });

                _triggerEngine.Cleanup();
                CleanupStepCard();
                
                _overlayManager.Close();

                TutorialCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error completing tutorial");
                // 即使出错也要强制清理
                ForceCleanup();
            }
        }

        // Helper Methods

        /// <summary>
        /// 确保遮罩窗口已创建
        /// </summary>
        private void EnsureOverlayWindow()
        {
            _overlayManager.EnsureOverlayWindow();
        }

        /// <summary>
        /// 获取 SettingsWindow 实例（用于访问 NavigationView）
        /// </summary>
        private Views.SettingsWindow? GetSettingsWindow()
        {
            try
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is Views.SettingsWindow settingsWindow && window.IsVisible)
                    {
                        return settingsWindow;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TutorialOrchestrator] Failed to get SettingsWindow");
                return null;
            }
        }

        public async Task SkipAsync()
        {
            try
            {
                _logger.LogInformation("[TutorialOrchestrator] Skip requested");

                _waitStepHintTimeout.Cancel();

                await UpdateConfigAsync(s =>
                {
                    s.HasCompletedTutorial = false;
                    s.LastTutorialStep = "Skipped";
                });

                _triggerEngine.Cleanup();
                CleanupStepCard();

                _overlayManager.Close();

                TutorialSkipped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error while skipping tutorial");
                ForceCleanup();
            }
        }

        public async Task CompleteFromServiceAsync()
        {
            await CompleteAsync();
        }

        /// <summary>
        /// 清理步骤卡片资源
        /// </summary>
        private void CleanupStepCard()
        {
            try
            {
                if (_stepCard != null)
                {
                    // [P0-1 Fix] 取消订阅事件，防止内存泄漏
                    _stepCard.BackClicked -= OnStepCardBackClicked;
                    _stepCard.NextClicked -= OnStepCardNextClicked;
                    _stepCard.SkipClicked -= OnStepCardSkipClicked;
                    _stepCard = null;
                    
                    _logger.LogDebug("[TutorialOrchestrator] Step card cleaned up");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error cleaning up step card");
                // 确保引用被清除，即使取消订阅失败
                _stepCard = null;
            }
        }

        /// <summary>
        /// 根据步骤配置确定初始焦点状态
        /// </summary>
        private Views.Tutorial.OverlayState DetermineFocusState(TutorialStep step)
        {
            switch (step.FocusMode)
            {
                case Models.Tutorial.TutorialFocusMode.AlwaysFocused:
                    return Views.Tutorial.OverlayState.Focused;

                case Models.Tutorial.TutorialFocusMode.AlwaysObserving:
                    return Views.Tutorial.OverlayState.Observing;

                case Models.Tutorial.TutorialFocusMode.Auto:
                    // Instruction 步骤默认 Focused，WaitForAction 默认 Observing
                    return step.Type == Models.Tutorial.TutorialStepType.Instruction
                        ? Views.Tutorial.OverlayState.Focused
                        : Views.Tutorial.OverlayState.Observing;

                default:
                    return Views.Tutorial.OverlayState.Focused;
            }
        }

        /// <summary>
        /// 触发器触发时的回调
        /// </summary>
        private async void OnTriggerFired()
        {
            try
            {
                _logger.LogInformation("[TutorialOrchestrator] Trigger fired for step: {StepId}", CurrentStep?.Id);

                _waitStepHintTimeout.Cancel();
                 
                // [P0-2 Fix] 异步事件处理器的错误边界
                await NextStepAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error advancing to next step after trigger");
                await HandleErrorAsync(ex);
            }
        }

        /// <summary>
        /// 步骤卡片"下一步"按钮点击
        /// </summary>
        private async void OnStepCardNextClicked(object? sender, EventArgs e)
        {
            try
            {
                var step = CurrentStep;
                if (step == null)
                {
                    return;
                }

                switch (step.PrimaryAction)
                {
                    case TutorialPrimaryAction.OpenSettingsWindow:
                        await OpenSettingsWindowAsync();
                        return;

                    case TutorialPrimaryAction.CompleteTutorial:
                        await CompleteAsync();
                        return;

                    default:
                        _waitStepHintTimeout.Cancel();
                        await NextStepAsync();
                        return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error in next button handler");
                await HandleErrorAsync(ex);
            }
        }

        /// <summary>
        /// 步骤卡片"跳过"按钮点击
        /// </summary>
        private async void OnStepCardSkipClicked(object? sender, EventArgs e)
        {
            try
            {
                // [P0-2 Fix] 异步事件处理器的错误边界
                 
                _logger.LogInformation("[TutorialOrchestrator] Tutorial skipped by user");

                _waitStepHintTimeout.Cancel();

                await UpdateConfigAsync(s =>
                {
                    s.HasCompletedTutorial = false;
                    s.LastTutorialStep = "Skipped";
                });

                _triggerEngine.Cleanup();
                CleanupStepCard();
                 
                _overlayManager.Close();

                TutorialSkipped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error in skip button handler");
                // 跳过时出错,强制清理
                ForceCleanup();
            }
        }

        private async void OnStepCardBackClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_currentStepIndex <= 0)
                {
                    return;
                }

                _waitStepHintTimeout.Cancel();
                await GoToStepAsync(_currentStepIndex - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error in back button handler");
                await HandleErrorAsync(ex);
            }
        }

        /// <summary>
        /// 更新配置的辅助方法
        /// </summary>
        private async Task UpdateConfigAsync(Action<Models.ProfileSettings> updateAction)
        {
            try
            {
                var config = _configService.Current;
                updateAction(config.Settings);
                await _configService.SaveAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error updating config");
                // 配置保存失败不应该阻止教程继续
            }
        }

        /// <summary>
        /// 处理教程错误
        /// </summary>
        private async Task HandleErrorAsync(Exception ex)
        {
            try
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Tutorial error occurred, attempting graceful shutdown");

                _waitStepHintTimeout.Cancel();
                
                // 清理资源
                _triggerEngine.Cleanup();
                CleanupStepCard();
                
                // 标记为已完成,避免重复触发
                await UpdateConfigAsync(s =>
                {
                    s.HasCompletedTutorial = true;
                    s.LastTutorialStep = null;
                });
                
                // 关闭遮罩窗口
                _overlayManager.Close();
                
                // 重置转换标志
                _isTransitioning = false;
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "[TutorialOrchestrator] Error during error handling, forcing cleanup");
                // 如果优雅关闭失败，强制清理
                ForceCleanup();
            }
        }

        /// <summary>
        /// 强制清理所有资源
        /// </summary>
        private void ForceCleanup()
        {
            _logger.LogWarning("[TutorialOrchestrator] Force cleanup initiated");

            try
            {
                _waitStepHintTimeout.Cancel();
            }
            catch
            {
            }
            
            try
            {
                _triggerEngine.Cleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error cleaning up trigger during force cleanup");
            }
            
            try
            {
                CleanupStepCard();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error cleaning up step card during force cleanup");
            }
            
            try
            {
                _overlayManager.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error closing overlay window during force cleanup");
            }
            
            // 重置转换标志，确保下次可以启动
            _isTransitioning = false;
            
            _logger.LogInformation("[TutorialOrchestrator] Force cleanup completed");
        }

        private Task OpenSettingsWindowAsync()
        {
            _logger.LogInformation("[TutorialOrchestrator] Opening settings window from tutorial");

            var settingsWindow = GetSettingsWindow();
            if (settingsWindow != null)
            {
                _logger.LogInformation("[TutorialOrchestrator] Settings window already open, activating");
                settingsWindow.Activate();
                return Task.CompletedTask;
            }

            _logger.LogInformation("[TutorialOrchestrator] Creating new settings window");

            var app = System.Windows.Application.Current as App;
            if (app?.Services == null)
            {
                _logger.LogError("[TutorialOrchestrator] App services are unavailable while opening settings window");
                return Task.CompletedTask;
            }

            try
            {
                var window = app.Services.GetService<Views.SettingsWindow>();
                if (window == null)
                {
                    _logger.LogError("[TutorialOrchestrator] Failed to resolve SettingsWindow from DI container");
                    return Task.CompletedTask;
                }

                window.Show();
                window.Activate();
                _logger.LogInformation("[TutorialOrchestrator] Settings window opened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialOrchestrator] Error opening settings window");
            }

            return Task.CompletedTask;
        }
    }
}
