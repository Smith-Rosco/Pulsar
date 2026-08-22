using System.Collections.Generic;
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
        void SetActionExecuted(bool value);
        void RestoreRootMenu();
        Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex);
    }
}
