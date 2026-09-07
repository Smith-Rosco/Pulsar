# Lesson: vbarunner 注入 VBA 宏报「宏不可用」——.bas 首行 `Attribute VB_Name` 破坏完整编译

> 2026-09-07 · 演示视频一《一键跑宏》录制排障中发现 · 修复 = `VbaModuleInjector` 剥离 Attribute 行 + 脚本文件去头（`format-report.bas` / `report-pack.bas`）

## 症状

1. Pulsar vbarunner 插件跑 `format-report.bas`（Slot1「一键跑宏」）报系统通知「操作失败。操作未完成。请重试或检查槽位设置。」
2. 插件日志（`com.pulsar.vbarunner-*.log`）：

   ```
   System.InvalidOperationException: Macro 'FormatReport' not found in the injected module.
    ---> System.Runtime.InteropServices.COMException (0x800A03EC):
        无法运行"Pulsar_57a2dfdf.FormatReport"宏。可能是因为该宏在此工作簿中不可用，或者所有的宏都被禁用。
   ```

3. **注入本身"成功"**：模块添加成功、`CountOfLines` 正常、`ProcStartLine('FormatReport')` 能查到（=3、60 行）——过程已注册。
4. 同一 Excel 进程、同一工作簿里，**最小空宏注入 + `Application.Run` 成功**（宏安全、注入→Run 链路当前可用）。
5. 对照机（另一台电脑、最新版软件、先前建好的脚本）同样操作正常 → 用户怀疑本机 Excel 配置。

## 根因

### `AddFromString` 注入后 `Attribute VB_Name` 残留行破坏完整编译

- `.bas` 文件是 **VBE「导出模块」格式**，首行固定 `Attribute VB_Name = "FormatReport"`。
- `CodeModule.AddFromString()` 注入这行时**不报错、也不按它改名**（模块名仍是注入时设置的 `Pulsar_<8hex>`），过程也能在解析层注册（`ProcStartLine` 可查）——表象完全正常。
- 但残留的属性行使模块在**首次执行触发完整编译**时失败，`Application.Run("Pulsar_xxx.FormatReport")` 于是抛「宏不可用」（0x800A03EC）。
- **核心陷阱：`过程能注册 ≠ 模块能完整编译`。** `ProcStartLine` / `ProcCountLines` 读的是解析树；`Application.Run` 才触发全模块编译。只看注册结果会被误导，误判成「内容没问题、怀疑配置」。

### 对照机为什么正常

另一台电脑的脚本文件是**不带 Attribute 头的「纯代码」保存格式**（或新建脚本时未走 VBE 导出），同样的插件代码自然没问题——**差异在脚本文件本身，不在 Excel 配置**。本机 Excel 侧核验均通过：`AccessVBOM=1`、`VBAWarnings=1`、`AutomationSecurity=Low`、`report-data.xlsx` 无 Zone.Identifier、VBProject 未保护（`Protection=0`）。

## 修复

1. **脚本文件层（立即生效）**：删除 `.bas` 首行 `Attribute VB_Name = "..."`，宏逻辑零改动。Pulsar 插件每次执行重新读文件，无需重启。
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

## 排查方法论（这次怎么定位到的）

1. **COM 探针复现**：不经过 Pulsar，直接用 PowerShell 对同一 Excel 进程做「注入 + Run」，复现出与 Pulsar 完全一致的 `0x800A03EC` → 排除 Pulsar 进程/连接路径（`ComConnectionManager`、STA Dispatcher、FocusManager 激活等）是嫌疑。
2. **二分法**：把真实内容逐变量剥离后分别注入运行——

   | 变体 | 内容 | 结果 |
   |---|---|---|
   | B0 | 最小空宏（对照组） | ✓ 成功 |
   | B1 | 真实内容（MsgBox→StatusBar） | ✗ 宏不可用 |
   | **B2** | **真实内容 − `Attribute VB_Name` 行** | ✓ **成功** |
   | B3 | 去中文、保留 Attribute 行 | ✗ 宏不可用 |

   唯一变量锁定 `Attribute` 行，与中文/编码无关。
3. **对照机反证**：另一台电脑正常 → 聚焦「机器/文件差异」，最终落在脚本文件格式（VBE 导出 vs 纯代码）。

排障顺序建议：**先复现（探针/模拟器）→ 再隔离变量（二分）→ 再对照机反证**，不要在配置假设上反复打转。

## 排查入口

| 信号 | 位置 |
|---|---|
| `Macro 'X' not found in the injected module` | `%APPDATA%\Pulsar\Logs\Plugins\com.pulsar.vbarunner-*.log` |
| 内层 `0x800A03EC`「无法运行…宏…所有宏都被禁用」 | 同上（inner exception） |
| 脚本文件首行是否为 `Attribute VB_Name` | `%USERPROFILE%\Documents\Pulsar\Scripts\*.bas` |
| 可复跑探针 | `%TEMP%\vba_probe3.ps1`（真实注入+Run）、`%TEMP%\vba_bisect.ps1`（二分法） |
| 工作簿/进程状态核验 | `%TEMP%\pulsar_tail.ps1`、注册表 `HKCU:\Software\Microsoft\Office\16.0\Excel\Security` |

## 附：读日志中文乱码的坑

用 `Get-Content` 读 Serilog 写的 `pulsar-*.log`（UTF-8）在 PowerShell 5.1 控制台（默认 GBK）下，中文会显示成 `鎿嶄綔澶辫触` 之类——**这是读取端的代码页问题，不是日志坏了**。用 `Get-Content -Encoding UTF8` 或 `pwsh` 读取即可。排查脚本同理：含中文的输出在 GBK 控制台会乱码，探针脚本输出尽量用纯 ASCII 或显式 `-Encoding UTF8`。
