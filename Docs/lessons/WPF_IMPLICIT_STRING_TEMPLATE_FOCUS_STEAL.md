# WPF 隐式 String DataTemplate 的全局焦点劫持（Tab 头不可点击）

**日期**: 2026-09-12
**症状**: 设置页 → 插件标签页，「内置 / 外部」两个 TabItem 标签头用鼠标真实点击无效，标签永不切换。控件可见、enabled、UIA 层无遮挡。

## 根因

`Themes/DialogTemplates.xaml` 中有一个**无键（隐式）**DataTemplate：

```xml
<DataTemplate DataType="{x:Type sys:String}">
    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
        <TextBlock Text="{Binding}" .../>
    </ScrollViewer>
</DataTemplate>
```

该字典在 `App.xaml` 全局合并。**隐式 `DataType=String` 模板对全应用所有"用 ContentPresenter 呈现字符串"的位点生效**——不止对话框消息体，还包括 `TabItem.Header`、Expander 头、菜单头等一切字符串头。

于是 TabItem 的 Header（一个普通 string）被渲染成「**可聚焦的 ScrollViewer**」。WPF 的 TabItem 选中逻辑是**基于焦点的**（dotnet/wpf `TabItem.OnMouseLeftButtonDown` → `SetFocus()` → `OnPreviewGotKeyboardFocus` → `SetCurrentValueInternal(IsSelectedProperty, true)`）。点击标签头时键盘焦点被 header 里的 ScrollViewer 截走，选中契约断裂 → `IsSelected` 永不置位 → 标签"点不动"。

## 为什么难查（取证路径）

1. 视觉上无任何遮挡（截图正常）；UIA 树里 TabItem `enabled=True` 且 bounds 无重叠 → 静态分析全部排除遮挡类假设。
2. E2E 真实鼠标点击 + 断言「外部标签内容物化」确立可复现红灯（`Pulsar.E2E` workflow，非 `--low-interference` 模式以保留命中测试保真）。
3. Page 级 `PreviewMouseDown` 探针证明命中测试正确（OriginalSource=头部 TextBlock，screen 坐标与 UIA bounds 吻合）。
4. `GotKeyboardFocus` 探针抓到决定性签名：点击后 `NewFocus=ScrollViewer(chain=...ContentSite ← ...TabItem)` 而非 TabItem；且 TabItem 视觉树 dump 显示 header ContentSite 的子级是 ScrollViewer+ScrollBar——**header 对象被模板替换**。
5. `headerType=System.String` + 资源链排查 → 锁定隐式 String 模板。

## 修复

模板根 ScrollViewer 加 `Focusable="False"`：

```xml
<ScrollViewer ... Focusable="False">
```

- 渲染型文本永远不需要键盘焦点；对话框消息体的**滚轮滚动不依赖焦点**，功能无损。
- 一处修复覆盖**整个缺陷类**：所有被该模板包裹的字符串渲染位点（今天断 Tab 头，明天可能断 Expander/菜单头）同时痊愈。

## 守卫

`Pulsar/Pulsar.Tests/UI/ImplicitStringTemplateFocusGuardTests.cs`：扫描全部源码 XAML，任何隐式 `DataType=String` DataTemplate 的根元素必须 `Focusable="False"`；匹配集为空时 fail-fast（防改名后守卫空转）。已做变异检查（移除属性 → 恰好红且报对位置）。

## 泛化教训

- **隐式（无键）DataTemplate 面向 BCL 基础类型（String/object 等）= 全应用作用域**。写之前先问：这个模板会被多少不相干的字符串渲染位点消费？模板里每个可聚焦/可交互元素都是散布全局的行为地雷。
- WPF `TabItem`（现代 .NET）的选中是焦点驱动不是鼠标捕获驱动——任何干扰焦点流的模板/样式都会让标签"看起来正常但点不动"。
- 排查 WPF "点不动" 缺陷的顺序：先 E2E 真实点击取红灯 → Page 级 `PreviewMouseDown` 验命中测试 → `GotKeyboardFocus` 探针看焦点实际落点 → 视觉树 dump 看对象身份。全部信号都在运行时，静态盯着 XAML 猜不出来。
