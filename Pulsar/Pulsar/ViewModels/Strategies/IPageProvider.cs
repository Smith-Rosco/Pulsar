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
        int TotalPages { get; }  // [New] Add TotalPages to interface
        int CurrentPage { get; }  // [New] Add CurrentPage to interface
    }

    /// <summary>
    /// Base class for simple list pagination
    /// </summary>
    public abstract class BasePageProvider : IPageProvider
    {
        protected int _currentPage = 0;
        protected int _itemsPerPage = 8;
        protected readonly IConfigService? _configService;
        
        public virtual int TotalPages => 1;
        public int CurrentPage => _currentPage;  // [New] Public accessor
        
        /// <summary>
        /// Gets the dynamic items per page from config, or falls back to default
        /// </summary>
        protected int ItemsPerPage => _configService?.GetValidatedSlotsPerPage() ?? _itemsPerPage;

        protected BasePageProvider(IConfigService? configService = null)
        {
            _configService = configService;
            if (_configService != null)
            {
                _itemsPerPage = _configService.GetValidatedSlotsPerPage();
            }
        }

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
                slot.IsEnabled = true;
            }
        }
    }
}
