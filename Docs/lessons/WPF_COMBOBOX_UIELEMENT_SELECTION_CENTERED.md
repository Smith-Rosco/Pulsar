# WPF ComboBox：UIElement 选中内容收起态居中（VisualBrush Rectangle 机制）

## 症状

设置页 ComboBox 收起态文字对齐不一致：`ItemsSource` + `DisplayMemberPath`（字符串项）的
组合框文字贴左（x≈18），显式声明 `<ComboBoxItem>`（内含 `TextBlock`/`StackPanel`）的组合框
文字居中悬空（x≈89-92）。箭头（chevron）位置正常贴右。Wpf.Ui 4.2.0 与 4.3.0 渲染**完全一致**
（E2E 截图像素级 diff 为零），与库版本无关。

## 误判路径（为什么折腾了三轮）

1. **误判一：改 `ComboBox.HorizontalContentAlignment="Left"`** → 严重回归。
   Wpf.Ui 的 ComboBox 模板里，「内容列 + chevron 列 + 全宽 ToggleButton 点击层」包在一个
   `HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"` 的内层 Grid 里
   （运行时从 4.3.0 程序集 `XamlWriter.Save` 导出模板确认，默认值 `Stretch`）。设为 Left
   会把整个 grid 缩成内容宽 → 箭头左移 + 点击区缩小。
2. **误判二：`ItemContainerStyle` 设 `HorizontalContentAlignment=Left`** → 零效果。
   XAML 里**显式声明**的 `ComboBoxItem` 不吃 `ItemContainerStyle`（那只作用于 ItemContainerGenerator
   生成的容器）。像素级 diff 为零。
3. **误判三：直接在 `ComboBoxItem` 上设 `HorizontalContentAlignment="Left"`** → 依然零效果。
   因为居中根本不是 item 的对齐属性造成的（见根因）。

## 真根因（探针复现 + 视觉树 dump 确认）

WPF **核心**（不是 Wpf.Ui）的 `ComboBox` 在收起态用 `SelectionBoxItem` 交给模板里的
`PART_ContentPresenter`：

- 选中项内容是**字符串** → `SelectionBoxItem` 就是字符串 → ContentPresenter 自动生成的
  `TextBlock` 可撑满（Stretch）→ 文字贴左。
- 选中项内容是 **UIElement**（显式 ComboBoxItem 的常见写法）→ `SelectionBoxItem` 是 WPF
  自动生成的**固定宽度（= 内容期望宽）`Rectangle` + VisualBrush 快照**（避免把下拉项从
  Popup 里"搬走"重宿主）。固定宽 + 默认 Stretch 对齐 = **居中**。

探针视觉树证据：
```
ContentPresenter x=11.3 w=248.7 ha=Stretch content=Rectangle
  Rectangle x=111.3 w=48 ha=Stretch     ← 固定宽 48 + Stretch = 居中
```

## 正确修法

不改模板、不改 `HorizontalContentAlignment`。在 `Loaded` / `SelectionChanged` 时找到
`PART_ContentPresenter` 的直接子 `Rectangle`，设 `HorizontalAlignment = Left`：

`Pulsar/Helpers/ComboBoxSelectionLeftAlign.cs` —— 可继承附加属性
`ComboBoxSelectionLeftAlign.EnableLeftAlign`，页面根节点设一次即覆盖整棵子树：

```xml
xmlns:helpers="clr-namespace:Pulsar.Helpers"
<Grid helpers:ComboBoxSelectionLeftAlign.EnableLeftAlign="True">
```

已在 `SettingsGeneralPage` / `SettingsAnalyticsPage` 启用。点击区不受影响的保证：只改了
`IsHitTestVisible="False"` 的内容展示层内部的对齐，ToggleButton（点击层）横跨全宽未动。

## 经验

- WPF 的 `SelectionBoxItem` 不是"选中项"本身——UIElement 内容会被替换成 VisualBrush Rectangle。
- 排查此类问题别猜：写 20 行探针（`new Window` + `ControlsDictionary` + 视觉树 dump +
  `TransformToAncestor` 取 x 坐标）10 分钟就能拿到决定性证据；三轮"合理猜测"每轮都要一次
  完整构建 + E2E 循环，成本反而更高，且引入过一次真实回归。
- `XamlWriter.Save(ControlTemplate)` 可直接导出 NuGet 包里模板的**运行时真身**，不要依赖
  GitHub 源码（main 分支 ≠ 你安装的版本）。
