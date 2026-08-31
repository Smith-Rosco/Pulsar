using Pulsar.Models;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// Semantic outcome of feeding a mouse event to the <see cref="RightDragGestureDetector"/>.
    /// </summary>
    public enum RightDragGestureDecision
    {
        /// <summary>Nothing to do; the event passes through to the target application.</summary>
        None,

        /// <summary>Summon the task-switcher menu (switcher modifier + right-button down).</summary>
        SwitcherSummon,

        /// <summary>Summon the action menu (action modifier + right-button down).</summary>
        ActionSummon,

        /// <summary>The right-button that summoned a menu has been released; execute the selection.</summary>
        GestureRelease,

        /// <summary>
        /// The gesture was claimed (a configured modifier was held) but the cursor
        /// never crossed the drag threshold before release. Replay a synthetic
        /// right-click to the source application so its native context menu appears.
        /// </summary>
        SubThresholdRelease
    }

    /// <summary>
    /// Pure, testable state machine for the "right-click to summon" gesture.
    ///
    /// Both menus require their own modifier, so the decision is unambiguous at the
    /// right-button DOWN — the caller swallows the whole gesture and the source
    /// application never sees any input (no stray context menus, no right-click
    /// paste). Plain right-clicks (no configured modifier) pass through untouched.
    ///
    /// Priority when both modifiers are held: the action menu wins.
    ///
    /// The detector tracks a displacement threshold (StarPie <c>DragThreshold</c>
    /// model) so a "plain click while holding the modifier" can be handed back to
    /// the source app via a replayed click instead of being swallowed:
    ///
    /// - <see cref="GestureSummonMode.Immediate"/> (default): the menu is summoned
    ///   at button-down; the threshold is irrelevant and every release is a
    ///   <see cref="RightDragGestureDecision.GestureRelease"/>.
    /// - <see cref="GestureSummonMode.OnThreshold"/>: the menu is summoned only once
    ///   the displacement crosses the threshold (<see cref="FeedDisplacement"/>);
    ///   a sub-threshold release resolves to
    ///   <see cref="RightDragGestureDecision.SubThresholdRelease"/>.
    /// </summary>
    public sealed class RightDragGestureDetector
    {
        private double _thresholdSquared;
        private GestureSummonMode _summonMode;
        private bool _thresholdCrossed;

        /// <summary>The right button is currently held and the press is being tracked.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>A menu has been summoned by the gesture during the current press.</summary>
        public bool IsSummoned { get; private set; }

        public RightDragGestureDetector(
            GestureSummonMode summonMode = GestureSummonMode.Immediate,
            double dragThreshold = 25.0)
        {
            _summonMode = summonMode;
            _thresholdSquared = dragThreshold * dragThreshold;
        }

        /// <summary>
        /// Applies a configuration change. Never touches an in-flight press state:
        /// <see cref="IsPressed"/> / <see cref="IsSummoned"/> are preserved so a
        /// config refresh mid-gesture cannot leak the release (see D3).
        /// </summary>
        public void Configure(GestureSummonMode summonMode, double dragThreshold)
        {
            _summonMode = summonMode;
            _thresholdSquared = dragThreshold * dragThreshold;
        }

        /// <summary>
        /// Feeds a right-button down. Returns the summon decision for the held
        /// modifier, or <see cref="RightDragGestureDecision.None"/> when no
        /// configured modifier is held (the press is not claimed and the right-click
        /// passes through to the application).
        /// </summary>
        public RightDragGestureDecision OnRightDown(bool switcherModifierHeld, bool actionModifierHeld)
        {
            if (actionModifierHeld)
            {
                BeginPress();
                return RightDragGestureDecision.ActionSummon;
            }

            if (switcherModifierHeld)
            {
                BeginPress();
                return RightDragGestureDecision.SwitcherSummon;
            }

            IsPressed = false;
            IsSummoned = false;
            _thresholdCrossed = false;
            return RightDragGestureDecision.None;
        }

        /// <summary>
        /// Feeds cursor movement relative to the button-down position. Returns true
        /// when the displacement crossed the drag threshold for the first time this
        /// press (transitioning the detector into the summoned state exactly once);
        /// false when not pressed, already summoned, or still sub-threshold.
        /// </summary>
        public bool FeedDisplacement(double dx, double dy)
        {
            if (!IsPressed || IsSummoned)
            {
                return false;
            }

            if (!_thresholdCrossed && dx * dx + dy * dy >= _thresholdSquared)
            {
                _thresholdCrossed = true;
                IsSummoned = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Feeds a right-button up. Produces <see cref="RightDragGestureDecision.GestureRelease"/>
        /// when a menu was summoned during this press (the release executes the
        /// selection), <see cref="RightDragGestureDecision.SubThresholdRelease"/>
        /// when the press was claimed but never crossed the threshold (replay the
        /// click to the source app), otherwise <see cref="RightDragGestureDecision.None"/>.
        /// </summary>
        public RightDragGestureDecision OnRightUp()
        {
            bool summoned = IsSummoned;
            bool pressed = IsPressed;
            IsPressed = false;
            IsSummoned = false;
            _thresholdCrossed = false;

            if (summoned)
            {
                return RightDragGestureDecision.GestureRelease;
            }

            return pressed ? RightDragGestureDecision.SubThresholdRelease : RightDragGestureDecision.None;
        }

        /// <summary>Abandons any in-progress press (e.g. the feature was disabled).</summary>
        public void Reset()
        {
            IsPressed = false;
            IsSummoned = false;
            _thresholdCrossed = false;
        }

        private void BeginPress()
        {
            IsPressed = true;
            _thresholdCrossed = false;
            IsSummoned = _summonMode == GestureSummonMode.Immediate;
        }
    }
}
