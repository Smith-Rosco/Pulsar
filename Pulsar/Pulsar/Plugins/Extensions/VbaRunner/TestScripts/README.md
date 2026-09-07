# VbaRunner TestScripts

VBA 测试脚本（`.txt` 存储，运行时粘贴进 Excel VBE 执行）。**不参与构建**（csproj 无引用），仅供人工/模拟器测试。

| 文件 | 用途 |
|---|---|
| `FlowSentinel.txt` | 流程哨兵：全流程守护宏 |
| `FormatCurrentSheet.txt` | 当前工作表格式化（对齐仓库 `FormatCurrentSheet` 约定：环境保存/逆序恢复、`Main` 入口 + `@Macro` 指令） |
| `GenerateReversionFlow.txt` | 逆版本流程生成 |
| `MacroFlow_DiffTool_Optimized.txt` | 宏流程 diff 工具（优化版） |
| `test_vba.txt` | 最小连通性测试 |

编写规范与坑点见 `Docs/lessons/VBA_INJECT_ATTRIBUTE_LINE_BREAKS_COMPILE.md` 与 `Docs/guides/VBARUNNER_AI_SCRIPTING.md`。目录归属依据：openspec `2026-06-16-project-structure-cleanup`。
