// [Path]: Pulsar/Pulsar.Tests/Tutorial/TutorialStepLocalizationGuardTests.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    /// <summary>
    /// 教程步骤文本的单一来源守卫（架构审查候选 #5）。
    ///
    /// 背景：5 个 <c>TutorialSteps*.json</c> 曾同时写散文（<c>title</c> / <c>description</c> /
    /// <c>waitHintText</c> / <c>primaryButtonText</c>）与本地化键（<c>*Key</c>），而
    /// <c>TutorialStepCard.RefreshStepContent</c> 运行时恒取键 —— 缺键时
    /// <c>ILocalizationService</c> 返回键名本身，从不回退到散文。那 75 处散文因此是与
    /// resx 值逐字重复、永远读不到的死副本，已删除。删除后由本守卫钉死三件事：
    /// ① 散文不得回潮；② 每个步骤必须声明键；③ 声明的键必须在两个 resx 里都存在。
    /// </summary>
    public class TutorialStepLocalizationGuardTests
    {
        private static readonly string[] StepFileNames =
        {
            "TutorialSteps.json",
            "TutorialSteps.browser.json",
            "TutorialSteps.excel.json",
            "TutorialSteps.notepad.json",
            "TutorialSteps.webscript.json",
        };

        /// <summary>与 <c>*Key</c> 成对的散文字段 —— 任一出现即为回潮。</summary>
        private static readonly string[] ProseFields =
        {
            "title", "description", "waitHintText", "primaryButtonText",
        };

        private static string ResolveStepFile(string fileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var inAssets = Path.Combine(baseDir, "Assets", fileName);
            if (File.Exists(inAssets)) return inAssets;

            for (var dir = new DirectoryInfo(baseDir); dir != null; dir = dir.Parent)
            {
                var inSource = Path.Combine(dir.FullName, "Pulsar", "Resources", "Tutorial", fileName);
                if (File.Exists(inSource)) return inSource;
            }

            // 返回最可能的路径，让「文件必须存在」以断言形式失败，而不是 NullReference。
            return inAssets;
        }

        private static List<(string File, JsonObject Step)> LoadAllSteps()
        {
            var result = new List<(string, JsonObject)>();

            foreach (var name in StepFileNames)
            {
                var path = ResolveStepFile(name);
                File.Exists(path).Should().BeTrue($"tutorial steps file '{name}' must be resolvable (looked at '{path}')");

                var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                root.Should().NotBeNull($"'{name}' must deserialize to an object");

                if (root!["steps"] is JsonArray steps)
                {
                    foreach (var step in steps)
                    {
                        if (step is JsonObject stepObject)
                        {
                            result.Add((name, stepObject));
                        }
                    }
                }
            }

            return result;
        }

        [Fact]
        public void Steps_ShouldBeFound()
        {
            LoadAllSteps().Should().NotBeEmpty("the guard is worthless if it cannot see the step files");
        }

        [Fact]
        public void Steps_ShouldNotCarryProseCopies()
        {
            var offenders = new List<string>();

            foreach (var (file, step) in LoadAllSteps())
            {
                var id = step["id"]?.GetValue<string>() ?? "<no id>";

                foreach (var property in step.AsObject())
                {
                    if (ProseFields.Any(field => string.Equals(field, property.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        offenders.Add($"{file}/{id}:{property.Key}");
                    }
                }
            }

            offenders.Should().BeEmpty(
                "tutorial prose lives in resx only — a JSON prose field is a dead copy that silently " +
                "drifts from the localized text (TutorialStepCard resolves *Key and never falls back to prose)");
        }

        [Fact]
        public void EveryStep_ShouldDeclareTitleAndDescriptionKeys()
        {
            var missing = new List<string>();

            foreach (var (file, step) in LoadAllSteps())
            {
                var id = step["id"]?.GetValue<string>() ?? "<no id>";

                if (string.IsNullOrWhiteSpace(step["titleKey"]?.GetValue<string>()))
                {
                    missing.Add($"{file}/{id}:titleKey");
                }

                if (string.IsNullOrWhiteSpace(step["descriptionKey"]?.GetValue<string>()))
                {
                    missing.Add($"{file}/{id}:descriptionKey");
                }
            }

            missing.Should().BeEmpty(
                "without a key the card has nothing to resolve — there is no prose fallback to render anymore");
        }

        [Fact]
        public void EveryLocalizationKey_ShouldExistInBothResx()
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);
            var keys = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var (_, step) in LoadAllSteps())
            {
                foreach (var property in step.AsObject())
                {
                    if (!property.Key.EndsWith("Key", StringComparison.Ordinal)) continue;

                    var value = property.Value?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        keys.Add(value!);
                    }
                }
            }

            keys.Should().NotBeEmpty("the guard must actually observe the step keys");

            var missing = new List<string>();
            foreach (var culture in new[] { "en", "zh-CN" })
            {
                var info = CultureInfo.GetCultureInfo(culture);
                foreach (var key in keys)
                {
                    if (manager.GetString(key, info) == null)
                    {
                        missing.Add($"{culture}:{key}");
                    }
                }
            }

            missing.Should().BeEmpty(
                "an unresolved tutorial key renders as the raw key itself, and the prose fallback it used " +
                "to hide behind no longer exists");
        }
    }
}
