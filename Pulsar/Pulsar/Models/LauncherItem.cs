// [Path]: Pulsar/Pulsar/Models/LauncherItem.cs
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel; // 确保引用了 MVVM Toolkit

namespace Pulsar.Models
{
    // 假设 GridItemBase 继承自 ObservableObject
    public class LauncherItem : GridItemBase
    {
        private string _processName = string.Empty;
        public string ProcessName
        {
            get => _processName;
            set => SetProperty(ref _processName, value);
        }

        private string _exePath = string.Empty;
        // [New] 智能启动：当找不到窗口时，使用此路径启动程序
        public string ExePath
        {
            get => _exePath;
            set => SetProperty(ref _exePath, value);
        }

        private string _arguments = string.Empty;
        // [New] 启动参数
        public string Arguments
        {
            get => _arguments;
            set => SetProperty(ref _arguments, value);
        }

        private bool _matchTitle = false;
        public bool MatchTitle
        {
            get => _matchTitle;
            set => SetProperty(ref _matchTitle, value);
        }
    }
}