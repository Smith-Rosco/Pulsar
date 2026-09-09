using System;
using Pulsar.Core.Localization;

namespace Pulsar.ViewModels;

/// <summary>
/// [R3 2026-09-09] Single owner of the centre-slot identity decisions made
/// during submenu transitions (the "what does the centre orb show" policy).
/// The <see cref="SubMenuTransitionController"/> executes these directives
/// verbatim; the policy itself is pure and unit-testable without a harness.
/// <para>
/// D7/D8-class rework (parent identity leaking into / missing from the
/// centre orb) previously required editing scattered assignment blocks; every
/// transition-time centre identity decision now routes through this class.
/// Root-state centre identity (page providers / coordinator) is out of scope.
/// </para>
/// </summary>
internal static class CenterIdentityPolicy
{
    /// <summary>
    /// The centre text while a submenu is open: the parent slot's label, or the
    /// localized back affordance when the parent has none. Shared by the window
    /// morph and the cascade Ring entry (both centre orbs stand in for the
    /// parent); the cascade Fan keeps the root centre look (ADR-024 D8) and
    /// never calls this.
    /// </summary>
    public static string CenterTextForParent(SlotViewModel? parentSlot, ILocalizationService loc)
    {
        return !string.IsNullOrWhiteSpace(parentSlot?.Label)
            ? parentSlot.Label
            : loc["RadialMenu.Back"];
    }

    /// <summary>
    /// Copies the parent slot's icon onto the centre orb: a live
    /// <c>IconImage</c> wins; otherwise the icon key is (re)loaded. Called by
    /// both the window morph and the cascade Ring entry.
    /// </summary>
    public static void ApplyParentIcon(SlotViewModel centerSlot, SlotViewModel? parentSlot)
    {
        if (parentSlot == null)
        {
            return;
        }

        if (parentSlot.IconImage != null)
        {
            centerSlot.IconImage = parentSlot.IconImage;
        }
        else
        {
            centerSlot.LoadIconData(parentSlot.IconKey);
        }
    }

    /// <summary>
    /// The Ring's centre orb reads as the PARENT slot: its label too, not the
    /// descriptor's back label ([2026-09-06 user spec]). Returns the parent
    /// label, or <c>null</c> when the parent has no meaningful label (the
    /// executor then leaves the current orb label untouched).
    /// </summary>
    public static string? ParentOrbLabel(SlotViewModel? parentSlot)
    {
        return string.IsNullOrWhiteSpace(parentSlot?.Label) ? null : parentSlot!.Label;
    }

    /// <summary>
    /// Submenu page label: when the submenu spans multiple pages the current
    /// centre label gains a "page x / y" suffix; a single page keeps the label
    /// untouched (the historical no-format fast path).
    /// </summary>
    public static string SubMenuPageLabel(string currentLabel, int page, int totalPages, string pageFormat)
    {
        return totalPages > 1
            ? string.Format(pageFormat, currentLabel, page + 1, totalPages)
            : currentLabel;
    }
}
