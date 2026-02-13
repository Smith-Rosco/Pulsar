using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Core.Plugin; // For PulsarContext
using Pulsar.Helpers;

namespace Pulsar.ViewModels.Strategies
{
    public interface IPageProvider
    {
        Task LoadAsync();
        void NextPage();
        void PrevPage();
        void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot);
    }

    /// <summary>
    /// Base class for simple list pagination
    /// </summary>
    public abstract class BasePageProvider : IPageProvider
    {
        protected int _currentPage = 0;
        protected int _itemsPerPage = 8;
        
        public virtual int TotalPages => 1;

        public abstract Task LoadAsync();

        public virtual void NextPage()
        {
            _currentPage++;
            if (_currentPage >= TotalPages) _currentPage = 0;
        }

        public virtual void PrevPage()
        {
            _currentPage--;
            if (_currentPage < 0) _currentPage = Math.Max(0, TotalPages - 1);
        }

        public abstract void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot);
        
        protected void ClearSlots(ObservableCollection<SlotViewModel> slots)
        {
            foreach (var slot in slots)
            {
                slot.Label = "";
                slot.LoadIconData(string.Empty);
                slot.IconImage = null;
                slot.Type = SlotType.None;
                slot.DataContext = null;
                slot.BadgeCount = 0;
                slot.SetColor(null);
                slot.ActionStrategy = new NoOpStrategy();
            }
        }
    }
}
