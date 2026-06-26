using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Helpers;
using Pulsar.Core.Plugin;
using Pulsar.Services; // Added
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.ViewModels.Strategies
{
    public class CommandPageProvider : BasePageProvider
    {
        private readonly List<PluginSlot> _allSlots;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly PulsarContext _context;
        private readonly ITrayService _trayService;
        private readonly IActionFeedbackService _feedbackService;

        private readonly IServiceProvider _serviceProvider;

        public override int TotalPages => (int)Math.Ceiling((double)_allSlots.Count / (double)ItemsPerPage);

        public bool HasCreatorSlot() => _allSlots.Any(s => s.PluginId == "internal:create_profile");

        public CommandPageProvider(
            List<PluginSlot> slots, 
            IPluginRegistry pluginRegistry, 
            PulsarContext context, 
            ITrayService trayService,
            IServiceProvider serviceProvider)
            : base(serviceProvider.GetService(typeof(IConfigService)) as IConfigService)
        {
            // [Refactor] 按 Slot 字段排序，确保用户自定义顺序生效
            _allSlots = slots?.OrderBy(s => s.Slot).ToList() ?? new List<PluginSlot>();
            _pluginRegistry = pluginRegistry;
            _context = context;
            _trayService = trayService;
            _feedbackService = (IActionFeedbackService)serviceProvider.GetService(typeof(IActionFeedbackService))!;
            _serviceProvider = serviceProvider;
        }

        public override Task LoadAsync()
        {
            _currentPage = 0;
            return Task.CompletedTask;
        }

        public override void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot)
        {
            ClearSlots(slots);
            
            // [Refactor] 按 Slot 排序后分页，使用动态 ItemsPerPage
            var pageItems = _allSlots.Skip(_currentPage * ItemsPerPage).Take(ItemsPerPage).ToList();

            for (int i = 0; i < pageItems.Count; i++)
            {
                var item = pageItems[i];
                var slot = slots[i]; // Slot 1 is index 0
                
                // [Refactor] Slot 值已经在配置中持久化，无需运行时分配
                
                slot.Label = item.Label;
                slot.LoadIconData(item.IconKey);
                var presentation = SlotPresentationBuilder.Build(item);
                item.SetPresentation(presentation);
                slot.ApplyPresentation(presentation);
                slot.Type = SlotType.Action;
                slot.DataContext = item;

                if (item.PluginId == "internal:create_profile")
                {
                    // Resolve dependencies
                    var configService = (IConfigService)_serviceProvider.GetService(typeof(IConfigService))!;
                    
                    slot.ActionStrategy = new CreateProfileStrategy(
                        _context.TargetProcessName, 
                        _context.GetTargetExePathAsync,
                        configService, 
                        _serviceProvider);
                    
                    slot.IsEnabled = true; // Always enabled
                }
                else
                {
                    // Check if plugin is enabled
                    bool isEnabled = _pluginRegistry.IsPluginEnabled(item.PluginId);
                    slot.IsEnabled = isEnabled;
                    
                    if (isEnabled)
                    {
                        slot.ActionStrategy = new PluginActionStrategy(item, _pluginRegistry, _context, _trayService, _feedbackService,
                            _serviceProvider.GetService(typeof(IPluginUsageTracker)) as IPluginUsageTracker);
                    }
                    else
                    {
                        // Disabled Strategy (Toast or NoOp)
                        slot.ActionStrategy = new NoOpStrategy(); 
                        // Optional: Append (Disabled) to label? 
                        // slot.Label += " (Disabled)"; 
                        // Better to rely on visual cue (greyed out)
                    }
                }
            }

            // Update Center Text
            string centerText = TotalPages > 1 
                ? $"Page {_currentPage + 1}/{TotalPages}" 
                : (string.IsNullOrEmpty(_context.TargetProcessName) ? "Global" : _context.DisplayProcessName);
            centerSlot.Label = centerText;
            centerSlot.ActionStrategy = NoOpStrategy.Instance;
            centerSlot.Type = SlotType.Action;
            centerSlot.BadgeCount = 0;
        }
    }
}
