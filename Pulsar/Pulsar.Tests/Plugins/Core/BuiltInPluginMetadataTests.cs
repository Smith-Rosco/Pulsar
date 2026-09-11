using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.SecretFill;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SystemCommand;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Plugins.Extensions.BookmarkletRunner;
using Pulsar.Plugins.Extensions.Command;
using Pulsar.Plugins.Extensions.VbaRunner;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Plugins.Core
{
    public class BuiltInPluginMetadataTests
    {
        [Fact]
        public void CommandRunnerMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var keySender = new Mock<IKeySender>();
            var processLauncher = new Mock<IProcessLauncher>();
            var loc = new Mock<ILocalizationService>();
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            var plugin = new CommandPlugin(
                NullLogger<CommandPlugin>.Instance,
                keySender.Object,
                processLauncher.Object,
                loc.Object,
                windowService.Object,
                focusManager.Object);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Open & Type");
            metadata.Display.Description.Should().Contain("Open apps, files, folders, or URLs");
            metadata.Capabilities.SupportedActions.Should().Equal("run", "sendkeys");
            metadata.Actions["run"].Label.Should().Be("Open Target");
        }

        [Fact]
        public void WinSwitcherMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("App Switch");
            metadata.Capabilities.SupportedActions.Should().Equal("switch", "launch", "activate");
            metadata.Actions["switch"].Label.Should().Be("Switch Or Launch");
            metadata.Actions["launch"].Label.Should().Be("Launch App");
            metadata.Actions["activate"].Label.Should().Be("Switch Existing App");
        }

        [Fact]
        public void WinSwitcherMetadata_Parameters_ShouldMatchCanonicalSpec()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();

            var switchAction = metadata.Actions["switch"];
            var launchAction = metadata.Actions["launch"];
            var activateAction = metadata.Actions["activate"];

            // Parameter key order per action (slot editor display order).
            switchAction.Parameters.Select(p => p.Key).Should().Equal("app", "path", "arguments");
            launchAction.Parameters.Select(p => p.Key).Should().Equal("path", "arguments");
            activateAction.Parameters.Select(p => p.Key).Should().Equal("app");

            // Shared action suggestions.
            foreach (var action in new[] { switchAction, launchAction, activateAction })
            {
                action.SuggestedIconKey.Should().Be("E8AB");
                action.SuggestedColorHex.Should().Be("#2196F3");
            }

            // switch.app and activate.app are one canonical definition (anti-drift core assertion).
            var app = switchAction.Parameters[0];
            activateAction.Parameters[0].Should().BeEquivalentTo(app,
                "the app parameter is defined once and shared by switch/activate");

            app.Key.Should().Be("app");
            app.Type.Should().Be("string");
            app.Label.Should().Be("Process Name");
            app.Description.Should().Be("Executable process name used to find the target window.");
            app.IsRequired.Should().BeTrue();
            app.Group.Should().Be(SlotParameterGroup.Required);
            app.Placeholder.Should().Be("chrome");
            app.Example.Should().Be("chrome");
            app.InputHint.Should().Be("Use the process name without .exe.");
            app.ValidationHint.Should().Be("Pick the running app by process name, without .exe.");
            app.PickerIntent.Should().Be(SlotPickerIntent.Process);
            app.IsSensitive.Should().BeFalse();
            app.SummaryMode.Should().Be(SlotParameterSummaryMode.RawValue);
            app.SummaryLabel.Should().Be("App");
            app.ConfiguredSummaryText.Should().Be("app selected");
            app.MissingSummaryText.Should().Be("app missing");
            app.PresentationHint.Should().Be(SlotParameterPresentationHint.QuickEdit);
            app.QuickEditPriority.Should().Be(100);
            app.Validators.Should().ContainSingle().Which.Should().BeOfType<RequiredValidator>();

            // switch.path = optional fallback-path variant.
            var fallbackPath = switchAction.Parameters[1];
            fallbackPath.Label.Should().Be("Launch Path");
            fallbackPath.Description.Should().Be("Optional fallback executable path used when the app is not already running.");
            fallbackPath.IsRequired.Should().BeFalse();
            fallbackPath.Group.Should().Be(SlotParameterGroup.Optional);
            fallbackPath.Placeholder.Should().Be("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe");
            fallbackPath.Example.Should().Be("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe");
            fallbackPath.InputHint.Should().Be("Use an absolute path for reliable launching.");
            fallbackPath.ValidationHint.Should().Be("Add a fallback executable only if this slot should launch when no window is found.");
            fallbackPath.PickerIntent.Should().Be(SlotPickerIntent.Process);
            fallbackPath.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
            fallbackPath.SummaryLabel.Should().Be("Launch");
            fallbackPath.ConfiguredSummaryText.Should().Be("fallback ready");
            fallbackPath.MissingSummaryText.Should().Be("switch only");
            fallbackPath.PresentationHint.Should().Be(SlotParameterPresentationHint.DialogOnly);
            fallbackPath.QuickEditPriority.Should().Be(0);
            fallbackPath.Validators.Should().BeEmpty();

            // launch.path = required executable-path variant.
            var execPath = launchAction.Parameters[0];
            execPath.Label.Should().Be("Executable Path");
            execPath.Description.Should().Be("Absolute path to the application to launch.");
            execPath.IsRequired.Should().BeTrue();
            execPath.Group.Should().Be(SlotParameterGroup.Required);
            execPath.Placeholder.Should().Be("C:\\Windows\\System32\\notepad.exe");
            execPath.Example.Should().Be("C:\\Windows\\System32\\notepad.exe");
            execPath.InputHint.Should().Be("Use a full path to an executable, shortcut, or script.");
            execPath.ValidationHint.Should().Be("Pick an executable, shortcut, or script to launch.");
            execPath.PickerIntent.Should().Be(SlotPickerIntent.Process);
            execPath.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
            execPath.SummaryLabel.Should().Be("App");
            execPath.ConfiguredSummaryText.Should().Be("path ready");
            execPath.MissingSummaryText.Should().Be("path missing");
            execPath.PresentationHint.Should().Be(SlotParameterPresentationHint.QuickEdit);
            execPath.QuickEditPriority.Should().Be(100);
            execPath.Validators.Should().ContainSingle().Which.Should().BeOfType<RequiredValidator>();

            // switch.arguments = Advanced-group variant with launch hints.
            var switchArgs = switchAction.Parameters[2];
            switchArgs.Label.Should().Be("Launch Arguments");
            switchArgs.Description.Should().Be("Optional command-line arguments passed when launching the app.");
            switchArgs.Group.Should().Be(SlotParameterGroup.Advanced);
            switchArgs.IsRequired.Should().BeFalse();
            switchArgs.Placeholder.Should().Be("--profile-directory=Default");
            switchArgs.Example.Should().Be("--new-window https://example.com");
            switchArgs.InputHint.Should().Be("Applied only when a new process is launched.");
            switchArgs.ValidationHint.Should().Be("Applied only when a new process is launched.");
            switchArgs.PickerIntent.Should().Be(SlotPickerIntent.None);
            switchArgs.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
            switchArgs.SummaryLabel.Should().Be("Args");
            switchArgs.ConfiguredSummaryText.Should().Be("args set");
            switchArgs.MissingSummaryText.Should().Be("no args");
            switchArgs.PresentationHint.Should().Be(SlotParameterPresentationHint.DialogOnly);
            switchArgs.Validators.Should().BeEmpty();

            // launch.arguments = Optional-group variant without hints.
            var launchArgs = launchAction.Parameters[1];
            launchArgs.Label.Should().Be("Launch Arguments");
            launchArgs.Description.Should().Be("Optional command-line arguments passed to the target application.");
            launchArgs.Group.Should().Be(SlotParameterGroup.Optional);
            launchArgs.IsRequired.Should().BeFalse();
            launchArgs.Placeholder.Should().Be("--incognito");
            launchArgs.Example.Should().Be("--new-window https://example.com");
            launchArgs.InputHint.Should().BeNull();
            launchArgs.ValidationHint.Should().BeNull();
            launchArgs.PickerIntent.Should().Be(SlotPickerIntent.None);
            launchArgs.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
            launchArgs.SummaryLabel.Should().Be("Args");
            launchArgs.ConfiguredSummaryText.Should().Be("args set");
            launchArgs.MissingSummaryText.Should().Be("no args");
            launchArgs.PresentationHint.Should().Be(SlotParameterPresentationHint.DialogOnly);
            launchArgs.Validators.Should().BeEmpty();
        }

        [Fact]
        public void SecretFillMetadata_ShouldExposeCanonicalActionAndLegacyAlias()
        {
            var executionService = new Mock<ISecretFillExecutionService>();
            var loc = new Mock<ILocalizationService>();
            var plugin = new SecretFillPlugin(NullLogger<SecretFillPlugin>.Instance, loc.Object, executionService.Object);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("AutoFill");
            metadata.Capabilities.SupportedActions.Should().Equal("fill");
            metadata.Actions.Keys.Should().Equal("fill");
            metadata.Actions["fill"].Aliases.Should().Contain("inject");
            metadata.Actions["fill"].Label.Should().Be("Fill Password");
        }

        [Fact]
        public void PulsarControlMetadata_ShouldExposeCanonicalActionContract()
        {
            var plugin = new SystemCommandPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Pulsar Settings");
            metadata.Actions.Keys.Should().Equal("open-settings", "quick-add-profile");
        }

        // ============ Cross-plugin contract guards ============
        //
        // [Architecture review 2026-09-11, candidate #2] The parameter specs of every
        // built-in plugin — Core AND Extension — are held to the shared canonical shapes.
        // Before this the guard only covered WinSwitcher, so the Extension plugins could
        // drift unobserved.
        //
        // Scope note: the guards below cover the shapes SlotParameterSpecs actually owns
        // (the multi-consumer ones). Single-consumer shapes stay asserted by the plugin
        // that declares them; a guard over one consumer would guard nothing.

        [Fact]
        public void ScriptPathParameters_AllMatchTheCanonicalFilePathSpec()
        {
            var specs = AllParameters().Where(p => p.Key == SlotParameterSpecs.ScriptPathKey).ToList();

            specs.Should().NotBeEmpty("the shared file-path shape must still be in use");

            foreach (var spec in specs)
            {
                spec.Type.Should().Be("string");
                spec.IsRequired.Should().BeTrue();
                spec.Group.Should().Be(SlotParameterGroup.Required);
                spec.SummaryLabel.Should().Be("Script");
                spec.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
                spec.ConfiguredSummaryText.Should().Be("file ready");
                spec.MissingSummaryText.Should().Be("file missing");
                spec.PresentationHint.Should().Be(SlotParameterPresentationHint.QuickEdit);
                spec.QuickEditPriority.Should().Be(100);
                spec.PickerIntent.Should().Be(SlotPickerIntent.File);
                spec.Validators.Should().ContainSingle().Which.Should().BeOfType<RequiredValidator>();
            }
        }

        [Fact]
        public void ScriptPathParameters_ShareTheirStructuralFieldsAcrossPlugins()
        {
            var specs = AllParameters().Where(p => p.Key == SlotParameterSpecs.ScriptPathKey).ToList();

            specs.Should().HaveCountGreaterThan(1, "this guard only earns its keep with two or more consumers");

            foreach (var other in specs.Skip(1))
            {
                other.Should().BeEquivalentTo(specs[0], options => options
                        .Excluding(p => p.Description)
                        .Excluding(p => p.Placeholder)
                        .Excluding(p => p.Example)
                        .Excluding(p => p.InputHint)
                        .Excluding(p => p.ValidationHint),
                    "the file-path shape is structural — only the prose may differ per plugin");
            }
        }

        [Fact]
        public void ArgumentsParameters_AllMatchTheCanonicalArgumentsSpec()
        {
            var specs = AllParameters().Where(p => p.Key == SlotParameterSpecs.ArgumentsKey).ToList();

            specs.Should().NotBeEmpty("the shared arguments shape must still be in use");

            foreach (var spec in specs)
            {
                spec.Type.Should().Be("string");
                spec.IsRequired.Should().BeFalse();
                spec.SummaryLabel.Should().Be("Args");
                spec.SummaryMode.Should().Be(SlotParameterSummaryMode.SafeStateOnly);
                spec.ConfiguredSummaryText.Should().Be("args set");
                spec.MissingSummaryText.Should().Be("no args");
                spec.PresentationHint.Should().Be(SlotParameterPresentationHint.DialogOnly);
                spec.Validators.Should().BeEmpty();
            }
        }

        [Fact]
        public void ArgumentsParameters_ShareTheirStructuralFieldsAcrossPlugins()
        {
            var specs = AllParameters().Where(p => p.Key == SlotParameterSpecs.ArgumentsKey).ToList();

            specs.Should().HaveCountGreaterThan(1, "this guard only earns its keep with two or more consumers");

            foreach (var other in specs.Skip(1))
            {
                other.Should().BeEquivalentTo(specs[0], options => options
                        .Excluding(p => p.Label)
                        .Excluding(p => p.Description)
                        .Excluding(p => p.Group)
                        .Excluding(p => p.Placeholder)
                        .Excluding(p => p.Example)
                        .Excluding(p => p.InputHint)
                        .Excluding(p => p.ValidationHint),
                    "the arguments shape is structural — only the prose and group may differ per action");
            }
        }

        /// <summary>
        /// Every parameter declared by any built-in plugin, across every Action.
        /// </summary>
        private static IEnumerable<SlotParameterMetadata> AllParameters() =>
            AllBuiltInMetadata()
                .SelectMany(metadata => metadata.Actions.Values)
                .SelectMany(action => action.Parameters);

        /// <summary>
        /// Every built-in plugin, Core and Extension alike. Extension plugins are built here
        /// with mocked seams because they have no parameterless constructor.
        /// </summary>
        private static IEnumerable<PluginMetadata> AllBuiltInMetadata()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);

            yield return new WinSwitcherPlugin().GetMetadata();
            yield return new SystemCommandPlugin().GetMetadata();
            yield return new SecretFillPlugin(
                NullLogger<SecretFillPlugin>.Instance,
                loc.Object,
                new Mock<ISecretFillExecutionService>().Object).GetMetadata();
            yield return new VbaRunnerPlugin().GetMetadata();
            yield return new BookmarkletRunnerPlugin().GetMetadata();
            yield return new CommandPlugin(
                NullLogger<CommandPlugin>.Instance,
                new Mock<IKeySender>().Object,
                new Mock<IProcessLauncher>().Object,
                loc.Object,
                new Mock<IWindowService>().Object,
                new Mock<IFocusManager>().Object).GetMetadata();
        }
    }
}
