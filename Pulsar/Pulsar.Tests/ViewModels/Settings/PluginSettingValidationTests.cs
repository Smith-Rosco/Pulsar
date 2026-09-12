using System;
using System.Linq;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels.Settings
{
    /// <summary>
    /// 文本型设置校验骨架（PluginSettingViewModel.ValidateText）的钉子。
    /// String / Secret 共用同一组键、Path 用 Path 组键且不查 Pattern ——
    /// 这些是类型间的真实分面差异；骨架结构本身必须只有一个 owner，
    /// 不允许某一天某一份 Validate 又悄悄长出自己的 required/length/pattern 阶梯。
    /// </summary>
    public class PluginSettingValidationTests
    {
        private static ILocalizationService CreateLoc()
        {
            var mock = new Mock<ILocalizationService>();
            // 键即消息：断言落到键名本身，绕开测试宿主 culture（zh-CN）差异。
            mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => k);
            return mock.Object;
        }

        private static PluginSettingViewModel CreateTextVm(PluginSettingType type, object? value)
        {
            var def = new PluginSettingDefinition { Key = "k", Label = "k", Type = type };
            return PluginSettingViewModel.Create(def, value, CreateLoc());
        }

        private static PluginSettingViewModel WithDef(PluginSettingViewModel vm, Action<PluginSettingDefinition> configure)
        {
            configure(vm.Definition);
            return vm;
        }

        [Theory]
        [InlineData(PluginSettingType.String)]
        [InlineData(PluginSettingType.Secret)]
        public void TextSkeleton_StringAndSecretShareSameKeyGroup(PluginSettingType type)
        {
            var required = WithDef(CreateTextVm(type, ""), d => d.IsRequired = true);
            required.Validate();
            required.ValidationMessage.Should().Be("Validation.Required");

            var tooShort = WithDef(CreateTextVm(type, "ab"), d => d.MinLength = 3);
            tooShort.Validate();
            tooShort.ValidationMessage.Should().Be("Validation.MinLengthFormat");

            var tooLong = WithDef(CreateTextVm(type, "abcd"), d => d.MaxLength = 3);
            tooLong.Validate();
            tooLong.ValidationMessage.Should().Be("Validation.MaxLengthFormat");

            var mismatch = WithDef(CreateTextVm(type, "abc"), d => d.Pattern = "[0-9]+");
            mismatch.Validate();
            mismatch.ValidationMessage.Should().Be("Validation.FormatMismatch");
        }

        [Fact]
        public void PathSkeleton_UsesPathKeys_AndIgnoresPattern()
        {
            var required = WithDef(CreateTextVm(PluginSettingType.Path, ""), d => d.IsRequired = true);
            required.Validate();
            required.ValidationMessage.Should().Be("Validation.PathRequired");

            var tooShort = WithDef(CreateTextVm(PluginSettingType.Path, "ab"), d => d.MinLength = 3);
            tooShort.Validate();
            tooShort.ValidationMessage.Should().Be("Validation.PathMinLengthFormat");

            var tooLong = WithDef(CreateTextVm(PluginSettingType.Path, "abcd"), d => d.MaxLength = 3);
            tooLong.Validate();
            tooLong.ValidationMessage.Should().Be("Validation.PathMaxLengthFormat");

            // Path 不查 Pattern：即使声明了违规 pattern，值匹配与否都不产生消息。
            var patternIgnored = WithDef(CreateTextVm(PluginSettingType.Path, "abc"), d => d.Pattern = "[0-9]+");
            patternIgnored.Validate();
            patternIgnored.ValidationMessage.Should().BeEmpty();
        }

        [Fact]
        public void Skeleton_Order_RequiredBeatsLengthBeatsPattern()
        {
            var empty = WithDef(CreateTextVm(PluginSettingType.String, ""), d =>
            {
                d.IsRequired = true;
                d.MinLength = 3;
                d.Pattern = "[0-9]+";
            });
            empty.Validate();
            empty.ValidationMessage.Should().Be("Validation.Required");

            var shortAndMismatch = WithDef(CreateTextVm(PluginSettingType.String, "ab"), d =>
            {
                d.IsRequired = true;
                d.MinLength = 3;
                d.Pattern = "[0-9]+";
            });
            shortAndMismatch.Validate();
            shortAndMismatch.ValidationMessage.Should().Be("Validation.MinLengthFormat");
        }

        [Fact]
        public void InvalidRegex_IsTreatedAsNoConstraint()
        {
            var vm = WithDef(CreateTextVm(PluginSettingType.String, "anything"), d => d.Pattern = "[");
            Action act = () => vm.Validate();
            act.Should().NotThrow();
            vm.ValidationMessage.Should().BeEmpty();
        }

        [Fact]
        public void ValueChange_Revalidates_AndResetToDefaultClearsMessage()
        {
            var def = new PluginSettingDefinition
            {
                Key = "k",
                Label = "k",
                Type = PluginSettingType.String,
                IsRequired = true,
                MinLength = 3,
                DefaultValue = "abcdef"
            };
            var vm = (StringSettingViewModel)PluginSettingViewModel.Create(def, "ab", CreateLoc());
            vm.Validate();
            vm.IsValid.Should().BeFalse();
            vm.HasValidation.Should().BeTrue();

            vm.ResetToDefault();
            vm.IsValid.Should().BeTrue();
            vm.HasValidation.Should().BeFalse();
        }

        [Fact]
        public void TextValue_FallsBackToDefinitionDefault()
        {
            var def = new PluginSettingDefinition
            {
                Key = "k",
                Label = "k",
                Type = PluginSettingType.String,
                DefaultValue = "fallback"
            };
            var vm = new StringSettingViewModel(def, null, CreateLoc());
            vm.StringValue.Should().Be("fallback");
        }
    }
}
