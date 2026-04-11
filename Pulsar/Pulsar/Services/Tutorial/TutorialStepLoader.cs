// [Path]: Pulsar/Pulsar/Services/Tutorial/TutorialStepLoader.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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
        private readonly string _defaultConfigPath;

        public TutorialStepLoader(ILogger<TutorialStepLoader> logger)
        {
            _logger = logger;
            
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
        /// 获取步骤文件路径 - 尝试多个可能的位置
        /// </summary>
        private string GetStepsFilePath(string? preferredPath = null)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            _logger.LogDebug("[TutorialStepLoader] Searching for tutorial steps file...");
            _logger.LogDebug("[TutorialStepLoader] Base path: {BasePath}", basePath);
            _logger.LogDebug("[TutorialStepLoader] Preferred path: {PreferredPath}", preferredPath ?? "null");
            
            var paths = new[]
            {
                preferredPath,
                Path.Combine(basePath, "Assets", "TutorialSteps.json"),
                Path.Combine(basePath, "TutorialSteps.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "TutorialSteps.json"),
                Path.Combine(basePath, "Resources", "Tutorial", "Steps.json")
            };
            
            _logger.LogDebug("[TutorialStepLoader] Checking {Count} possible paths:", paths.Length);
            
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (!string.IsNullOrEmpty(path))
                {
                    var exists = File.Exists(path);
                    _logger.LogDebug("[TutorialStepLoader]   [{Index}] {Status} {Path}", 
                        i + 1, 
                        exists ? "✓ FOUND" : "✗ Not found", 
                        path);
                    
                    if (exists)
                    {
                        _logger.LogInformation("[TutorialStepLoader] Selected file: {Path}", path);
                        return path;
                    }
                }
                else
                {
                    _logger.LogDebug("[TutorialStepLoader]   [{Index}] ✗ Path is null/empty", i + 1);
                }
            }
            
            // 返回默认路径（即使不存在）
            var defaultPath = Path.Combine(basePath, "Assets", "TutorialSteps.json");
            _logger.LogWarning("[TutorialStepLoader] No tutorial steps file found, returning default path: {Path}", defaultPath);
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
                    Title = "Welcome to Pulsar",
                    Description = "Your setup wizard already created a starter configuration. This tutorial now focuses on one successful Switch Mode action and one successful Command Mode action.",
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysFocused,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 450,
                        FixedCardHeight = 320
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ButtonClick
                    }
                },
                new TutorialStep
                {
                    Id = "step2_switch_mode_intro",
                    Title = "Switch Mode: app switching and launching",
                    Description = "Press Ctrl+Q to open Switch Mode and preview the app slots created during setup.",
                    Type = TutorialStepType.WaitForAction,
                    WaitHintText = "Open Switch Mode with Ctrl+Q.",
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 450,
                        FixedCardHeight = 320
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
                    Title = "First switch success",
                    Description = "Choose any generated app slot from Switch Mode. This step completes only after Pulsar reports one successful switch or launch action.",
                    Type = TutorialStepType.WaitForAction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 450,
                        FixedCardHeight = 320
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
                    Title = "Command Mode: run an action",
                    Description = "Press Ctrl+Shift+Q to open Command Mode and preview the starter command slot created during setup.",
                    Type = TutorialStepType.WaitForAction,
                    WaitHintText = "Open Command Mode with Ctrl+Shift+Q.",
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 460,
                        FixedCardHeight = 300
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
                    Title = "First command success",
                    Description = "Choose any generated command slot from Command Mode. This step completes only after Pulsar reports one successful command execution.",
                    Type = TutorialStepType.WaitForAction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 480,
                        FixedCardHeight = 320
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
                    Title = "Onboarding complete",
                    Description = "You completed the minimum onboarding path: one successful Switch Mode action and one successful Command Mode action.",
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    PrimaryAction = TutorialPrimaryAction.CompleteTutorial,
                    PrimaryButtonText = "Finish",
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 480,
                        FixedCardHeight = 300
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
