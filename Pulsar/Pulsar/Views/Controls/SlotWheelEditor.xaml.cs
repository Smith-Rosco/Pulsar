using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace Pulsar.Views.Controls
{
    public sealed class SlotWheelActionEventArgs : EventArgs
    {
        public SlotWheelActionEventArgs(PluginSlot slot)
        {
            Slot = slot;
        }

        public PluginSlot Slot { get; }
    }

    public partial class SlotWheelEditor : UserControl
    {
        private const double DragThreshold = 8;

        private readonly IServiceProvider _services;
        private SlotWheelEditorViewModel? _viewModel;
        private WheelSlotItem? _dragSource;
        private Point _dragStart;
        private bool _isDragging;
        private Border? _dragGhost;
        private WheelSlotItem? _emptySlotCandidate;
        private SlotContextMenuBuilder? _contextMenuBuilder;

        public event EventHandler<SlotWheelActionEventArgs>? EditRequested;
        public event EventHandler<SlotWheelActionEventArgs>? DeleteRequested;
        public event EventHandler? AddSlotRequested;

        public SlotWheelEditor()
        {
            InitializeComponent();
            _services = App.Current.Services;
            DataContextChanged += OnDataContextChanged;
        }

        private SlotWheelEditorViewModel? Vm => _viewModel;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = DataContext as SlotWheelEditorViewModel;
        }

        private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is ButtonBase)
            {
                return;
            }

            if (sender is not FrameworkElement fe || fe.DataContext is not WheelSlotItem item)
            {
                return;
            }

            if (item.IsEmpty)
            {
                _emptySlotCandidate = item;
                WheelItems.CaptureMouse();
                e.Handled = true;
                return;
            }

            _dragSource = item;
            _dragStart = e.GetPosition(WheelItems);
            WheelItems.CaptureMouse();
            e.Handled = true;
        }

        private void WheelItems_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSource == null)
            {
                return;
            }

            var pos = e.GetPosition(WheelItems);

            if (!_isDragging && (pos - _dragStart).Length > DragThreshold)
            {
                _isDragging = true;
                _dragSource.IsDragging = true;
                ShowDragGhost(_dragSource);
                Cursor = Cursors.SizeAll;
            }

            if (_isDragging)
            {
                PositionDragGhost(pos);
                HighlightDropTarget(pos);
            }
        }

        private void WheelItems_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_emptySlotCandidate != null)
            {
                _emptySlotCandidate = null;
                EndDrag();
                if (e.ChangedButton == MouseButton.Left)
                {
                    AddSlotRequested?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            if (_dragSource == null)
            {
                return;
            }

            var pos = e.GetPosition(WheelItems);
            var wasDragging = _isDragging;
            var source = _dragSource;
            var isLeftRelease = e.ChangedButton == MouseButton.Left;
            EndDrag();

            if (wasDragging)
            {
                if (isLeftRelease
                    && Vm != null
                    && source.Slot != null
                    && Vm.TryResolveDropPosition(pos.X, pos.Y, out var targetPosition))
                {
                    Vm.Reorder(source.Slot, targetPosition);
                }
            }
            else if (isLeftRelease && source.Slot != null)
            {
                EditRequested?.Invoke(this, new SlotWheelActionEventArgs(source.Slot));
            }
        }

        private void EndDrag()
        {
            WheelItems.ReleaseMouseCapture();
            if (_dragSource != null)
            {
                _dragSource.IsDragging = false;
            }

            _dragSource = null;
            _isDragging = false;
            Cursor = Cursors.Arrow;

            if (_dragGhost != null)
            {
                DragLayer.Children.Remove(_dragGhost);
                _dragGhost = null;
            }

            ClearDropTargets();
        }

        private void ShowDragGhost(WheelSlotItem item)
        {
            var orb = new SlotOrb
            {
                IconKey = item.IconKey ?? string.Empty,
                Label = item.Label ?? string.Empty,
                Size = 44,
                Width = 44,
                Height = 44,
                ShowActiveGlow = false
            };

            _dragGhost = new Border
            {
                Child = orb,
                Opacity = 0.92,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.08, 1.08)
            };

            DragLayer.Children.Add(_dragGhost);
        }

        private void PositionDragGhost(Point pos)
        {
            if (_dragGhost == null)
            {
                return;
            }

            Canvas.SetLeft(_dragGhost, pos.X - 22);
            Canvas.SetTop(_dragGhost, pos.Y - 22);
        }

        private void HighlightDropTarget(Point pos)
        {
            ClearDropTargets();
            if (Vm == null || !Vm.TryResolveDropPosition(pos.X, pos.Y, out var targetPosition))
            {
                return;
            }

            var target = Vm.Items.FirstOrDefault(item => item.Position == targetPosition);
            if (target != null)
            {
                target.IsDropTarget = true;
            }
        }

        private void ClearDropTargets()
        {
            if (Vm == null)
            {
                return;
            }

            foreach (var item in Vm.Items)
            {
                if (item.IsDropTarget)
                {
                    item.IsDropTarget = false;
                }
            }
        }

        private void HoverEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe
                && fe.Tag is WheelSlotItem item
                && item.Slot != null)
            {
                EditRequested?.Invoke(this, new SlotWheelActionEventArgs(item.Slot));
            }
        }

        private void HoverDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe
                && fe.Tag is WheelSlotItem item
                && item.Slot != null)
            {
                DeleteRequested?.Invoke(this, new SlotWheelActionEventArgs(item.Slot));
            }
        }

        private void WheelItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = FindItem(e.OriginalSource as DependencyObject);
            if (item == null || item.IsEmpty || item.Slot == null || Vm == null)
            {
                e.Handled = true;
                return;
            }

            WheelItems.ContextMenu = BuildContextMenu(item.Slot);
        }

        private static WheelSlotItem? FindItem(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is WheelSlotItem item)
                {
                    return item;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private ContextMenu BuildContextMenu(PluginSlot slot)
        {
            var loc = _services.GetRequiredService<ILocalizationService>();
            _contextMenuBuilder ??= new SlotContextMenuBuilder(loc);

            var menu = _contextMenuBuilder.Build(slot, Vm!);
            ApplyThemeToContextMenu(menu);

            _contextMenuBuilder.OnEdit = s => EditRequested?.Invoke(this, new SlotWheelActionEventArgs(s));
            _contextMenuBuilder.OnDelete = s => DeleteRequested?.Invoke(this, new SlotWheelActionEventArgs(s));

            return menu;
        }

        private void ApplyThemeToContextMenu(ContextMenu menu)
        {
            var themeService = _services.GetRequiredService<IThemeService>();
            var themeTarget = themeService.CurrentTheme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            var existingTheme = menu.Resources.MergedDictionaries.OfType<ThemesDictionary>().FirstOrDefault();
            if (existingTheme != null)
            {
                existingTheme.Theme = themeTarget;
            }
            else
            {
                menu.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = themeTarget });
            }

            if (!menu.Resources.MergedDictionaries.OfType<ControlsDictionary>().Any())
            {
                menu.Resources.MergedDictionaries.Add(new ControlsDictionary());
            }
        }
    }
}
