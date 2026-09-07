# VBA 注入后「宏不可用」——`.bas` 首行 `Attribute VB_Name` 破坏完整编译

> 2026-09-07 · 来源：`Docs/demo/lesson2.md`（演示视频一《一键跑宏》录制排障）+ lesson.md §5。
> 修复 = `VbaModuleInjector` 剥离 Attribute 行（commit 2bd2085）+ 脚本文件去头。

## 症状

1. Pulsar vbarunner 插件跑 `format-report.bas` 报「操作失败。操作未完成。请重试或检查槽位设置。」
2. 插件日志（`com.pulsar.vbarunner-*.log`）：

   ```
   System.InvalidOperationException: Macro 'FormatReport' not found in the injected module.
    ---> System.Runtime.InteropServices.COMException (0x800A03EC):
        无法运行"Pulsar_57a2dfdf.FormatReport"宏。可能是因为该宏在此工作簿中不可用，或者所有的宏都被禁用。
   ```

3. **注入本身"成功"**：模块添加成功、`CountOfLines` 正常、`ProcStartLine('FormatReport')` 能查到——过程已注册。
4. 同一 Excel 进程、同一工作簿里，**最小空宏注入 + `Application.Run` 成功**。
5. 对照机（另一台电脑、同版本插件、先前建好的脚本）同样操作正常 → 极易误判为「本机 Excel 配置问题」。

## 根因

### `AddFromString` 注入后 `Attribute VB_Name` 残留行破坏完整编译

- `.bas` 文件是 **VBE「导出模块」格式**，首行固定 `Attribute VB_Name = "..."`。
- `CodeModule.AddFromString()` 注入这行时**不报错、也不按它改名**，过程也能在解析层注册（`ProcStartLine` 可查）——表象完全正常。
- 但残留的属性行使模块在**首次执行触发完整编译**时失败，`Application.Run` 抛「宏不可用」（0x800A03EC）。
- **核心陷阱：`过程能注册 ≠ 模块能完整编译`。** `ProcStartLine` / `ProcCountLines` 读的是解析树；`Application.Run` 才触发全模块编译。只看注册结果会被误导成「内容没问题、怀疑配置」。

### 对照机为什么正常

另一台电脑的脚本文件是**不带 Attribute 头的「纯代码」保存格式**——差异在脚本文件本身，不在 Excel 配置。本机 Excel 侧核验均通过：`AccessVBOM=1`、`VBAWarnings=1`、`AutomationSecurity=Low`、无 Zone.Identifier、VBProject 未保护。

## 修复

1. **脚本文件层（立即生效）**：删除 `.bas` 首行 `Attribute VB_Name = "..."`，宏逻辑零改动。插件每次执行重新读文件，无需重启。
2. **插件代码层（治本）**：`VbaModuleInjector.cs` 在 `AddFromString` 前剥离全部属性行，此后任何 VBE 导出的 `.bas` 都能直接注入：

   ```csharp
   // Strip VBE-export module attribute lines (e.g. "Attribute VB_Name = \"Module1\"").
   // AddFromString does not process them, and their presence breaks full module
   // compilation, making Application.Run fail with 0x800A03EC.
   string sanitized = Regex.Replace(
       content,
       @"(?m)^\s*Attribute\s+VB_\w+\s*=.*$",
       string.Empty);
   component.CodeModule.AddFromString(sanitized);
   ```

## 排查方法论（怎么定位到的）

1. **COM 探针复现**：不经过 Pulsar，直接用 PowerShell 对同一 Excel 进程做「注入 + Run」，复现出完全一致的 `0x800A03EC` → 排除 Pulsar 进程/连接路径嫌疑。
2. **二分法**：把真实内容逐变量剥离后分别注入运行——

   | 变体 | 内容 | 结果 |
   |---|---|---|
   | B0 | 最小空宏（对照组） | ✓ 成功 |
   | B1 | 真实内容 | ✗ 宏不可用 |
   | **B2** | **真实内容 − `Attribute VB_Name` 行** | ✓ **成功** |
   | B3 | 去中文、保留 Attribute 行 | ✗ 宏不可用 |

   唯一变量锁定 `Attribute` 行，与中文/编码无关。
3. **对照机反证**：另一台电脑正常 → 聚焦「机器/文件差异」，最终落在脚本文件格式。

排障顺序建议：**先复现（探针/模拟器）→ 再隔离变量（二分）→ 再对照机反证**，不要在配置假设上反复打转。

## 排查入口

| 信号 | 位置 |
|---|---|
| `Macro 'X' not found in the injected module` | `%APPDATA%\Pulsar\Logs\Plugins\com.pulsar.vbarunner-*.log` |
| 内层 `0x800A03EC`「无法运行…宏…所有宏都被禁用」 | 同上（inner exception） |
| 脚本文件首行是否为 `Attribute VB_Name` | `%USERPROFILE%\Documents\Pulsar\Scripts\*.bas` |
| 工作簿/进程状态核验 | 注册表 `HKCU:\Software\Microsoft\Office\16.0\Excel\Security` |

## 附：VBA 类型判断顺序（同源踩坑，改宏时别踩回去）

- **必须先推断列类型、后清样式**：`NumberFormat` 清成 General 后，Excel COM 把日期单元格当普通 Double 返回（`IsDate(46255) = False`），日期列会被误判成数字列。
- **VBA 的 `IsDate("1,200") = True`**（带千分位的数字被当日期）：字符串必须先剥逗号判 `IsNumeric`，再判 `IsDate`，顺序不能反。

## 附：读日志中文乱码

`Get-Content` 读 Serilog 的 UTF-8 日志在 PowerShell 5.1 控制台（GBK）下中文显示为 `鎿嶄綔澶辫触` ——**读取端代码页问题，不是日志坏了**。用 `Get-Content -Encoding UTF8` 或 `pwsh` 读取；探针脚本输出尽量纯 ASCII。
