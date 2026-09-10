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
using Pulsar.Plugins.Extensions.Command;
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
    }
}
