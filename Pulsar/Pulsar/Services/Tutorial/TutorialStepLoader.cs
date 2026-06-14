// [Path]: Pulsar/Pulsar/Services/Tutorial/TutorialStepLoader.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    /// <summary>
    /// 教程步骤配置加载器
    /// 负责从 JSON 文件加载教程步骤配置
    /// </summary>
    public class TutorialStepLoader
    {
        private readonly ILogger<TutorialStepLoader> _logger;
        private readonly ILocalizationService _loc;
        private readonly TutorialScenarioRegistry _scenarioRegistry;
        private readonly string _defaultConfigPath;

        public TutorialStepLoader(
            ILogger<TutorialStepLoader> logger,
            ILocalizationService localizationService,
            TutorialScenarioRegistry? scenarioRegistry = null)
        {
            _logger = logger;
            _loc = localizationService;
            _scenarioRegistry = scenarioRegistry ?? new TutorialScenarioRegistry();
            
            // 默认配置文件路径 - 优先使用 Assets 目录
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _defaultConfigPath = Path.Combine(appDirectory, "Assets", "TutorialSteps.json");
        }

        /// <summary>
        /// 从默认路径加载教程步骤
        /// </summary>
        public List<TutorialStep> LoadSteps()
        {
            return LoadSteps(_defaultConfigPath);
        }

        /// <summary>
        /// 根据 scenario ID 加载教程步骤
        /// </summary>
        public List<TutorialStep> LoadStepsForScenario(string? scenarioId)
        {
            if (!string.IsNullOrEmpty(scenarioId))
            {
                var scenario = _scenarioRegistry.GetById(scenarioId);
                if (scenario != null && !string.IsNullOrEmpty(scenario.StepsJsonPath))
                {
                    var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    var scenarioPath = Path.Combine(appDirectory, "Assets", scenario.StepsJsonPath);
                    return LoadSteps(scenarioPath);
                }

                _logger.LogWarning("[TutorialStepLoader] Scenario '{ScenarioId}' not found or has no custom steps, falling back to default", scenarioId);
            }

            return LoadSteps(_defaultConfigPath);
        }

        /// <summary>
        /// 从指定路径加载教程步骤
        /// </summary>
        public List<TutorialStep> LoadSteps(string configPath)
        {
            try
            {
                _logger.LogInformation("[TutorialStepLoader] === Starting tutorial steps loading ===");
                _logger.LogInformation("[TutorialStepLoader] Requested config path: {Path}", configPath ?? "null");
                
                // 尝试多个可能的路径
                var filePath = GetStepsFilePath(configPath);
                
                _logger.LogInformation("[TutorialStepLoader] Resolved file path: {Path}", filePath);
                _logger.LogInformation("[TutorialStepLoader] File exists: {Exists}", File.Exists(filePath));
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("[TutorialStepLoader] Steps file not found: {Path}", filePath);
                    _logger.LogWarning("[TutorialStepLoader] Falling back to hardcoded 3-step tutorial");
                    return GetFallbackSteps();
                }

                _logger.LogInformation("[TutorialStepLoader] Loading tutorial steps from: {Path}", filePath);

                var json = File.ReadAllText(filePath);
                 _logger.LogInformation("[TutorialStepLoader] JSON file read successfully, length: {Length} characters", json.Length);
                 // Avoid logging raw tutorial content to prevent noisy logs.
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                 _logger.LogInformation("[TutorialStepLoader] Attempting to deserialize JSON...");
                var config = JsonSerializer.Deserialize<TutorialConfig>(json, options);

                if (config?.Steps == null || config.Steps.Count == 0)
                {
                    _logger.LogError("[TutorialStepLoader] Failed to deserialize tutorial config - config is null or has no steps");
                    _logger.LogError("[TutorialStepLoader] Config null: {IsNull}, Steps null: {StepsNull}, Steps count: {Count}", 
                        config == null, config?.Steps == null, config?.Steps?.Count ?? 0);
                    _logger.LogWarning("[TutorialStepLoader] Falling back to hardcoded 3-step tutorial");
                    return GetFallbackSteps();
                }

                _logger.LogInformation("[TutorialStepLoader] Deserialization successful, found {Count} steps", config.Steps.Count);

                // 验证配置
                _logger.LogInformation("[TutorialStepLoader] Validating configuration...");
                ValidateConfig(config);

                // 对没有显式设置 *Key 的步骤应用约定映射
                ApplyConventionKeys(config.Steps);

                _logger.LogInformation("[TutorialStepLoader] ✅ Successfully loaded {Count} tutorial steps from {Path}", 
                    config.Steps.Count, filePath);
                
                // 输出步骤 ID 列表
                var stepIds = string.Join(", ", config.Steps.Select(s => s.Id));
                _logger.LogInformation("[TutorialStepLoader] Step IDs: {StepIds}", stepIds);

                return config.Steps;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[TutorialStepLoader] ❌ Failed to parse tutorial config JSON");
                _logger.LogError("[TutorialStepLoader] JSON parsing error at line {Line}, position {Position}", 
                    ex.LineNumber, ex.BytePositionInLine);
                _logger.LogWarning("[TutorialStepLoader] Falling back to hardcoded 3-step tutorial");
                return GetFallbackSteps();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialStepLoader] ❌ Error loading tutorial steps");
                _logger.LogWarning("[TutorialStepLoader] Falling back to hardcoded 3-step tutorial");
                return GetFallbackSteps();
            }
        }

        /// <summary>
        /// 获取步骤文件路径 - 优先加载语言特定文件
        /// </summary>
        private string GetStepsFilePath(string? preferredPath = null)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var lang = _loc.CurrentLanguage;

            _logger.LogDebug("[TutorialStepLoader] Searching for tutorial steps file (lang: {Lang})...", lang);

            // 优先加载语言特定文件 (e.g. TutorialSteps.en.json, TutorialSteps.zh-CN.json)
            // 然后回退到通用文件名
            var fileNames = new[]
            {
                $"TutorialSteps.{lang}.json",
                "TutorialSteps.json"
            };

            var searchDirs = new[]
            {
                preferredPath,
                Path.Combine(basePath, "Assets"),
                basePath,
                Directory.GetCurrentDirectory(),
                Path.Combine(basePath, "Resources", "Tutorial")
            };

            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                // 如果 preferredPath 是完整文件路径，直接检查
                if (dir == preferredPath && File.Exists(dir))
                {
                    var ext = Path.GetExtension(dir);
                    if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("[TutorialStepLoader] Using preferred path: {Path}", dir);
                        return dir;
                    }
                }

                foreach (var fileName in fileNames)
                {
                    var path = Path.Combine(dir, fileName);
                    if (File.Exists(path))
                    {
                        _logger.LogInformation("[TutorialStepLoader] Found steps file: {Path}", path);
                        return path;
                    }
                }
            }

            var defaultPath = Path.Combine(basePath, "Assets", "TutorialSteps.json");
            _logger.LogWarning("[TutorialStepLoader] No tutorial steps file found, returning default: {Path}", defaultPath);
            return defaultPath;
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        private void ValidateConfig(TutorialConfig config)
        {
            if (config.Steps.Count == 0)
            {
                throw new InvalidOperationException("Tutorial config must contain at least one step");
            }
            
            // 验证步骤 ID 唯一性
            var ids = new HashSet<string>();
            foreach (var step in config.Steps)
            {
                if (string.IsNullOrEmpty(step.Id))
                {
                    throw new InvalidOperationException("Tutorial step must have an ID");
                }
                
                if (!ids.Add(step.Id))
                {
                    throw new InvalidOperationException($"Duplicate step ID: {step.Id}");
                }
            }
            
            _logger.LogDebug("[TutorialStepLoader] Config validation passed");
        }

        /// <summary>
        /// 对没有显式设置 *Key 的步骤应用约定映射
        /// 规则：从 step ID 提取最后一个有意义的片段，PascalCase 后拼接成 "Tutorial.{Suffix}"
        /// 例如：step1_onboarding_welcome → Tutorial.Welcome
        /// </summary>
        private static void ApplyConventionKeys(List<TutorialStep> steps)
        {
            foreach (var step in steps)
            {
                if (string.IsNullOrEmpty(step.TitleKey))
                {
                    var titleSuffix = DeriveLocSuffix(step.Id);
                    if (titleSuffix != null)
                    {
                        step.TitleKey = "Tutorial." + titleSuffix;
                    }
                }

                if (string.IsNullOrEmpty(step.DescriptionKey))
                {
                    var descSuffix = DeriveLocSuffix(step.Id);
                    if (descSuffix != null)
                    {
                        step.DescriptionKey = "Tutorial." + descSuffix + "Desc";
                    }
                }
            }
        }

        /// <summary>
        /// 从步骤 ID 推导本地化键后缀
        /// 格式：去掉 stepN_ 前缀后，取最后一段下划线分隔的单词
        /// </summary>
        private static string? DeriveLocSuffix(string stepId)
        {
            // 去掉 stepN_ 前缀
            var match = System.Text.RegularExpressions.Regex.Match(stepId, @"^step\d+_(.+)$");
            if (!match.Success) return null;

            var remainder = match.Groups[1].Value;

            // 按 _ 分割，取最后一段
            var parts = remainder.Split('_');
            var lastSegment = parts.Length > 0 ? parts[^1] : remainder;
            if (string.IsNullOrEmpty(lastSegment)) return null;

            // PascalCase: 首字母大写
            return char.ToUpper(lastSegment[0]) + lastSegment[1..];
        }

        /// <summary>
        /// 获取降级步骤（硬编码）
        /// 当 JSON 配置加载失败时使用
        /// </summary>
        private List<TutorialStep> GetFallbackSteps()
        {
            _logger.LogWarning("[TutorialStepLoader] Using fallback hardcoded tutorial steps");

            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Id = "step1_onboarding_welcome",
                    Title = _loc["Tutorial.Welcome"],
                    Description = _loc["Tutorial.WelcomeDesc"],
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysFocused,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ButtonClick
                    }
                },
                new TutorialStep
                {
                    Id = "step2_switch_mode_intro",
                    Title = _loc["Tutorial.SwitchMode"],
                    Description = _loc["Tutorial.SwitchModeDesc"],
                    Type = TutorialStepType.WaitForAction,
                    WaitHintText = _loc["Tutorial.SwitchModeHint"],
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.RadialMenuShown,
                        TargetValue = "Task"
                    }
                },
                new TutorialStep
                {
                    Id = "step3_switch_mode_success",
                    Title = _loc["Tutorial.FirstSwitch"],
                    Description = _loc["Tutorial.FirstSwitchDesc"],
                    Type = TutorialStepType.WaitForAction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ActionExecuted,
                        TargetValue = "Switch"
                    }
                },
                new TutorialStep
                {
                    Id = "step4_command_mode_intro",
                    Title = _loc["Tutorial.CommandMode"],
                    Description = _loc["Tutorial.CommandModeDesc"],
                    Type = TutorialStepType.WaitForAction,
                    WaitHintText = _loc["Tutorial.CommandModeHint"],
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.RadialMenuShown,
                        TargetValue = "Action"
                    }
                },
                new TutorialStep
                {
                    Id = "step5_command_mode_success",
                    Title = _loc["Tutorial.FirstCommand"],
                    Description = _loc["Tutorial.FirstCommandDesc"],
                    Type = TutorialStepType.WaitForAction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ActionExecuted,
                        TargetValue = "Command"
                    }
                },
                new TutorialStep
                {
                    Id = "step6_completion",
                    Title = _loc["Tutorial.OnboardingComplete"],
                    Description = _loc["Tutorial.OnboardingCompleteDesc"],
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    PrimaryAction = TutorialPrimaryAction.CompleteTutorial,
                    PrimaryButtonText = _loc["Tutorial.Finish"],
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ButtonClick
                    }
                }
            };
        }

        /// <summary>
        /// 教程配置根对象
        /// </summary>
        private class TutorialConfig
        {
            [JsonPropertyName("version")]
            public string Version { get; set; } = "1.0.0";

            [JsonPropertyName("steps")]
            public List<TutorialStep> Steps { get; set; } = new();
        }
    }
}
