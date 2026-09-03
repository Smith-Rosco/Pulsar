using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Features.Presets.Models;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services.Prerequisites;

namespace Pulsar.Features.Presets.Services
{
    /// <summary>
    /// Static first-party preset-pack catalog (mirrors <c>TutorialScenarioRegistry</c>).
    /// Pack payload files ship under <c>Assets/Presets/&lt;pack-id&gt;/</c> and are copied to
    /// the output Assets folder by the project file.
    /// </summary>
    public sealed class PresetCatalogService : IPresetCatalogService
    {
        private readonly Dictionary<string, PresetPack> _packs;

        public PresetCatalogService()
        {
            _packs = new Dictionary<string, PresetPack>(StringComparer.OrdinalIgnoreCase)
            {
                ["macro"] = CreateMacroPack(),
                ["form-fill"] = CreateFormFillPack(),
                ["sign-in"] = CreateSignInPack()
            };
        }

        public IReadOnlyList<PresetPack> All => _packs.Values.ToList();

        public PresetPack? GetById(string id)
        {
            return _packs.TryGetValue(id, out var pack) ? pack : null;
        }

        private static PresetPack CreateMacroPack()
        {
            return new PresetPack
            {
                Id = "macro",
                Version = "1.0.0",
                TitleKey = "Preset.Pack.Macro.Title",
                DescriptionKey = "Preset.Pack.Macro.Description",
                SlotDescriptionKey = "Preset.Pack.Macro.SlotDescription",
                PayloadDirectory = "Assets/Presets/macro",
                PrerequisiteProvider = typeof(ExcelPrerequisiteProvider),
                CommandSlotTemplates = new List<CommandSlotTemplate>
                {
                    new()
                    {
                        PluginId = "com.pulsar.vbarunner",
                        Action = "run",
                        Args = new Dictionary<string, string>
                        {
                            ["scriptPath"] = "Assets/Presets/macro/excel_macro.txt",
                            ["macro"] = "PulsarDemo"
                        },
                        LabelKey = "CommandSlot.RunVbaDemo",
                        IconKey = "\uE736",
                        IsTutorialPrimary = true
                    }
                }
            };
        }

        private static PresetPack CreateFormFillPack()
        {
            return new PresetPack
            {
                Id = "form-fill",
                Version = "1.0.0",
                TitleKey = "Preset.Pack.FormFill.Title",
                DescriptionKey = "Preset.Pack.FormFill.Description",
                SlotDescriptionKey = "Preset.Pack.FormFill.SlotDescription",
                PayloadDirectory = "Assets/Presets/form-fill",
                PrerequisiteProvider = typeof(BrowserPrerequisiteProvider),
                CommandSlotTemplates = new List<CommandSlotTemplate>
                {
                    new()
                    {
                        PluginId = "com.pulsar.bookmarklet",
                        Action = "run",
                        Args = new Dictionary<string, string>
                        {
                            ["scriptPath"] = "Assets/Presets/form-fill/form_fill.js"
                        },
                        LabelKey = "CommandSlot.RunFormFillDemo",
                        IconKey = "\uE774",
                        IsTutorialPrimary = true
                    }
                }
            };
        }

        private static PresetPack CreateSignInPack()
        {
            return new PresetPack
            {
                Id = "sign-in",
                Version = "1.0.0",
                TitleKey = "Preset.Pack.SignIn.Title",
                DescriptionKey = "Preset.Pack.SignIn.Description",
                SlotDescriptionKey = "Preset.Pack.SignIn.SlotDescription",
                PayloadDirectory = "Assets/Presets/sign-in",
                PrerequisiteProvider = typeof(BrowserPrerequisiteProvider),
                CommandSlotTemplates = new List<CommandSlotTemplate>
                {
                    new()
                    {
                        PluginId = "com.pulsar.bookmarklet",
                        Action = "run",
                        Args = new Dictionary<string, string>
                        {
                            ["scriptPath"] = "Assets/Presets/sign-in/sign_in.js"
                        },
                        LabelKey = "CommandSlot.RunSignInDemo",
                        IconKey = "\uE774",
                        IsTutorialPrimary = true
                    }
                }
            };
        }
    }
}
