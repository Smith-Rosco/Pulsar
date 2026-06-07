// [Path]: Pulsar/Pulsar/Views/Tutorial/TutorialStepCard.xaml.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Pulsar.Core.Localization;
using Pulsar.Models.Tutorial;

namespace Pulsar.Views.Tutorial
{
    /// <summary>
    /// 教程步骤卡片
    /// </summary>
    public partial class TutorialStepCard : UserControl
    {
        public event EventHandler? BackClicked;
        public event EventHandler? NextClicked;
        public event EventHandler? SkipClicked;
        
        private TutorialStep? _currentStep;
        private int _currentIndex;
        private int _totalSteps;
        private readonly ILocalizationService? _loc;

        private TextBlock? _waitHintText;
        private System.Windows.Controls.Button? _backButton;

        public TutorialStepCard()
        {
            InitializeComponent();

            _loc = (System.Windows.Application.Current as App)?.Services.GetService(typeof(ILocalizationService)) as ILocalizationService;
            _waitHintText = FindName("WaitHintText") as TextBlock;
            _backButton = FindName("BackButton") as System.Windows.Controls.Button;

            if (_loc != null)
            {
                _loc.LanguageChanged += OnLanguageChanged;
            }

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)Resources["CardEntranceAnimation"];
            storyboard?.Begin();

            _waitHintText ??= FindName("WaitHintText") as TextBlock;
            _backButton ??= FindName("BackButton") as System.Windows.Controls.Button;
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
            TitleText.Text = _loc?[_currentStep.Title] ?? _currentStep.Title;
            DescriptionText.Text = _loc?[_currentStep.Description] ?? _currentStep.Description;
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

            if (step.Type == TutorialStepType.Instruction)
            {
                NextButton.Visibility = Visibility.Visible;
            }
            else
            {
                NextButton.Visibility = Visibility.Visible;
            }
        }

        private string ResolvePrimaryButtonText(TutorialStep step)
        {
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

            if (step.Type == TutorialStepType.Instruction && string.IsNullOrWhiteSpace(step.WaitHintText))
            {
                _waitHintText.Visibility = Visibility.Collapsed;
                return;
            }

            var hintText = string.IsNullOrWhiteSpace(step.WaitHintText)
                ? (_loc?["Tutorial.WaitHintDefault"] ?? "It will continue automatically after completing the action.")
                : (_loc?[step.WaitHintText] ?? step.WaitHintText);

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

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            BackClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取当前步骤
        /// </summary>
        public TutorialStep? GetCurrentStep() => _currentStep;
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
