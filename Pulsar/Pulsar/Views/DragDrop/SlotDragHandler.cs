using System.Windows;
using System.Windows.Media;
using GongSolutions.Wpf.DragDrop;

namespace Pulsar.Views.DragDrop
{
    /// <summary>
    /// Restricts slot reorder dragging to the dedicated grip handle and
    /// dims the source card while a drag is in progress.
    /// </summary>
    public class SlotDragHandler : DefaultDragHandler
    {
        private const string HandleName = "SlotDragHandle";
        private FrameworkElement? _sourceItem;

        public override bool CanStartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.VisualSourceItem is not FrameworkElement item)
            {
                return false;
            }

            var handle = FindVisualChildByName(item, HandleName);
            if (handle == null)
            {
                return false;
            }

            var origin = handle.TranslatePoint(new Point(0, 0), item);
            var handleBounds = new Rect(origin, new Size(handle.ActualWidth, handle.ActualHeight));
            return handleBounds.Contains(dragInfo.PositionInDraggedItem);
        }

        public override void StartDrag(IDragInfo dragInfo)
        {
            base.StartDrag(dragInfo);
            _sourceItem = dragInfo.VisualSourceItem as FrameworkElement;
            if (_sourceItem != null)
            {
                _sourceItem.Opacity = 0.4;
            }
        }

        public override void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
        {
            RestoreSource();
            base.DragDropOperationFinished(operationResult, dragInfo);
        }

        public override void DragCancelled()
        {
            RestoreSource();
            base.DragCancelled();
        }

        private void RestoreSource()
        {
            if (_sourceItem != null)
            {
                _sourceItem.Opacity = 1.0;
                _sourceItem = null;
            }
        }

        private static FrameworkElement? FindVisualChildByName(DependencyObject parent, string name)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                {
                    return fe;
                }

                var nested = FindVisualChildByName(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
