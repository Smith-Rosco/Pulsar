# AI 脚本编写提示词 — VBA Runner（Excel/WPS 宏）

> 用途：把下面的提示词整段粘给任意 AI（ChatGPT / Claude / Copilot 等），再补上你的需求，即可得到一个能被 Pulsar「Excel 宏执行器」直接运行的 `.bas` 脚本。
> 深度参考：[VBARUNNER_AI_SCRIPTING.md](./VBARUNNER_AI_SCRIPTING.md)（完整指令系统与模板）、
> [lessons/VBA_INJECT_ATTRIBUTE_LINE_BREAKS_COMPILE.md](../lessons/VBA_INJECT_ATTRIBUTE_LINE_BREAKS_COMPILE.md)（排障实录）。
> 范本：`Pulsar/Pulsar/Plugins/Extensions/VbaRunner/TestScripts/FormatCurrentSheet.txt`。

---

## 提示词（整段复制，替换【需求】后发给 AI）

```text
你是资深 Excel VBA 工程师。请为我编写一个将在 Pulsar VbaRunner 插件中执行的 .bas 脚本。

【需求】<在此描述你想自动化的操作，例如：整理当前表的格式并冻结表头>

【硬性约束——违反任何一条都会执行失败】
1. 输出纯代码，禁止输出 `Attribute VB_Name = "..."` 等 Attribute 属性行
   （VBE「导出模块」格式的首行），否则注入后模块完整编译失败，
   报 0x800A03EC「无法运行宏……宏不可用」。过程能注册 ≠ 模块能编译。
2. 入口过程必须是 `Public Sub Main()`（无参数），文件头注释加 `' @Macro: Main`。
   插件会把脚本作为临时模块注入活动工作簿、立即调用入口、随后删除模块——
   不要依赖 Public 变量或模块跨次运行存活。
3. 前提：用户 Excel 信任中心已开启「信任对 VBA 工程对象模型的访问」；
   脚本不得引用外部 DLL，需要对象时用 CreateObject 后期绑定。
4. 兼容 Excel 与 WPS，只用两者都支持的标准 VBA。

【工程约定】
5. 环境卫生：进入时保存 Application 状态（ScreenUpdating / Calculation /
   EnableEvents / DisplayAlerts / DisplayPageBreaks），在统一出口 CleanExit
   中逆序恢复——宏跑完不允许把 Excel 留在"手动计算 + 事件关闭"状态。
6. 性能：主体逻辑前关 ScreenUpdating 与事件、Calculation 改手动；
   AutoFit 前关 DisplayPageBreaks；AutoFit 后给列宽设上限（如 60），
   防个别超长文本把列撑爆。
7. 类型判断顺序（顺序不能反）：
   - 先推断列类型、后清样式：NumberFormat 清成 General 后，COM 会把日期
     单元格当 Double 返回（IsDate(46255) = False），日期列会被误判成数字列；
   - 字符串先剥千分位逗号判 IsNumeric，再判 IsDate
     （VBA 的 IsDate("1,200") = True，带千分位数字会被当日期）。
8. 幂等且不破坏：先清样式再套样式，连跑两次结果一致；不删行、不排序、
   不合并单元格；文本型数字转数字加保护（不转 0 开头编码、不转 12 位以上长号码）。
9. 错误处理：On Error 统一走 Fail → CleanExit；恢复段用 On Error Resume Next
   包住（恢复自身不再抛错）；任何错误都不向上抛给插件，只在状态栏或
   MsgBox 提示后干净退出。

【指令系统（按需使用，放在文件头 100 行以内的注释里）】
- '@Runner: ShowSheetSelector —— 需要用户选工作表时；
  此时入口签名改为 Public Sub Main(targetSheetName As String)
- '@Requires: Sheet=名称 / Cell=A1 / Range=A1:B10 与 '@OnMissing: Setup ——
  前提校验，缺前提时自动先跑 Setup
- '@SheetFilter: exclude:_* 与 '@AutoSelectSingle: true —— 选择器过滤与单表自动选中

【交付要求】
- 输出完整 .bas 文件内容（一个代码块给全，不要分段）；
- 附 3~5 条人工验证步骤（在 Excel 里怎么确认跑对了）；
- 需求涉及具体列名 / 工作表名 / 文件路径时，先问我确认，不要假设。
```

---

## 使用提示

1. AI 返回后**自查首行**：不得以 `Attribute` 开头（这是最高频翻车点）。
2. 保存为 `.bas`（UTF-8），在 Pulsar 槽位里填**绝对路径**——槽位参数不做 `%USERPROFILE%` 等环境变量展开。
3. 执行报「宏不可用 / 操作失败」时，先查插件日志
   `%APPDATA%\Pulsar\Logs\Plugins\com.pulsar.vbarunner-*.log`，再对照上面 lessons 文档的排查入口。
