using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Cascade submenu strategy (id <c>cascade</c>). Configures the submenu for a
    /// <see cref="CascadeSubMenuDescriptor"/>: a back-navigation center slot carrying
    /// the cascade label, and child slots mapped from each <see cref="SubSlotDescriptor"/>
    /// to a <see cref="PluginActionStrategy"/> (full plugin pipeline). Empty page slots
    /// become <see cref="NoOpStrategy"/> fillers; children whose plugin/action is unknown
    /// are marked not-enabled. Pagination derives from <see cref="SubSlotDescriptor"/> count
    /// via the coordinator's page window.
    /// </summary>
    public sealed class CascadeSubMenuStrategy : ISubMenuStrategy
    {
        public const string StrategyIdValue = "cascade";

        public string StrategyId => StrategyIdValue;

        private readonly IPluginExecutor _executor;
        private readonly IPluginMetadataRegistry _metadataRegistry;
        private readonly ITrayService _trayService;
        private readonly IActionFeedbackService _feedbackService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;
        private readonly ILogger<CascadeSubMenuStrategy>? _logger;

        public CascadeSubMenuStrategy(
            IPluginExecutor executor,
            IPluginMetadataRegistry metadataRegistry,
            ITrayService trayService,
            IActionFeedbackService feedbackService,
            IPluginUsageTracker? usageTracker = null,
            IActionFeedbackPresenter? feedbackPresenter = null,
            ILogger<CascadeSubMenuStrategy>? logger = null)
        {
            _executor = executor;
            _metadataRegistry = metadataRegistry;
            _trayService = trayService;
            _feedbackService = feedbackService;
            _usageTracker = usageTracker;
            _feedbackPresenter = feedbackPresenter;
            _logger = logger;
        }

        public ProcessWindowInfo? ConfigureSubMenu(SubMenuContext context, SubMenuDescriptor descriptor)
        {
            if (descriptor is not CascadeSubMenuDescriptor cascade)
            {
                _logger?.LogWarning("[CascadeSubMenuStrategy] Unexpected descriptor type {DescriptorType} — no-op",
                    descriptor?.GetType().Name ?? "<null>");
                return null;
            }

            context.CenterSlot.Label = cascade.Label;
            context.CenterSlot.Type = SlotType.Action;
            context.CenterSlot.ActionStrategy = new BackActionStrategy();

            int startIndex = Math.Max(0, context.PageIndex * Math.Max(1, context.SlotsPerPage));
            var pageSubSlots = cascade.SubSlots
                .Skip(startIndex)
                .Take(Math.Max(1, context.SlotsPerPage))
                .ToList();

            for (int i = 0; i < context.SlotsPerPage; i++)
            {
                var slot = context.Slots.FirstOrDefault(s => s.SlotIndex == i + 1);
                if (slot == null) continue;

                if (i < pageSubSlots.Count)
                {
                    var sub = pageSubSlots[i];
                    slot.Label = sub.Label;
                    slot.LoadIconData(sub.IconKey);
                    slot.Type = SlotType.Action;
                    slot.DataContext = sub;
                    slot.BadgeCount = 0;
                    slot.ClearPresentation();
                    slot.SetColor(sub.ColorHex);

                    bool isKnown = _metadataRegistry.GetActionMetadata(sub.PluginId, sub.Action) != null;
                    slot.IsEnabled = isKnown;

                    if (isKnown && context.PulsarContext != null)
                    {
                        var pluginSlot = new PluginSlot
                        {
                            PluginId = sub.PluginId,
                            Action = sub.Action,
                            Args = sub.Args ?? new Dictionary<string, string>(),
                            Label = sub.Label,
                            IconKey = sub.IconKey,
                            Color = sub.ColorHex
                        };
                        slot.ActionStrategy = new PluginActionStrategy(
                            pluginSlot,
                            _executor,
                            context.PulsarContext,
                            _trayService,
                            _feedbackService,
                            _usageTracker,
                            _feedbackPresenter);
                    }
                    else
                    {
                        slot.ActionStrategy = new NoOpStrategy();
                    }

                    slot.ResetAnimation();
                }
                else
                {
                    slot.Label = string.Empty;
                    slot.LoadIconData(string.Empty);
                    slot.Type = SlotType.None;
                    slot.ActionStrategy = new NoOpStrategy();
                    slot.BadgeCount = 0;
                    slot.ClearPresentation();
                    slot.ResetAnimation();
                }
            }

            return null;
        }
    }
}
