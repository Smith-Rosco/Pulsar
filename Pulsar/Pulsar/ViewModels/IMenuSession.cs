using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// The narrow contract a Slot Action can see of the running Menu Session.
    /// Strategies depend on this seam instead of the whole RadialMenuViewModel,
    /// so the session's input/state decisions can be tested without a view.
    /// </summary>
    public interface IMenuSession
    {
        bool IsVisible { get; set; }
        bool IsInSubMenu { get; }
        void SetActionExecuted(bool value, SlotViewModel? executedSlot = null);

        /// <summary>
        /// Opens the execution window for a Slot Action: records WHICH slot is running
        /// (so E2E/debug assertions never fall back to guessing by index) and hides the
        /// menu in the same breath, before the action produces any observable effect.
        /// </summary>
        /// <remarks>
        /// The ordering is load-bearing and therefore owned by the implementation, not
        /// by callers: a plugin that simulates input (e.g. a Ctrl release) would
        /// re-trigger the hotkey hook while the menu is still visible — an infinite
        /// loop. Strategies declare intent; the session owns the sequence.
        /// </remarks>
        void BeginExecution(SlotViewModel slot);

        void RestoreRootMenu();
        Task EnterSubMenuAsync(SubMenuDescriptor descriptor, int clickedSlotIndex);
    }
}
