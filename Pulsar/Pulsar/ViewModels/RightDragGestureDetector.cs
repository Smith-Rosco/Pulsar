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
        GestureRelease
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
    /// </summary>
    public sealed class RightDragGestureDetector
    {
        /// <summary>The right button is currently held and the press is being tracked.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>A menu has been summoned by the gesture during the current press.</summary>
        public bool IsSummoned { get; private set; }

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
                IsPressed = true;
                IsSummoned = true;
                return RightDragGestureDecision.ActionSummon;
            }

            if (switcherModifierHeld)
            {
                IsPressed = true;
                IsSummoned = true;
                return RightDragGestureDecision.SwitcherSummon;
            }

            IsPressed = false;
            IsSummoned = false;
            return RightDragGestureDecision.None;
        }

        /// <summary>
        /// Feeds a right-button up. Produces <see cref="RightDragGestureDecision.GestureRelease"/>
        /// when a menu was summoned during this press (the release executes the
        /// selection), otherwise <see cref="RightDragGestureDecision.None"/> so the
        /// event passes through as a normal right-click.
        /// </summary>
        public RightDragGestureDecision OnRightUp()
        {
            bool summoned = IsSummoned;
            IsPressed = false;
            IsSummoned = false;
            return summoned ? RightDragGestureDecision.GestureRelease : RightDragGestureDecision.None;
        }

        /// <summary>Abandons any in-progress press (e.g. the feature was disabled).</summary>
        public void Reset()
        {
            IsPressed = false;
            IsSummoned = false;
        }
    }
}
