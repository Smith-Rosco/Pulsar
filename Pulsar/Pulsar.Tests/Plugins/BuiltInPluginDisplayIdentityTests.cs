using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Localization;
using Pulsar.Plugins.Extensions.BookmarkletRunner;
using Pulsar.Plugins.Extensions.VbaRunner;
using Xunit;

namespace Pulsar.Tests.Plugins
{
    /// <summary>
    /// plugin-display-identity 叙事对齐回归（openspec 2026-09-05-repositioning-narrative-rollout）。
    /// 覆盖三个不变量：
    /// 1. 内置插件显示名必须能经 PluginLocalization 约定（Plugin.Name.{AlphaNumOnly(DisplayName)}）
    ///    命中 resx 键——若有人改 C# DisplayName 而不同步 resx，键推导断裂、UI 回退英文，本测试立即失败；
    /// 2. 内置插件描述在 zh-CN 下必须是叙事口径文本（三支柱：自动化领衔 / 凭据护城河 / 窗口打底）；
    /// 3. BookmarkletRunner / VbaRunner 的插件 Id 与显示名保持不变（本次只动描述层，配置不受影响），
    ///    且 C# 英文描述与 EN resx 保持同步（en 语言下经本地化管道输出与 C# 原文一致）。
    /// </summary>
    public class BuiltInPluginDisplayIdentityTests
    {
        private static LocalizationService CreateLocalization(string language)
        {
            var loc = new LocalizationService(NullLogger<LocalizationService>.Instance);
            loc.SetLanguage(language);
            return loc;
        }

        public static TheoryData<string, string> BuiltInDisplayNames => new()
        {
            // (英文显示名, zh-CN 叙事名)——键由显示名推导，改名必须同步 resx
            { "Web Scripts", "网页脚本" },
            { "Excel Macros", "Excel 宏" },
            { "AutoFill", "自动填充" },
            { "App Switch", "应用切换" },
            { "Open & Type", "打开并输入" },
            { "Pulsar Settings", "Pulsar 设置" },
        };

        [Theory]
        [MemberData(nameof(BuiltInDisplayNames))]
        public void DisplayName_ResolvesToChineseNarrativeName_ViaConventionKey(string displayName, string expectedZh)
        {
            var loc = CreateLocalization("zh-CN");

            var localized = PluginLocalization.LocalizePluginName(loc, displayName);

            localized.Should().Be(expectedZh,
                $"显示名 \"{displayName}\" 与 Plugin.Name.* resx 键的约定绑定不可断裂（键由显示名推导）");
        }

        [Theory]
        [InlineData("Web Scripts", "老旧内网")]
        [InlineData("Excel Macros", "一键")]
        [InlineData("AutoFill", "DPAPI")]
        public void Description_IsNarrativeAligned_UnderChinese(string displayName, string expectedKeyword)
        {
            var loc = CreateLocalization("zh-CN");

            var localized = PluginLocalization.LocalizePluginDescription(loc, "any-en-fallback", displayName);

            localized.Should().Contain(expectedKeyword,
                $"\"{displayName}\" 的中文描述必须落在办公自动化叙事口径（三支柱）上");
            localized.Should().NotBe("any-en-fallback", "zh-CN 下不应回退英文原文");
        }

        [Fact]
        public void BookmarkletRunner_Identity_IsStable_DisplayLayerOnly()
        {
            var plugin = new BookmarkletRunnerPlugin();

            plugin.Id.Should().Be("com.pulsar.bookmarklet", "插件 Id 是持久化键，叙事重定位不得改动");
            plugin.DisplayName.Should().Be("Web Scripts", "显示名与 resx 键 WebScripts 绑定，保持稳定");

            var metadata = plugin.GetMetadata();
            metadata.Display.Name.Should().Be(plugin.DisplayName);
            metadata.Display.Description.Should().Be(plugin.Description);
            plugin.Description.Should().Contain("legacy intranet web pages",
                "BookmarkletRunner 描述必须落在「老旧系统网页脚本」叙事方向");
        }

        [Fact]
        public void VbaRunner_Identity_IsStable_DisplayLayerOnly()
        {
            var plugin = new VbaRunnerPlugin();

            plugin.Id.Should().Be("com.pulsar.vbarunner", "插件 Id 是持久化键，叙事重定位不得改动");
            plugin.DisplayName.Should().Be("Excel Macros", "显示名与 resx 键 ExcelMacros 绑定，保持稳定");
            plugin.Description.Should().Contain("one click", "VbaRunner 描述必须落在「一键跑宏」自动化领衔叙事方向");
        }

        [Theory]
        [InlineData("Web Scripts", "Plugin.Description.WebScripts")]
        [InlineData("Excel Macros", "Plugin.Description.ExcelMacros")]
        [InlineData("AutoFill", "Plugin.Description.AutoFill")]
        public void EnglishDescription_StayInSync_BetweenCodeAndResx(string displayName, string resxKey)
        {
            // en 语言下 resx 值即英文 canonical 文案；C# Description 是无 loc 时的回退。
            // 两者必须同步，否则 en 用户看到 resx 文案、异常路径看到 C# 文案，出现两套叙事。
            var loc = CreateLocalization("en");

            var viaPipeline = PluginLocalization.LocalizePluginDescription(loc, "CODE_FALLBACK_SENTINEL", displayName);

            viaPipeline.Should().NotBe("CODE_FALLBACK_SENTINEL", $"{resxKey} 必须命中 EN 资源");
            viaPipeline.Should().Be(loc[resxKey], "EN 资源值即 canonical 英文文案，C# 回退文本必须与其一致");
        }
    }
}
