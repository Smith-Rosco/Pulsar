using System;
using System.Collections.Generic;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// Everything the Radial Menu's visual-state module needs to render one frame of
    /// centre identity: which Slot is hovered, which menu state we are in, the Slot
    /// collection in play, and the three sinks it writes back to.
    /// </summary>
    /// <remarks>
    /// This context exists so the cascade policies (ADR-024 D8 centre identity, D11
    /// dynamic-title suppression) have a single home inside the coordinator. Without
    /// it the caller had to derive <c>preserveCenterIdentity</c> itself and hand it
    /// over as a boolean — spreading the same policy across two modules.
    /// </remarks>
    public sealed record VisualStateContext
    {
        public int ActiveSlotIndex { get; init; }

        public MenuState MenuState { get; init; }

        /// <summary>The root centre label ("Pulsar"), or the parent Slot's label in a cascade.</summary>
        public string CenterText { get; init; } = string.Empty;

        public IReadOnlyCollection<SlotViewModel> Slots { get; init; } = Array.Empty<SlotViewModel>();

        public SlotViewModel CenterSlot { get; init; } = null!;

        /// <summary>
        /// True when the open sub-menu is a Cascade Sub-Menu. Drives ADR-024 D8/D11.
        /// The caller only reports the fact; what it implies is the coordinator's business.
        /// </summary>
        public bool IsCascadeSubMenu { get; init; }

        public Func<PreviewHostContext> GetPreviewHostContext { get; init; } = null!;

        public Action<string> SetDynamicTitle { get; init; } = null!;

        public Action<ResolvedWindowPreview> SetCenterPreview { get; init; } = null!;
    }

    /// <summary>
    /// The seam for the Radial Menu's centre visual state: label, icon, badge, dynamic
    /// title and window-preview capture (including its cancellation).
    /// </summary>
    public interface IRadialMenuVisualStateCoordinator
    {
        /// <summary>Recomputes the centre for the given hover/menu state.</summary>
        void UpdateVisuals(VisualStateContext context);

        /// <summary>Warms the centre preview for a sub-menu that is about to open.</summary>
        void PrimeSubMenuPreview(
            ProcessWindowInfo? mostRecentWindow,
            Func<bool> shouldCapture,
            Func<PreviewHostContext> getPreviewHostContext,
            Action<ResolvedWindowPreview> setCenterPreview);
    }
}
