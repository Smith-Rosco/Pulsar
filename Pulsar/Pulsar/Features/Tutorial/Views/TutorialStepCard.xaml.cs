// [Path]: Pulsar/Pulsar/Views/Tutorial/TutorialStepCard.xaml.cs

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Pulsar.Core.Localization;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Views
{
    /// <summary>
    /// 教程步骤卡片
    /// </summary>
    public partial class TutorialStepCard : UserControl
    {
        public event EventHandler? BackClicked;
        public event EventHandler? NextClicked;
        public event EventHandler? SkipClicked;
        public event EventHandler? ManualContinueRequested;
        
        private TutorialStep? _currentStep;
        private int _currentIndex;
        private int _totalSteps;
        private readonly ILocalizationService? _loc;

        private TextBlock? _waitHintText;
        private System.Windows.Controls.Button? _backButton;
        private System.Windows.Controls.Button? _continueButton;
        private System.Windows.Controls.ProgressBar? _stepProgressBar;

        public TutorialStepCard()
        {
            InitializeComponent();

            _loc = (System.Windows.Application.Current as App)?.Services.GetService(typeof(ILocalizationService)) as ILocalizationService;
            _waitHintText = FindName("WaitHintText") as TextBlock;
            _backButton = FindName("BackButton") as System.Windows.Controls.Button;
            _continueButton = FindName("ContinueButton") as System.Windows.Controls.Button;
            _stepProgressBar = FindName("StepProgressBar") as System.Windows.Controls.ProgressBar;

            if (_loc != null)
            {
                _loc.LanguageChanged += OnLanguageChanged;
            }

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _waitHintText ??= FindName("WaitHintText") as TextBlock;
            _backButton ??= FindName("BackButton") as System.Windows.Controls.Button;
            _continueButton ??= FindName("ContinueButton") as System.Windows.Controls.Button;
            _stepProgressBar ??= FindName("StepProgressBar") as System.Windows.Controls.ProgressBar;
        }

        private void OnLanguageChanged(object? sender, string e)
        {
            if (_currentStep == null) return;
            RefreshStepContent();
        }

        private void RefreshStepContent()
        {
            if (_currentStep == null) return;

            StepCounter.Text = string.Format(_loc?["Tutorial.StepFormat"] ?? "Step {0}/{1}", _currentIndex + 1, _totalSteps);
            TitleText.Text = !string.IsNullOrEmpty(_currentStep.TitleKey)
                ? (_loc?[_currentStep.TitleKey] ?? _currentStep.Title)
                : _currentStep.Title;
            DescriptionText.Text = !string.IsNullOrEmpty(_currentStep.DescriptionKey)
                ? (_loc?[_currentStep.DescriptionKey] ?? _currentStep.Description)
                : _currentStep.Description;
            NextButton.Content = ResolvePrimaryButtonText(_currentStep);

            ApplyWaitHint(_currentStep);
        }

        public void SetWaitHintText(string text)
        {
            if (_waitHintText != null)
            {
                _waitHintText.Text = _loc?[text] ?? text;
                _waitHintText.Visibility = Visibility.Visible;
            }
        }

        public void SetStep(TutorialStep step, int currentIndex, int totalSteps)
        {
            _currentStep = step;
            _currentIndex = currentIndex;
            _totalSteps = totalSteps;

            RefreshStepContent();
            ApplyBackButtonVisibility(currentIndex);

            if (_stepProgressBar != null && totalSteps > 0)
            {
                _stepProgressBar.Value = (double)(currentIndex + 1) / totalSteps;
            }

            if (step.Type == TutorialStepType.Instruction)
            {
                NextButton.Visibility = Visibility.Visible;
            }
            else
            {
                NextButton.Visibility = Visibility.Collapsed;
            }
        }

        private string ResolvePrimaryButtonText(TutorialStep step)
        {
            if (!string.IsNullOrWhiteSpace(step.PrimaryButtonTextKey))
            {
                var resolved = _loc?[step.PrimaryButtonTextKey] ?? step.PrimaryButtonText;
                if (!string.IsNullOrWhiteSpace(resolved))
                    return resolved;
            }

            if (!string.IsNullOrWhiteSpace(step.PrimaryButtonText))
            {
                var resolved = _loc?[step.PrimaryButtonText] ?? step.PrimaryButtonText;
                return resolved;
            }

            if (step.PrimaryAction == TutorialPrimaryAction.CompleteTutorial)
            {
                return _loc?["Tutorial.Complete"] ?? "Complete";
            }

            return step.Type == TutorialStepType.Instruction
                ? _loc?["Tutorial.Next"] ?? "Next"
                : _loc?["Tutorial.Continue"] ?? "Continue";
        }

        private void ApplyWaitHint(TutorialStep step)
        {
            if (_waitHintText == null)
            {
                return;
            }

            if (step.Type == TutorialStepType.Instruction && string.IsNullOrWhiteSpace(step.WaitHintText) && string.IsNullOrWhiteSpace(step.WaitHintKey))
            {
                _waitHintText.Visibility = Visibility.Collapsed;
                return;
            }

            string hintText;
            if (!string.IsNullOrWhiteSpace(step.WaitHintKey))
            {
                hintText = _loc?[step.WaitHintKey] ?? step.WaitHintText ?? string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(step.WaitHintText))
            {
                hintText = _loc?[step.WaitHintText] ?? step.WaitHintText;
            }
            else
            {
                hintText = _loc?["Tutorial.WaitHintDefault"] ?? "It will continue automatically after completing the action.";
            }

            if (string.IsNullOrWhiteSpace(hintText))
            {
                _waitHintText.Visibility = Visibility.Collapsed;
                return;
            }

            _waitHintText.Text = hintText;
            _waitHintText.Visibility = Visibility.Visible;
        }

        private void ApplyBackButtonVisibility(int currentIndex)
        {
            if (_backButton == null)
            {
                return;
            }

            _backButton.Visibility = currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 显示手动继续按钮（超时后调用）
        /// </summary>
        public void ShowManualContinueButton()
        {
            if (_continueButton != null)
            {
                _continueButton.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 隐藏手动继续按钮（触发器提前触发时调用）
        /// </summary>
        public void HideManualContinueButton()
        {
            if (_continueButton != null)
            {
                _continueButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 根据目标位置定位卡片
        /// </summary>
        public void PositionRelativeTo(Rect targetBounds, ArrowDirection direction)
        {
            if (targetBounds.IsEmpty)
            {
                // Center on screen
                CenterOnScreen();
                return;
            }

            // Calculate card position based on target and arrow direction
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            double cardX = 0;
            double cardY = 0;
            const double margin = 20; // Distance from target

            switch (direction)
            {
                case ArrowDirection.Top:
                    cardX = targetBounds.Left + (targetBounds.Width - ActualWidth) / 2;
                    cardY = targetBounds.Bottom + margin;
                    break;

                case ArrowDirection.Bottom:
                    cardX = targetBounds.Left + (targetBounds.Width - ActualWidth) / 2;
                    cardY = targetBounds.Top - ActualHeight - margin;
                    break;

                case ArrowDirection.Left:
                    cardX = targetBounds.Right + margin;
                    cardY = targetBounds.Top + (targetBounds.Height - ActualHeight) / 2;
                    break;

                case ArrowDirection.Right:
                    cardX = targetBounds.Left - ActualWidth - margin;
                    cardY = targetBounds.Top + (targetBounds.Height - ActualHeight) / 2;
                    break;
            }

            // Ensure card stays within screen bounds
            cardX = Math.Max(20, Math.Min(cardX, screenWidth - ActualWidth - 20));
            cardY = Math.Max(20, Math.Min(cardY, screenHeight - ActualHeight - 20));

            // Set position
            Canvas.SetLeft(this, cardX);
            Canvas.SetTop(this, cardY);
        }

        /// <summary>
        /// 居中显示卡片
        /// </summary>
        private void CenterOnScreen()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            Canvas.SetLeft(this, (screenWidth - ActualWidth) / 2);
            Canvas.SetTop(this, (screenHeight - ActualHeight) / 2);
        }

        private void OnContinueButtonClick(object sender, RoutedEventArgs e)
        {
            ManualContinueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnNextButtonClick(object sender, RoutedEventArgs e)
        {
            NextClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnSkipButtonClick(object sender, RoutedEventArgs e)
        {
            SkipClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            BackClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取当前步骤
        /// </summary>
        public TutorialStep? GetCurrentStep() => _currentStep;

        /// <summary>
        /// 设置 slot 缺失引导文本
        /// </summary>
        public void SetSlotMissingGuidance()
        {
            if (_loc == null) return;

            TitleText.Text = _loc["Tutorial.SlotMissingTitle"] ?? "Slots not configured";
            DescriptionText.Text = _loc["Tutorial.SlotMissingDesc"] ?? "Your Pulsar slots are not configured yet. Go to Settings → Slots to add one, or restore default slots.";
            WaitHintText.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Visible;
            NextButton.Content = _loc["Tutorial.Next"] ?? "Next";
        }

        /// <summary>
        /// 显示 Next 按钮（供 Orchestrator 调用）
        /// </summary>
        public void ShowNextButton()
        {
            NextButton.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 播放成功动画（绿色边框闪烁）
        /// </summary>
        public void PlaySuccessAnimation()
        {
            var storyboard = (Storyboard)Resources["SuccessBorderFlashAnimation"];
            storyboard?.Begin();
        }

        /// <summary>
        /// 播放渐出动画（用于跨步骤过渡）
        /// </summary>
        public async Task CrossfadeOutAsync()
        {
            var storyboard = (Storyboard)Resources["CardCrossfadeOutAnimation"];
            if (storyboard == null) return;

            var tcs = new TaskCompletionSource();
            void handler(object? s, EventArgs e)
            {
                storyboard.Completed -= handler;
                tcs.TrySetResult();
            }
            storyboard.Completed += handler;
            storyboard.Begin();

            await tcs.Task;
        }

        /// <summary>
        /// 播放入场动画
        /// </summary>
        public void PlayEntranceAnimation()
        {
            var storyboard = (Storyboard)Resources["CardEntranceAnimation"];
            storyboard?.Begin();
        }
    }

    /// <summary>
    /// 箭头方向
    /// </summary>
    public enum ArrowDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }
}
