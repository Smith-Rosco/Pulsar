// [Path]: Pulsar/Pulsar/Features/Tutorial/Services/TutorialStepCardFactory.cs

using Pulsar.Core.Localization;
using Pulsar.Features.Tutorial.Views;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Tutorial.Services
{
    /// <summary>
    /// Creates TutorialStepCard instances with their dependencies injected.
    /// Extracted from TutorialOrchestrator's inline `new TutorialStepCard()`
    /// (architecture review candidate C5, 2026-09-09): the Orchestrator is already
    /// mocked in tests, and the factory is mockable the same way, so card creation
    /// no longer forces a WPF InitializeComponent round-trip in Orchestrator tests.
    /// Mirrors the Func&lt;T&gt; factory pattern used elsewhere in the composition root.
    /// </summary>
    public interface ITutorialStepCardFactory
    {
        TutorialStepCard Create();
    }

    public sealed class TutorialStepCardFactory : ITutorialStepCardFactory
    {
        private readonly ILocalizationService _loc;
        private readonly IConfigService _configService;

        public TutorialStepCardFactory(ILocalizationService loc, IConfigService configService)
        {
            _loc = loc;
            _configService = configService;
        }

        public TutorialStepCard Create()
        {
            return new TutorialStepCard(_loc, _configService);
        }
    }
}
