using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Localization;
using Pulsar.Core.Messages;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    public partial class SlotWheelEditorViewModel : ObservableObject
    {
        private const double SourceCanvasSize = 500;
        private const double SourceCenter = 250;
        private const double SourceDefaultSlotSize = 50;
        private const int HighlightDurationMs = 2000;

        private readonly ISlotLayoutEngine _layoutEngine;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _loc;
        private IList<PluginSlot>? _slots;
        private int _slotsPerPage = 8;
        private LayoutParameters _layoutParams;
        private double _scale = 1.0;
        private CancellationTokenSource? _highlightCts;

        public SlotWheelEditorViewModel(
            ISlotLayoutEngine layoutEngine,
            ILocalizationService localizationService,
            IMessenger? messenger = null,
            double canvasSize = 520)
        {
            _layoutEngine = layoutEngine;
            _loc = localizationService;
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _messenger.Register<SlotAddedMessage>(this, (_, message) => HandleSlotAdded(message.Slot));
            CanvasSize = canvasSize;
        }

        public ObservableCollection<WheelSlotItem> Items { get; } = new();

        public double CanvasSize { get; }

        [ObservableProperty]
        private int _currentPage = 1;

        public int TotalPages => _slots == null || _slots.Count == 0
            ? 1
            : (int)Math.Ceiling(_slots.Count / (double)_slotsPerPage);

        public int TotalSlots => _slots?.Count ?? 0;

        public int SlotsPerPage => _slotsPerPage;

        public double Scale => _scale;

        public double Radius { get; private set; }

        public double SlotSize { get; private set; }

        public string PageDisplayText => $"{CurrentPage}/{TotalPages}";

        public bool HasPreviousPage => CurrentPage > 1;

        public bool HasNextPage => CurrentPage < TotalPages;

        [ObservableProperty]
        private PluginSlot? _highlightedSlot;

        partial void OnCurrentPageChanged(int value)
        {
            NotifyPageState();
            RebuildItems();
        }

        public void SetSlots(IList<PluginSlot>? slots, int slotsPerPage)
        {
            if (_slots is ObservableCollection<PluginSlot> previous)
            {
                previous.CollectionChanged -= OnSlotsCollectionChanged;
            }

            _slots = slots;
            _slotsPerPage = Math.Clamp(slotsPerPage, 1, 60);

            if (_slots is ObservableCollection<PluginSlot> current)
            {
                current.CollectionChanged += OnSlotsCollectionChanged;
            }

            _highlightCts?.Cancel();
            HighlightedSlot = null;

            ComputeLayout();
            SetCurrentPage(1);
        }

        public void RefreshLayout(int slotsPerPage)
        {
            _slotsPerPage = Math.Clamp(slotsPerPage, 1, 60);
            ComputeLayout();
            NormalizePage();
            RebuildItems();
        }

        public bool TryResolveDropPosition(double x, double y, out int position)
        {
            position = 0;
            if (_slots == null || _slotsPerPage <= 0 || _scale <= 0)
            {
                return false;
            }

            double px = x / _scale;
            double py = y / _scale;

            double dx = px - SourceCenter;
            double dy = py - SourceCenter;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < _layoutParams.DeadZoneRadius)
            {
                return false;
            }

            double maxDist = _layoutParams.Radius + (SourceDefaultSlotSize / 2) + 10;
            if (dist > maxDist)
            {
                return false;
            }

            int hit = _layoutEngine.HitTest(new Vector(px, py), _layoutParams);
            if (hit <= 0)
            {
                return false;
            }

            position = hit;
            return true;
        }

        public bool Reorder(PluginSlot source, int targetPositionInPage)
        {
            if (_slots == null || source == null)
            {
                return false;
            }

            int sourceIndex = _slots.IndexOf(source);
            if (sourceIndex < 0)
            {
                return false;
            }

            int targetIndex = (CurrentPage - 1) * _slotsPerPage + (targetPositionInPage - 1);
            targetIndex = Math.Clamp(targetIndex, 0, _slots.Count - 1);

            if (sourceIndex == targetIndex)
            {
                return false;
            }

            MoveAndRenumber(sourceIndex, targetIndex);
            return true;
        }

        public bool MoveToPageAndSlot(PluginSlot source, int page, int slotNumber)
        {
            if (_slots == null || _slots.Count == 0 || source == null)
            {
                return false;
            }

            int clampedPage = Math.Clamp(page, 1, TotalPages);
            int targetIndex = (clampedPage - 1) * _slotsPerPage + (slotNumber - 1);
            targetIndex = Math.Clamp(targetIndex, 0, _slots.Count - 1);

            int sourceIndex = _slots.IndexOf(source);
            if (sourceIndex < 0 || sourceIndex == targetIndex)
            {
                return false;
            }

            MoveAndRenumber(sourceIndex, targetIndex);
            SetCurrentPage(clampedPage);
            Flash(source);
            return true;
        }

        public void Flash(PluginSlot? slot)
        {
            _highlightCts?.Cancel();
            _highlightCts = new CancellationTokenSource();

            HighlightedSlot = slot;
            UpdateHighlight();

            if (slot == null)
            {
                return;
            }

            var token = _highlightCts.Token;
            _ = ClearHighlightAfterAsync(token);
        }

        public void GoToPage(int page)
        {
            SetCurrentPage(page);
        }

        [RelayCommand]
        private void GoPreviousPage()
        {
            if (CurrentPage > 1)
            {
                SetCurrentPage(CurrentPage - 1);
            }
        }

        [RelayCommand]
        private void GoNextPage()
        {
            if (CurrentPage < TotalPages)
            {
                SetCurrentPage(CurrentPage + 1);
            }
        }

        private void ComputeLayout()
        {
            int count = Math.Max(_slotsPerPage, 1);
            _layoutParams = _layoutEngine.CalculateOptimalLayout(count);
            _scale = CanvasSize / SourceCanvasSize;
            Radius = _layoutParams.Radius * _scale;
            SlotSize = CalculateScaledSlotSize(count);
        }

        private double CalculateScaledSlotSize(int count)
        {
            double size = _layoutEngine.CalculateOptimalSlotSize(count);

            return size * _scale;
        }

        private void SetCurrentPage(int page)
        {
            int target = Math.Clamp(page, 1, Math.Max(TotalPages, 1));
            if (CurrentPage == target)
            {
                RebuildItems();
                return;
            }

            CurrentPage = target;
        }

        private void NormalizePage()
        {
            if (TotalPages < CurrentPage)
            {
                SetCurrentPage(TotalPages);
            }
        }

        private void MoveAndRenumber(int sourceIndex, int targetIndex)
        {
            if (_slots is ObservableCollection<PluginSlot> observable)
            {
                observable.Move(sourceIndex, targetIndex);
            }
            else
            {
                var item = _slots![sourceIndex];
                _slots.RemoveAt(sourceIndex);
                _slots.Insert(targetIndex, item);
            }

            for (int i = 0; i < _slots!.Count; i++)
            {
                _slots[i].Slot = i + 1;
            }

            RebuildItems();
        }

        private void RebuildItems()
        {
            foreach (var item in Items)
            {
                item.Dispose();
            }

            Items.Clear();

            if (_slots == null)
            {
                return;
            }

            int startIndex = (CurrentPage - 1) * _slotsPerPage;
            int ringCount = Math.Max(_slotsPerPage, 1);

            for (int position = 1; position <= ringCount; position++)
            {
                int flatIndex = startIndex + (position - 1);
                var slot = flatIndex < _slots.Count ? _slots[flatIndex] : null;

                var enginePos = _layoutEngine.GetSlotPosition(position, ringCount, _layoutParams);
                Items.Add(new WheelSlotItem(
                    slot,
                    position,
                    enginePos.X * _scale,
                    enginePos.Y * _scale,
                    SlotSize,
                    _loc)
                {
                    IsHighlighted = slot != null && ReferenceEquals(slot, HighlightedSlot)
                });
            }

            NotifyPageState();
        }

        private void NotifyPageState()
        {
            OnPropertyChanged(nameof(PageDisplayText));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(TotalSlots));
        }

        private void OnSlotsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            NormalizePage();
            RebuildItems();
        }

        private void HandleSlotAdded(PluginSlot slot)
        {
            if (_slots == null)
            {
                return;
            }

            int index = _slots.IndexOf(slot);
            if (index < 0)
            {
                return;
            }

            SetCurrentPage(index / _slotsPerPage + 1);
            Flash(slot);
        }

        private void UpdateHighlight()
        {
            foreach (var item in Items)
            {
                item.IsHighlighted = item.Slot != null && ReferenceEquals(item.Slot, HighlightedSlot);
            }
        }

        private async Task ClearHighlightAfterAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(HighlightDurationMs, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!token.IsCancellationRequested)
            {
                HighlightedSlot = null;
                UpdateHighlight();
            }
        }
    }
}
