# Lesson: 插件页本地化静默回退英文（漏传 ILocalizationService + 约定键源失配）

> 2026-09-03 · 中文设定下插件页仍显示英文 · 修复 + 回归测试

## 症状

中文语言设定下，插件页卡片仍显示英文：

1. **插件名是英文**（如 "App Switch" 而非 "应用切换"）——用户主诉。
2. 插件**描述**也是英文（如 "Switch to an existing app…"）。
3. 同类问题：卡片 **Category** 副标题英文（"Productivity"）、健康状态 **ToolTip** 显示英文枚举名（"Healthy"）、外部插件作者行前缀硬编码 `by {0}`。

## 根因

### 1. 构造 PluginViewModel 时漏传 `ILocalizationService`

`PluginManagerViewModel.LoadPlugins()` 手工 `new PluginViewModel(...)` 传了 9 个参数，
唯独漏掉了第 10 个可选尾参 `ILocalizationService? localizationService = null`。
结果 `BuiltInPluginDisplayModel.FromMetadata(metadata, null)` 收到 `null`，
`DisplayName` / `Description` 走 `loc != null ? … : 原文` 的降级分支 → **静默回退英文原文**。
resx 键、`PluginLocalization` 助手、DI 注册全都在，唯独接线处断掉——最隐蔽的一类本地化 bug。

```
LoadPlugins() → new PluginViewModel(…, 缺 _loc) → FromMetadata(metadata, null) → 英文回退
```

### 2. 描述约定键的"键源"与 resx 实际键失配

`LocalizePluginDescription` 用**描述文本**推导键（`Plugin.Description.{AlphaNumOnly(描述)}`），
但 resx 里 6 个 `Plugin.Description.*` 键有 5 个是按**插件显示名**建立的
（`Plugin.Description.AppSwitch` 等），仅 Web Scripts 误用了整段描述文本建了怪键
（`Plugin.Description.Runcustomscriptsinlegacy…`）。即使接好线，描述也永远命不中。

### 3. 同类绕过

- `CategoryLabel` 未走本地化；健康状态 ToolTip/自动化名直接绑定 `HealthReport.Status` 枚举；
- 作者行硬编码 `StringFormat='by {0}'`，而 `Settings.ExternalPlugins.ByAuthorFormat` 双语键早已存在却从未被引用（死键）。

## 修复

1. **接线**：`PluginManagerViewModel` 构造参数改为必选 `ILocalizationService localizationService` 并转发给 `PluginViewModel`（单构造点、非 DI 注册，改动安全）。**把"漏传即静默降级"从可选尾参结构上消灭**——关键本地化依赖应为必选。
2. **键源对齐**：`LocalizePluginDescription(loc, description, displayName)` 改为按**插件显示名**推导键；Web Scripts 怪键重命名为 `Plugin.Description.WebScripts`（双语 resx 同步）。
3. **同类收尾**（沿用同一套 `Plugin.*.{AlphaNumOnly}` 约定）：
   - 新增 `PluginLocalization.LocalizePluginCategory`，`BuiltInPluginDisplayModel` 本地化 `CategoryLabel`；resx 加 `Plugin.Category.{General,Productivity,Credentials,Apps,Automation,System,Web}`。
   - 新增 `PluginViewModel.HealthStatusText`（`Plugin.Health.{Healthy,Warning,Critical,Unused,Disabled}`），XAML ToolTip/自动化名改绑该属性。
   - 外部作者行改为 `<Run Text="{lex:Locale Settings.ExternalPlugins.ByAuthor}"/>` + `<Run Text="{Binding Author}"/>`，复用既有键。

## 验证

- `Pulsar.Tests/ViewModels/Settings/PluginManagerViewModelLocalizationTests.cs`：6 个回归测试
  （Name / Description / 英文回退 / 描述键按名推导 / Category / HealthStatusText），全部绿。
- 全量测试 903 通过，`dotnet build` 0 警告 0 错误。
- 注意：`dotnet test` 构建时若 Pulsar 正在运行会锁 `Pulsar.exe`，用 `-p:UseAppHost=false` 规避。

## 教训

- 约定式本地化的**键源必须单一**，且与 resx 里**实际存在的键**对齐；建键时先核对既有键的命名依据（这里是"显示名"而非"描述文本"）。
- DI/手工构造中的**可选尾参**是静默降级的温床：参数类型本身可为 null 时，漏传不报错、只回退。对"必须有"的依赖，设必选参数比靠纪律更可靠。
- 排查顺序建议：resx 有键 → 助手表面对 → 怀疑"接线/时序"；本类 bug 的反馈回路（真实 resx + 真实插件名的单测）能在毫秒级坐实根因。
