# E2E 截图必须提供 --run-id——落盘与防覆盖约定

**日期：** 2026-09-05  
**严重程度：** 低（E2E 脚手架可用性）  
**影响范围：** `Pulsar.E2E` 所有截图步骤、诊断产物收集  
**状态：** 已解决（含 run 目录 bug 修复记录）

---

## 问题描述

分析页 E2E 调试时发现两个连带的产物问题：

1. **截图不落盘**：`screenshot` 步骤执行成功、控制台也打印了文件名，但
   `artifacts/` 下找不到文件——截图只在提供 `--run-id <id>` 时才写入
   `artifacts\<runId>\`，不提供时静默丢弃（仅录制到内存/丢弃）。
2. **run 目录 bug**：即使提供了 `--run-id`，`WorkflowRunner` 未创建
   `artifacts\<runId>` 目录，截图/录屏保存必然失败（`DirectoryNotFoundException`）。

## 根本原因

- 截图/录屏是重量级产物，脚手架按 `--run-id` 决定是否保留（无 run-id 视为试运行）；
  但这个约定没有体现在输出信息里，失败时无提示。
- `WorkflowRunner` 启动步骤未预创建 `artifacts\<runId>` 目录，属纯遗漏。

## 解决方案

1. **总是传 `--run-id`，且用带语义后缀的唯一值**，避免多次运行互相覆盖：
   ```bash
   Pulsar.E2E.exe run --workflow Workflows\settings-analytics-data-dark.json \
     --app <Pulsar.exe> --artifacts artifacts --run-id 20260905-analytics-data-dark-v1
   ```
   截图按工作流内 `file` 字段命名落到 `artifacts\<run-id>\<file>`。
2. **`WorkflowRunner` 在启动时预创建 `artifacts\<run-id>`**（目录不存在则创建），
   修复截图/录屏必挂的 bug。
3. 诊断/复核时结合**像素统计**（裁剪区域 max/min 亮度）做确定性验证，不依赖人工看截图。

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Pulsar/Pulsar.E2E/Runner/WorkflowRunner.cs` | 预创建 `artifacts\<runId>` 目录（已修，勿重报） |
| `Pulsar/Pulsar.E2E/Driver/Capture.cs` | 截图仅在提供 run-id 时落盘（约定，勿改回） |
| `Pulsar/Pulsar.E2E/Workflows/settings-analytics-*-dark.json` | 截图步骤文件命名带语义后缀 |

## 架构教训

1. **脚手架约定要显式**：条件性行为（如"只有 run-id 才落盘"）必须在输出中可见，
   否则调试期会以为功能坏了。
2. **目录前置**：任何"往路径写文件"的步骤，先确保父目录存在；失败要 fail fast。
3. **产物可复现**：`--run-id` 语义化 + 文件后缀，使同一工作流多次运行的产物互不覆盖，
   便于回看与对比。
