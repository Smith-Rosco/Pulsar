using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models.Enums;
using Pulsar.ViewModels;
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
    }
}
