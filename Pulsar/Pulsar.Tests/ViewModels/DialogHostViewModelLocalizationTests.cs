using System.Windows.Input;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models.Enums;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Base;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class DialogHostViewModelLocalizationTests
    {
        [Fact]
        public void ConfigureButtons_UsesLocalizedLabels()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["Dialog.Button.Save"]).Returns("保存");
            loc.Setup(l => l["Dialog.Button.DontSave"]).Returns("不保存");
            loc.Setup(l => l["Dialog.Button.Cancel"]).Returns("取消");

            var vm = new DialogHostViewModel(loc.Object);
            vm.ConfigureButtons(DialogButtons.SaveDontSaveCancel);

            vm.PrimaryButtonText.Should().Be("保存");
            vm.TertiaryButtonText.Should().Be("不保存");
            vm.SecondaryButtonText.Should().Be("取消");
            vm.UseDangerStyleForTertiary.Should().BeTrue();
        }

        [Fact]
        public void ConfigureButtons_FallsBackToEnglish_WithoutLocalizationService()
        {
            var vm = new DialogHostViewModel(null);
            vm.ConfigureButtons(DialogButtons.OkCancel);

            vm.PrimaryButtonText.Should().Be("OK");
            vm.SecondaryButtonText.Should().Be("Cancel");
        }

        [Fact]
        public void CancelFromKeyboard_ClosesNormalDialogAsCancelled()
        {
            var vm = new DialogHostViewModel();
            DialogResult? requestedResult = null;
            vm.RequestClose = result => requestedResult = result;

            vm.CancelFromKeyboard();

            requestedResult.Should().Be(DialogResult.Cancelled);
        }

        [Fact]
        public void CancelFromKeyboard_DelegatesToWizardSecondaryCommand()
        {
            var secondaryCommand = new Mock<ICommand>();
            secondaryCommand
                .Setup(command => command.CanExecute(It.IsAny<object?>()))
                .Returns(true);

            var wizard = new Mock<IWizardDialogViewModel>();
            wizard.SetupGet(w => w.PrimaryButtonText).Returns("Next");
            wizard.SetupGet(w => w.SecondaryButtonText).Returns("Cancel");
            wizard.SetupGet(w => w.IsPrimaryButtonVisible).Returns(true);
            wizard.SetupGet(w => w.IsSecondaryButtonVisible).Returns(true);
            wizard.SetupGet(w => w.PrimaryCommand).Returns(new Mock<ICommand>().Object);
            wizard.SetupGet(w => w.SecondaryCommand).Returns(secondaryCommand.Object);

            var vm = new DialogHostViewModel();
            vm.Content = wizard.Object;

            vm.CancelFromKeyboard();

            secondaryCommand.Verify(command => command.Execute(null), Times.Once);
        }
    }
}
