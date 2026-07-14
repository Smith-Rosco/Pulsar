# 🎨 动态自适应布局 - 测试指南

## 快速测试（5 分钟）

### 测试 1: 4 Slots（紧凑饱满）
```
1. 打开 Settings (Ctrl+,)
2. Launcher → Slots Per Page → 4
3. 点击 Save
4. 触发 Radial Menu (Ctrl+Shift+Q)

预期效果：
✅ Slot 明显变大（58px）
✅ Center 明显变大（80px）
✅ 整体更紧凑，不再空旷
✅ 平滑动画过渡
```

### 测试 2: 12 Slots（疏朗清晰）
```
1. Settings → Slots Per Page → 12
2. 点击 Save
3. 触发 Radial Menu

预期效果：
✅ Slot 明显变小（42px）
✅ Center 明显变小（60px）
✅ 整体更疏朗，不再拥挤
✅ 所有 slot 易于点击
✅ 平滑动画过渡
```

### 测试 3: 动画流畅性
```
快速切换：4 → 8 → 12 → 6

预期效果：
✅ 无跳跃感
✅ 大小平滑变化
✅ 60 FPS 流畅
```

---

## 视觉对比表

| Slot Count | Slot 大小 | Center 大小 | 半径 | 视觉效果 |
|------------|-----------|-------------|------|----------|
| 4 slots    | 58px ↑    | 80px ↑      | 75px ↓ | 紧凑饱满 ✅ |
| 6 slots    | 52px ↑    | 72px ↑      | 85px ↓ | 平衡舒适 ✅ |
| 8 slots    | 50px      | 70px        | 90px   | 标准参考 ✅ |
| 10 slots   | 46px ↓    | 65px ↓      | 105px ↑ | 疏朗清晰 ✅ |
| 12 slots   | 42px ↓    | 60px ↓      | 120px ↑ | 精致紧凑 ✅ |

---

## 日志验证

查看 `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`：

**成功示例（4 → 12）**:
```
[UpdateSlotsPerPage] Reconfiguring layout: 4 → 12 slots
[UpdateSlotsPerPage] Layout updated - Slots: 12, SlotSize: 42.0px (Δ-16.0px), CenterSize: 60.0px (Δ-20.0px), Radius: 120.0px (Δ+45.0px), Angle: 30.0°/slot, Density: 0.95
```

**视觉密度指标**:
- 0.85 - 1.15: ✅ 最佳范围
- < 0.85: ⚠️ 过于稀疏
- > 1.15: ⚠️ 过于拥挤

---

## 完整测试矩阵

| 测试场景 | 步骤 | 预期结果 | 状态 |
|---------|------|----------|------|
| 4 slots 视觉 | 设置 4 slots → 触发菜单 | 紧凑饱满，不空旷 | ⬜ |
| 6 slots 视觉 | 设置 6 slots → 触发菜单 | 平衡舒适 | ⬜ |
| 8 slots 视觉 | 设置 8 slots → 触发菜单 | 标准参考（不变） | ⬜ |
| 10 slots 视觉 | 设置 10 slots → 触发菜单 | 疏朗清晰 | ⬜ |
| 12 slots 视觉 | 设置 12 slots → 触发菜单 | 精致紧凑，易点击 | ⬜ |
| 动画流畅性 | 快速切换 4→8→12 | 平滑过渡，无跳跃 | ⬜ |
| SubMenu 动态 | 10 slots + 多窗口 | 显示 10 个窗口 | ⬜ |
| 性能测试 | 所有配置 | 60 FPS 保持 | ⬜ |
| 持久化 | 重启应用 | 配置保持 | ⬜ |

---

## 问题排查

### 问题 1: Slot 大小没有变化
**检查**:
- 是否保存了 Settings？
- 日志中是否有 `UpdateSlotsPerPage` 消息？
- `_currentSlotSize` 是否更新？

### 问题 2: 动画不流畅
**检查**:
- CPU 使用率是否正常（< 5%）？
- 动画定时器是否运行（16ms 间隔）？
- 是否有其他进程占用资源？

### 问题 3: 视觉密度仍不平衡
**检查**:
- 日志中的 Density 值是否在 0.85-1.15 范围？
- Slot 大小是否正确（4 slots: 58px, 12 slots: 42px）？
- 半径是否正确（4 slots: 75px, 12 slots: 120px）？

---

## 成功标准

✅ **视觉质量**: 所有 slot count 都美观平衡  
✅ **操作体验**: 所有 slot 易于点击  
✅ **动画效果**: 平滑流畅，无卡顿  
✅ **性能表现**: 60 FPS 保持  
✅ **日志验证**: Density 在最佳范围

---

**测试完成后，请在上方测试矩阵中打勾 ✅**
