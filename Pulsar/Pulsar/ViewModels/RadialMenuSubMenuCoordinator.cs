using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// Result of a coordinator <see cref="ConfigureSubMenu"/> pass. <see cref="FallbackToRoot"/>
    /// is set when no strategy matched the descriptor — the session must restore the root
    /// menu instead of entering a submenu. <see cref="SelectedWindow"/> is the default-target
    /// window the strategy resolved (window strategies), used to prime the center preview.
    /// </summary>
    internal sealed class SubMenuConfigResult
    {
        public bool FallbackToRoot { get; init; }

        public ProcessWindowInfo? SelectedWindow { get; init; }
    }

    /// <summary>
    /// Host for submenu strategies. Selects the <see cref="ISubMenuStrategy"/> matching a
    /// descriptor's <see cref="SubMenuDescriptor.StrategyId"/> and delegates slot
    /// configuration to it. Window switching is one concrete strategy; cascade forms plug in
    /// later without touching the session. Unknown strategies fall back to the root menu with
    /// a logged warning — never throw.
    /// </summary>
    internal sealed class RadialMenuSubMenuCoordinator
    {
        private readonly IReadOnlyDictionary<string, ISubMenuStrategy> _strategiesById;
        private readonly ILogger? _logger;

        public RadialMenuSubMenuCoordinator(
            IEnumerable<ISubMenuStrategy> strategies,
            ILogger? logger = null)
        {
            _strategiesById = strategies
                ?.Where(s => s != null)
                .ToDictionary(s => s.StrategyId, s => s, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ISubMenuStrategy>(StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        /// <summary>
        /// Routes a descriptor to the matching strategy and configures the submenu slots.
        /// Returns <see cref="SubMenuConfigResult.FallbackToRoot"/>=true (with a logged warning)
        /// when no strategy is registered for <paramref name="descriptor"/>.StrategyId.
        /// </summary>
        public SubMenuConfigResult ConfigureSubMenu(
            SubMenuDescriptor descriptor,
            int slotsPerPage,
            int pageIndex,
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots,
            PulsarContext? pulsarContext = null)
        {
            if (descriptor == null)
            {
                _logger?.LogWarning("[SubMenuCoordinator] Null descriptor — falling back to root menu");
                return new SubMenuConfigResult { FallbackToRoot = true };
            }

            if (!_strategiesById.TryGetValue(descriptor.StrategyId, out var strategy))
            {
                _logger?.LogWarning(
                    "[SubMenuCoordinator] No strategy registered for StrategyId '{StrategyId}' — falling back to root menu",
                    descriptor.StrategyId);
                return new SubMenuConfigResult { FallbackToRoot = true };
            }

            var context = new SubMenuContext(centerSlot, slots, slotsPerPage, pageIndex, pulsarContext);
            var selectedWindow = strategy.ConfigureSubMenu(context, descriptor);
            return new SubMenuConfigResult { SelectedWindow = selectedWindow };
        }

        public void RestoreRootMenu(
            IPageProvider? pageProvider,
            IPagingController pagingController,
            ObservableCollection<SlotViewModel> slots,
            SlotViewModel centerSlot)
        {
            foreach (var slot in slots)
            {
                SubMenuColorPalette.Clear(slot);
            }

            if (pageProvider == null) return;

            // Synchronous refresh only. The page provider was loaded when the menu
            // opened; firing an async reload here would make root slots "pop" back
            // halfway through the submenu exit morph.
            pagingController.SetTotalPages(pageProvider.TotalPages);
            pageProvider.RefreshVisuals(slots, centerSlot);
        }
    }
}
