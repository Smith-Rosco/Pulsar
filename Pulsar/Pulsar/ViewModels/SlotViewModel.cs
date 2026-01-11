using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Helpers;
using System.IO;
using System.Windows.Media;

namespace Pulsar.ViewModels
{
    public class SlotViewModel : ObservableObject
    {
        private int _slotIndex;
        private double _x;
        private double _y;
        private double _size;
        private string _label = string.Empty;
        private bool _isActive;

        // --- 图标系统字段 ---
        private string _iconGlyph = string.Empty;
        private ImageSource? _iconImage;
        private bool _hasIcon;
        private bool _isImageMode; // True = 显示图片, False = 显示字体

        public SlotViewModel(int index, double x, double y, double size)
        {
            SlotIndex = index;
            X = x;
            Y = y;
            Size = size;
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int SlotIndex
        {
            get => _slotIndex;
            set => SetProperty(ref _slotIndex, value);
        }

        // --- 图标系统属性 ---

        /// <summary>
        /// 字体图标字符 (Segoe Fluent Icons)
        /// </summary>
        public string IconGlyph
        {
            get => _iconGlyph;
            private set => SetProperty(ref _iconGlyph, value);
        }

        /// <summary>
        /// 图片图标源 (用于 EXE/文件路径)
        /// </summary>
        public ImageSource? IconImage
        {
            get => _iconImage;
            private set => SetProperty(ref _iconImage, value);
        }

        /// <summary>
        /// UI 状态位：是否显示图片模式 (True=Image, False=Font/Text)
        /// </summary>
        public bool IsImageMode
        {
            get => _isImageMode;
            private set => SetProperty(ref _isImageMode, value);
        }

        /// <summary>
        /// 是否有任何类型的图标
        /// </summary>
        public bool HasIcon
        {
            get => _hasIcon;
            private set => SetProperty(ref _hasIcon, value);
        }

        // --- 核心逻辑 ---

        /// <summary>
        /// 智能加载图标数据：根据 iconKey 自动判断是路径还是字体编码
        /// </summary>
        /// <param name="iconKey">配置中的原始字符串 (e.g., "E700" or "C:\App.exe")</param>
        public void LoadIconData(string iconKey)
        {
            // 1. 重置所有图标状态
            IconGlyph = string.Empty;
            IconImage = null;
            IsImageMode = false;
            HasIcon = false;

            if (string.IsNullOrWhiteSpace(iconKey)) return;

            // 2. 尝试解析为文件路径 (包含路径分隔符或扩展名)
            if (iconKey.Contains(Path.DirectorySeparatorChar) || iconKey.Contains('.'))
            {
                // 调用 IconHelper 提取文件图标
                var image = IconHelper.GetIconFromPath(iconKey);
                if (image != null)
                {
                    IconImage = image;
                    IsImageMode = true;
                    HasIcon = true;
                    return; // 成功提取图片，直接返回
                }
            }

            // 3. 尝试解析为字体 Hex (兜底逻辑)
            // 即使看起来像路径但文件不存在，或者只是普通字符串，也尝试作为 Glyph 处理
            var glyph = IconHelper.GetGlyph(iconKey);
            if (!string.IsNullOrEmpty(glyph))
            {
                IconGlyph = glyph;
                IsImageMode = false;
                HasIcon = true;
            }
        }
    }
}