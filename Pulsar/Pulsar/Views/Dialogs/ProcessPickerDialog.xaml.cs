// [Path]: Pulsar/Pulsar/Views/Dialogs/ProcessPickerDialog.xaml.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.Services; // 引用包含 ProcessWindowInfo 的命名空间
using Pulsar.Services.Interfaces;
using Pulsar.Models;

namespace Pulsar.Views.Dialogs
{
    public partial class ProcessPickerDialog : Window
    {
        private readonly IWindowService _windowService;
        private List<ProcessWindowInfo> _allWindows;

        // 公开选中的结果
        public ProcessWindowInfo SelectedProcess { get; private set; }

        public ProcessPickerDialog(IWindowService windowService)
        {
            InitializeComponent();
            _windowService = windowService;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 显示加载状态...
            _allWindows = await _windowService.GetActiveWindowsAsync();
            ProcessList.ItemsSource = _allWindows;

            // 自动聚焦搜索框
            SearchBox.Focus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allWindows == null) return;

            var query = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                ProcessList.ItemsSource = _allWindows;
            }
            else
            {
                // 简单的客户端过滤
                var filtered = _allWindows.Where(w =>
                    (w.Title?.ToLower().Contains(query) == true) ||
                    (w.ProcessName?.ToLower().Contains(query) == true)
                ).ToList();
                ProcessList.ItemsSource = filtered;
            }
        }

        private void ProcessList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (ProcessList.SelectedItem is ProcessWindowInfo info)
            {
                SelectedProcess = info;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}