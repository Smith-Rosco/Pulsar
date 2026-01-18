// [Path]: Pulsar/Pulsar/Views/Dialogs/QuickSecretsDialog.xaml.cs

using System.Windows;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Pki.Services;
using Pulsar.Features.Pki.Models;

namespace Pulsar.Views.Dialogs
{
    public partial class QuickSecretsDialog : Window
    {
        private readonly IWindowService _windowService;
        private readonly CredentialsManager _credManager;

        // 编辑模式下持有原始加密数据
        private string _originalEncryptedData;
        private bool _isEditMode = false;

        // 输出结果
        public string ResultLabel { get; private set; }
        public string ResultProcess { get; private set; }
        public string ResultAccount { get; private set; }
        public string ResultEncryptedData { get; private set; }
        public bool ResultAutoEnter { get; private set; }

        public QuickSecretsDialog(IWindowService windowService)
        {
            InitializeComponent();
            _windowService = windowService;
            _credManager = new CredentialsManager();
            TxtLabel.Focus();
        }

        /// <summary>
        /// 加载现有 Item 进行编辑
        /// </summary>
        public void LoadForEdit(SecretItem item)
        {
            if (item == null) return;

            _isEditMode = true;
            _originalEncryptedData = item.EncryptedData;

            Title = "Edit Secret";
            TxtLabel.Text = item.Label;
            TxtProcess.Text = item.TargetProcessName;
            TxtAccount.Text = item.Account;
            ChkAutoEnter.IsChecked = item.AutoEnter;

            // 显示“留空保持不变”的提示
            TxtPasswordHint.Visibility = Visibility.Visible;
            TxtPassword.Password = "";
        }

        private void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            var picker = new ProcessPickerDialog(_windowService);
            // 简单复用资源
            picker.Resources = this.Resources;
            picker.Owner = this;

            if (picker.ShowDialog() == true && picker.SelectedProcess != null)
            {
                TxtProcess.Text = picker.SelectedProcess.ProcessName;
                // 仅当 Label 为空时自动填充，避免覆盖用户已输入的内容
                if (string.IsNullOrEmpty(TxtLabel.Text))
                {
                    TxtLabel.Text = picker.SelectedProcess.Title;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 验证 Label (Account 允许为空)
            if (string.IsNullOrWhiteSpace(TxtLabel.Text))
            {
                System.Windows.MessageBox.Show("Please enter a label.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string rawPassword = TxtPassword.Password;

            // 处理密码逻辑
            if (_isEditMode && string.IsNullOrEmpty(rawPassword))
            {
                // 编辑模式且留空 -> 保持原密码 (使用旧的 Payload)
                ResultEncryptedData = _originalEncryptedData;
            }
            else
            {
                // 新增模式或输入了新密码 -> 加密新密码
                ResultEncryptedData = _credManager.Encrypt(rawPassword);
            }

            ResultLabel = TxtLabel.Text;
            ResultProcess = TxtProcess.Text;
            ResultAccount = TxtAccount.Text; // 允许为空
            ResultAutoEnter = ChkAutoEnter.IsChecked == true;

            DialogResult = true;
            Close();
        }
    }
}