// [Path]: Pulsar/GlobalUsings.cs

global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Linq;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Input;

// 1. 解决 UI 控件冲突 (WPF vs WinForms)
global using WpfApplication = System.Windows.Application;
global using WpfButton = System.Windows.Controls.Button;
global using FormsButton = System.Windows.Forms.Button;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// 2. [关键修复] 强制指派 Model 类型
// 这两行会覆盖 System.Windows.Forms 中的同名类型，彻底解决歧义
global using GridItem = Pulsar.Models.GridItem;
global using GridItemType = Pulsar.Models.Enums.GridItemType;

// [新增] 解决 UserControl 冲突
global using UserControl = System.Windows.Controls.UserControl;

