// [Path]: Pulsar/Pulsar/Views/Tutorial/TutorialStepCard.xaml.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Pulsar.Models.Tutorial;

namespace Pulsar.Views.Tutorial
{
    /// <summary>
    /// 教程步骤卡片
    /// </summary>
    public partial class TutorialStepCard : UserControl
    {
        public event EventHandler? NextClicked;
        public event EventHandler? SkipClicked;
        public event EventHandler? RetryLocateClicked;
        
        private TutorialStep? _currentStep;  // [Fix] 保存当前步骤引用

        // Some XAML fields may not be available until generated; keep a safe runtime reference.
        private TextBlock? _waitHintText;
        private System.Windows.Controls.Button? _retryButton;

        public TutorialStepCard()
        {
            InitializeComponent();

            _waitHintText = FindName("WaitHintText") as TextBlock;
            _retryButton = FindName("RetryButton") as System.Windows.Controls.Button;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Play entrance animation
            var storyboard = (Storyboard)Resources["CardEntranceAnimation"];
            storyboard?.Begin();

            // Ensure hint reference is resolved (XAML generator edge-cases).
            _waitHintText ??= FindName("WaitHintText") as TextBlock;
            _retryButton ??= FindName("RetryButton") as System.Windows.Controls.Button;
        }

        public void SetWaitHintText(string text)
        {
            if (_waitHintText != null)
            {
                _waitHintText.Text = text;
                _waitHintText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 设置步骤信息
        /// </summary>
        public void SetStep(TutorialStep step, int currentIndex, int totalSteps)
        {
            _currentStep = step;  // [Fix] 保存步骤引用
             
            StepCounter.Text = $"步骤 {currentIndex + 1}/{totalSteps}";
            TitleText.Text = step.Title;
            DescriptionText.Text = step.Description;

            // Reset hint visibility by default
            if (_waitHintText != null)
            {
                _waitHintText.Visibility = Visibility.Collapsed;
            }

            if (_retryButton != null)
            {
                _retryButton.Visibility = Visibility.Collapsed;
            }

            // [Fix] 根据步骤 ID 自定义按钮文本
            // Step2 is a special case: even if it's modeled as a wait step, the primary action is "open settings".
            if (step.Id == "step2_open_settings")
            {
                NextButton.Visibility = Visibility.Visible;
                NextButton.Content = "打开设置";

                if (_waitHintText != null)
                {
                    _waitHintText.Visibility = Visibility.Visible;
                }

                if (_retryButton != null)
                {
                    _retryButton.Visibility = Visibility.Visible;
                }

                return;
            }

            if (step.Type == TutorialStepType.Instruction)
            {
                NextButton.Visibility = Visibility.Visible;

                // [Fix] 最后一步显示"完成"而非"下一步"
                if (step.Id == "step9_completion")
                {
                    NextButton.Content = "完成";
                }
                else
                {
                    NextButton.Content = "下一步";
                }
            }
            else
            {
                // For WaitForAction steps, we normally auto-advance via triggers.
                // Still provide a visible manual "Continue" path to reduce dead-ends when triggers fail.
                if (_waitHintText != null)
                {
                    _waitHintText.Visibility = Visibility.Visible;
                }

                // Allow user to re-attempt detection/target location when something fails.
                if (_retryButton != null)
                {
                    _retryButton.Visibility = Visibility.Visible;
                }
                NextButton.Visibility = Visibility.Visible;
                NextButton.Content = "继续";
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

        private void OnNextButtonClick(object sender, RoutedEventArgs e)
        {
            NextClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnSkipButtonClick(object sender, RoutedEventArgs e)
        {
            SkipClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnRetryButtonClick(object sender, RoutedEventArgs e)
        {
            RetryLocateClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取当前步骤
        /// </summary>
        public TutorialStep? GetCurrentStep() => _currentStep;

        /// <summary>
        /// 显示"继续"按钮（用于 WaitForAction 步骤完成后）
        /// </summary>
        public void ShowContinueButton()
        {
            NextButton.Visibility = Visibility.Visible;
            NextButton.Content = "继续";
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
