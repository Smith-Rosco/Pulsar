# QA Checklist — renderer-plugin-registry (任务 4.4)

样例插件：`Pulsar/Samples/NeonRendererPlugin/`（Neon 渲染器：虚线霓虹外环 + 柔和模糊高亮）。

## 准备

1. 构建样例插件（产物 ZIP 在 `Pulsar/Samples/NeonRendererPlugin/bin/Release/NeonRendererPlugin-1.0.0.zip`）：
   ```
   dotnet build Pulsar/Samples/NeonRendererPlugin/NeonRendererPlugin.csproj -c Release
   ```
2. 运行 Pulsar → 设置 → 外部插件 → **从文件安装** → 选择上面的 ZIP。
3. 安装向导会请求 `ui.render`（渲染器注册）权限 —— **选择授予**。

## 用例

- [ ] **A1 安装与注册**：安装后启用插件，重启或重新加载后，设置 → 通用 → 外观 的「渲染器」下拉出现 `Neon` 项（内置三项之下）。
- [ ] **A2 选择生效**：选择 Neon → 保存 → 唤出环形菜单：外圈为虚线霓虹环、内环细线，槽位激活时有模糊辉光。Dark / Light 两主题下均正常（画笔来自 token，无硬编码颜色）。
- [ ] **A3 持久化**：重启 Pulsar，`Profiles.json` 的 `RadialRenderer` 仍为 `Neon`，菜单依旧 Neon 形态。
- [ ] **A4 禁用回落**：禁用 Neon 插件（不改渲染器配置）→ 再次唤出菜单：**安全回落 Default 形态，无异常、无空白**。日志出现 `Removed N plugin renderer(s) on disable`。
- [ ] **A5 重新启用**：重新启用插件 → 菜单恢复 Neon 形态（下次唤出即生效）。
- [ ] **A6 卸载清理**：卸载插件 → `Registrations` 清空（日志确认），菜单保持 Default 回落。
- [ ] **A7 权限拒绝**（可选，需手工制造）：在 `Profiles.json` 中移除该插件的 `ui.render` 授权再启用 → 插件启用但注册被拒（日志 `registration rejected`），下拉中无 Neon、菜单无异常。
- [ ] **A8 保留 id 防遮蔽**（代码保障，抽查日志即可）：样例插件尝试注册 `Default` 会失败——由单测覆盖，人工无需构造。

## 通过标准

A1–A6 全部通过；A4/A5 为核心（回落不崩、恢复即时）。发现问题记录到本文件 Change History。
