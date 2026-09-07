# BookmarkletRunner TestScripts

Bookmarklet 测试脚本与测试页。**不参与构建**（csproj 无引用），仅供人工/模拟器测试。

| 文件 | 用途 |
|---|---|
| `Flow.js` / `Flow_v2.js` | 完整流程演示脚本（v2 为当前版） |
| `test.js` | 最小连通性测试 |
| `test_complex.js` | 复杂选择器场景 |
| `test_error.js` | 错误路径验证 |
| `temp.html` | 本地测试页 |

编写规范（节奏/下载/BOM/DOM 契约）见 `Docs/lessons/DEMO_SCRIPT_AUTHORING_PITFALLS.md`。目录归属依据：openspec `2026-06-16-project-structure-cleanup`。
