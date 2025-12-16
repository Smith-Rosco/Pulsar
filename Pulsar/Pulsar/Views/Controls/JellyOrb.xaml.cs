// [Path]: Pulsar/Views/Controls/JellyOrb.xaml.cs

using System.Windows.Controls; 

namespace Pulsar.Views.Controls
{
    // 方法 A: 使用全名
    // public partial class JellyOrb : System.Windows.Controls.UserControl 
    
    // 方法 B (推荐): 如果你在 GlobalUsings 里加了那行代码，这里直接写 UserControl 即可
    public partial class JellyOrb : UserControl
    {
        public JellyOrb()
        {
            InitializeComponent(); // <--- 必须有这一行！
        }
    }
}