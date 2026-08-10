using System;
using System.Windows.Controls;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Views.Controls
{
    /// <summary>
    /// Builds the right-click context menu for a wheel slot: Move to page/slot, Edit, Delete.
    /// Kept as a separate, testable component so the code-behind stays thin.
    /// </summary>
    public sealed class SlotContextMenuBuilder
    {
        private readonly ILocalizationService _loc;

        public SlotContextMenuBuilder(ILocalizationService loc)
        {
            _loc = loc;
        }

        public ContextMenu Build(PluginSlot slot, SlotWheelEditorViewModel vm)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (vm == null)
            {
                throw new ArgumentNullException(nameof(vm));
            }

            var menu = new ContextMenu();

            var moveTo = new MenuItem { Header = _loc["Settings.Slots.Wheel.MoveTo"] };
            for (int page = 1; page <= vm.TotalPages; page++)
            {
                var pageItem = new MenuItem { Header = string.Format(_loc["Settings.Slots.Wheel.PageFormat"], page) };
                for (int slotNum = 1; slotNum <= vm.SlotsPerPage; slotNum++)
                {
                    int targetPage = page;
                    int targetSlot = slotNum;
                    var slotItem = new MenuItem { Header = string.Format(_loc["Settings.Slots.Wheel.SlotFormat"], slotNum) };
                    slotItem.Click += (_, _) => vm.MoveToPageAndSlot(slot, targetPage, targetSlot);
                    pageItem.Items.Add(slotItem);
                }

                moveTo.Items.Add(pageItem);
            }

            menu.Items.Add(moveTo);
            menu.Items.Add(new Separator());

            var edit = new MenuItem { Header = _loc["Settings.Slots.Wheel.Edit"] };
            edit.Click += (_, _) => OnEdit?.Invoke(slot);
            menu.Items.Add(edit);

            var delete = new MenuItem { Header = _loc["Settings.Slots.Wheel.Delete"] };
            delete.Click += (_, _) => OnDelete?.Invoke(slot);
            menu.Items.Add(delete);

            return menu;
        }

        public Action<PluginSlot>? OnEdit;

        public Action<PluginSlot>? OnDelete;
    }
}
