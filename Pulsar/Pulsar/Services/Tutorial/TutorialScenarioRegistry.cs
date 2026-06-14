using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public sealed class TutorialScenarioRegistry
    {
        private readonly Dictionary<string, TutorialScenario> _scenarios;

        public TutorialScenarioRegistry()
        {
            _scenarios = new Dictionary<string, TutorialScenario>(StringComparer.OrdinalIgnoreCase)
            {
                ["notepad"] = new TutorialScenario
                {
                    Id = "notepad",
                    TitleKey = "Scenario.Notepad.Title",
                    DescriptionKey = "Scenario.Notepad.Description",
                    SlotDescriptionKey = "Scenario.Notepad.SlotDescription",
                    RequiredAppIds = new List<string> { "notepad" },
                    CommandSlotTemplates = new List<CommandSlotTemplate>
                    {
                        new()
                        {
                            PluginId = "com.pulsar.command",
                            Action = "sendkeys",
                            Args = new Dictionary<string, string>
                            {
                                ["keys"] = "Hello from Pulsar!{ENTER}"
                            },
                            LabelKey = "CommandSlot.InsertSampleText",
                            IconKey = "\uE756",
                            IsTutorialPrimary = true
                        }
                    },
                    StepsJsonPath = null
                },
                ["excel"] = new TutorialScenario
                {
                    Id = "excel",
                    TitleKey = "Scenario.Excel.Title",
                    DescriptionKey = "Scenario.Excel.Description",
                    SlotDescriptionKey = "Scenario.Excel.SlotDescription",
                    RequiredAppIds = new List<string> { "excel" },
                    CommandSlotTemplates = new List<CommandSlotTemplate>
                    {
                        new()
                        {
                            PluginId = "com.pulsar.vbarunner",
                            Action = "run",
                            Args = new Dictionary<string, string>
                            {
                                ["scriptPath"] = "Assets/Scripts/Demo/excel_demo.txt",
                                ["macro"] = "PulsarDemo"
                            },
                            LabelKey = "CommandSlot.RunVbaDemo",
                            IconKey = "\uE756",
                            IsTutorialPrimary = true
                        },
                        new()
                        {
                            PluginId = "com.pulsar.command",
                            Action = "sendkeys",
                            Args = new Dictionary<string, string>
                            {
                                ["keys"] = "Hello from Pulsar!{ENTER}"
                            },
                            LabelKey = "CommandSlot.InsertSampleText",
                            IconKey = "\uE756",
                            IsTutorialPrimary = false
                        }
                    },
                    PrerequisiteProvider = typeof(Prerequisites.ExcelPrerequisiteProvider),
                    StepsJsonPath = "TutorialSteps.excel.json"
                },
                ["browser"] = new TutorialScenario
                {
                    Id = "browser",
                    TitleKey = "Scenario.Browser.Title",
                    DescriptionKey = "Scenario.Browser.Description",
                    SlotDescriptionKey = "Scenario.Browser.SlotDescription",
                    RequiredAppIds = new List<string> { "chrome", "edge" },
                    CommandSlotTemplates = new List<CommandSlotTemplate>
                    {
                        new()
                        {
                            PluginId = "com.pulsar.bookmarklet",
                            Action = "run",
                            Args = new Dictionary<string, string>
                            {
                                ["scriptPath"] = "Assets/Scripts/Demo/browser_demo.js"
                            },
                            LabelKey = "CommandSlot.OpenBookmarklet",
                            IconKey = "\uE774",
                            IsTutorialPrimary = true
                        }
                    },
                    PrerequisiteProvider = typeof(Prerequisites.BrowserPrerequisiteProvider),
                    StepsJsonPath = "TutorialSteps.browser.json"
                }
            };
        }

        public IReadOnlyList<TutorialScenario> All => _scenarios.Values.ToList();

        public TutorialScenario Default => _scenarios.Values.First();

        public TutorialScenario? GetById(string id)
        {
            return _scenarios.TryGetValue(id, out var scenario) ? scenario : null;
        }
    }
}
