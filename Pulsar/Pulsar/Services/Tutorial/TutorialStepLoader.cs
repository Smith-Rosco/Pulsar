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
                // Step 1: Welcome
                new TutorialStep
                {
                    Id = "step1_welcome",
                    Title = "欢迎使用 Pulsar",
                    Description = "Pulsar 是一个基于肌肉记忆的快速启动器\n让我们用 30 秒了解核心功能\n\n核心特性：\n• 全局热键触发，无需鼠标\n• 两种模式：切换窗口 & 执行命令\n• 空间定位，盲操作友好",
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysFocused,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.Center,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 500,
                        FixedCardHeight = 400
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ButtonClick
                    }
                },

                // Step 2: Open Settings
                new TutorialStep
                {
                    Id = "step2_open_settings",
                    Title = "打开设置界面",
                    Description = "请左键单击任务栏托盘中的 Pulsar 图标\n（或右键选择\"设置\"）\n\n💡 提示：托盘图标通常在屏幕右下角",
                    Type = TutorialStepType.WaitForAction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Target = new TutorialTarget
                    {
                        Type = TutorialTargetType.TrayIcon
                    },
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.BottomRight,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.WindowOpened,
                        TargetValue = "SettingsWindow"
                    }
                },

                // Step 3: Settings Overview
                new TutorialStep
                {
                    Id = "step3_settings_overview",
                    Title = "设置界面导览",
                    Description = "这里可以配置 Pulsar 的所有功能：\n\n• 常规 - 热键、主题、启动项\n• 槽位配置 - 为不同应用配置快捷操作\n• 插件 - 管理功能扩展\n\n每个 Profile 可配置不同的槽位：\n• 通过顶部下拉框切换 Profile\n• 每个 Profile 的槽位独立配置",
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysObserving,
                    Target = new TutorialTarget
                    {
                        Type = TutorialTargetType.Window,
                        ElementName = "SettingsWindow"
                    },
                    Layout = new TutorialLayout
                    {
                        TargetWindow = new WindowLayout
                        {
                            Left = 0.25,
                            Top = 0.15,
                            Width = 0.5,
                            Height = 0.7,
                            IsRelative = true
                        },
                        CardPosition = CardPosition.TopRight,
                        CardSizeMode = CardSizeMode.Fixed,
                        FixedCardWidth = 450,
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
