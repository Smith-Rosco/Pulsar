# User Manual & Release Assets（用户手册 + GitHub Release 资产）

## Why

M1 验收标准要求「全新机器可安装、文档可无痛上手、GitHub Release 资产齐备」。Change 2（叙事）与 Change 3/4（更新/打包）落地后，缺最后一块：面向非技术目标用户（财务/运营/行政白领）的上手文档，以及把双形态产物、校验清单、发布说明组装成完整 GitHub Release 的收尾流程。

## What Changes

- **用户手册（中英）**：面向办公白领（非开发者视角）——安装与首次运行、第一次配置轮盘（10 分钟完成第一个自动化，M2 验收口径）、三支柱场景教程（Excel 跑宏 / 网页脚本 / 安全登录）、窗口切换、常见问题（SmartScreen 提示、管理员权限、卸载数据保留）。
- **GitHub Release 资产组装**：Release 说明（基于 Change 2 的模板 + CHANGELOG 提炼）、双形态产物 + SHA256 清单、手册链接、系统要求；首个 Release（v1.10.0 或发布时版本号）发布清单。
- **发布 checklist 文档**：`Docs/ops/RELEASE_CHECKLIST.md`——发布前门槛（构建绿/测试绿/QA 过）、资产清单、发布后动作，后续版本可复用。
- 手册入口接线：About 页/README 链接到手册；首次运行引导（已有 first-launch-setup-wizard 能力）尾部加入手册入口（仅链接，不改引导流程）。

## Capabilities

（纯文档与发布运营变更，不引入或修改任何系统能力需求——首启引导只加一个外链。故声明 `skip_specs: true`。）

## Impact

- **Affected code**: 仅两处链接性改动：`Views/Pages/SettingsAboutPage.xaml`（手册链接）、首启完成页外链；`README.md` 文档区链接更新。
- **New assets**: `Docs/manual/`（中英手册，Markdown 起步）、GitHub Release 草稿与资产清单。
- **Dependencies**: 依赖 Change 4 产物（无产物则 Release 无法组装）；依赖 Change 2 的发布说明模板与截图。
- **Out of scope**: 手册站/在线文档托管、视频字幕翻译、社区运营（M3 传播）。
