using System.Windows;
using Pulsar.Services.Interfaces; // 需要你确保 IWindowService 可用
using Pulsar.Features.Pki.Services;

namespace Pulsar.Views.Dialogs
{
    public partial class QuickSecretsDialog : Window
    {
        private readonly IWindowService _windowService;
        private readonly CredentialsManager _credManager;

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
            _credManager = new CredentialsManager(); // 可以注入，这里简单new
            TxtLabel.Focus();
        }

        private void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            var picker = new ProcessPickerDialog(_windowService);
            // 简单复用当前窗口的主题资源
            picker.Resources = this.Resources;
            picker.Owner = this;

            if (picker.ShowDialog() == true && picker.SelectedProcess != null)
            {
                TxtProcess.Text = picker.SelectedProcess.ProcessName;
                if (string.IsNullOrEmpty(TxtLabel.Text))
                {
                    TxtLabel.Text = picker.SelectedProcess.Title;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 简单的验证
            if (string.IsNullOrWhiteSpace(TxtLabel.Text))
            {
                // [Fix] 显式指定 System.Windows.MessageBox
                System.Windows.MessageBox.Show("Please enter a label.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. 获取明文
            string rawPassword = TxtPassword.Password;

            // 2. 加密
            ResultEncryptedData = _credManager.Encrypt(rawPassword);

            // 3. 设置其他属性
            ResultLabel = TxtLabel.Text;
            ResultProcess = TxtProcess.Text;
            ResultAccount = TxtAccount.Text;
            ResultAutoEnter = ChkAutoEnter.IsChecked == true;

            DialogResult = true;
            Close();
        }
    }
}