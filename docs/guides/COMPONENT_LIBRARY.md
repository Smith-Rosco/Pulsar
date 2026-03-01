# Pulsar Component Library Guide

**Status**: Published  
**Version**: v1.0.0  
**Last Updated**: 2026-03-01  
**Related Documents**: [AGENTS.md](../../AGENTS.md), [UI_BEST_PRACTICES.md](./UI_BEST_PRACTICES.md)

---

## Overview

This guide documents reusable UI components in the Pulsar project, promoting code reuse and consistency across the application.

## Available Components

### ExpandableCard

**Location**: `Views/Controls/ExpandableCard.xaml`

A parameterized, reusable card component that encapsulates common UI patterns for expandable content with actions and toggle controls.

**Status**: ✅ Production Ready
- ✅ Used in Plugins page
- ✅ Used in Slots page
- ✅ Build tested (0 warnings, 0 errors)

---

## ExpandableCard Usage

### Before Refactoring (~220 lines):
```xml
<ui:CardExpander Margin="0,0,0,8" IsExpanded="False">
    <ui:CardExpander.ContextMenu>
        <ContextMenu>
            <!-- 右键菜单定义 -->
        </ContextMenu>
    </ui:CardExpander.ContextMenu>
    
    <ui:CardExpander.Header>
        <Grid>
            <controls:JellyOrb IconKey="{Binding Icon}"/>
            <StackPanel>
                <TextBlock Text="{Binding Name}"/>
                <Border><!-- Core Badge --></Border>
                <TextBlock Text="{Binding HealthBadge}"/>
                <ProgressBar Value="{Binding HealthScore}"/>
            </StackPanel>
            <ui:Button Command="{Binding ConfigureCommand}"/>
            <ui:Button Command="{Binding ViewLogsCommand}"/>
            <ui:ToggleSwitch IsChecked="{Binding IsEnabled}"/>
        </Grid>
    </ui:CardExpander.Header>
    
    <StackPanel>
        <!-- 展开内容 -->
    </StackPanel>
</ui:CardExpander>
```

### 改造后 (使用 ExpandableCard ~100 行):
```xml
<controls:ExpandableCard 
    IconKey="{Binding Icon}"
    Title="{Binding Name}"
    IsToggleEnabled="{Binding IsEnabled, Mode=TwoWay}"
    CanToggle="{Binding CanDisable}"
    PrimaryActionCommand="{Binding ConfigureCommand}"
    PrimaryActionIcon="Settings24"
    PrimaryActionVisibility="{Binding HasSettings, Converter={StaticResource BoolToVis}}"
    SecondaryActionCommand="{Binding ViewLogsCommand}"
    SecondaryActionIcon="DocumentText24"
    SecondaryActionVisibility="{Binding IsViewLogsVisible, Converter={StaticResource BooleanToVisibilityConverter}}"
    CardContextMenu="{StaticResource PluginContextMenu}">
    
    <!-- 自定义头部内容: Badges + Health Progress -->
    <controls:ExpandableCard.HeaderContent>
        <StackPanel Orientation="Horizontal">
            <!-- Core Badge -->
            <Border Visibility="{Binding CanDisable, Converter={StaticResource InverseBoolToVisConverter}}"
                    Background="{DynamicResource SystemFillColorCautionBackgroundBrush}"
                    CornerRadius="4" Padding="4,1" Margin="0,0,8,0">
                <TextBlock Text="Core" FontSize="10"/>
            </Border>
            
            <!-- Health Badge -->
            <TextBlock Text="{Binding HealthBadge}" FontSize="14" Margin="0,0,8,0"/>
            
            <!-- Health Progress Bar -->
            <TextBlock Text="Health:" FontSize="11" Margin="0,0,6,0"/>
            <ProgressBar Value="{Binding HealthReport.HealthScore}" 
                         Maximum="100" Width="60" Height="4"
                         Foreground="{Binding HealthScoreColor, Converter={StaticResource HexToBrushConverter}}"/>
            <TextBlock Text="{Binding HealthScoreText}" FontSize="11" FontWeight="SemiBold"/>
        </StackPanel>
    </controls:ExpandableCard.HeaderContent>
    
    <!-- 展开内容: 统计数据 -->
    <controls:ExpandableCard.ExpandedContent>
        <StackPanel>
            <TextBlock Text="{Binding Description}" FontSize="12" Margin="0,0,0,12"/>
            
            <!-- Statistics Row -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                <StackPanel Orientation="Horizontal" Margin="0,0,24,0">
                    <ui:SymbolIcon Symbol="ChartMultiple24" FontSize="14"/>
                    <TextBlock Text="{Binding UsageSummary}" FontSize="12"/>
                </StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,24,0">
                    <ui:SymbolIcon Symbol="Folder24" FontSize="14"/>
                    <TextBlock Text="{Binding ProfilesSummary}" FontSize="12"/>
                </StackPanel>
                <StackPanel Orientation="Horizontal">
                    <ui:SymbolIcon Symbol="Clock24" FontSize="14"/>
                    <TextBlock Text="{Binding LastUsedSummary}" FontSize="12"/>
                </StackPanel>
            </StackPanel>
            
            <!-- Performance Metrics -->
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="⚡" FontSize="12"/>
                <TextBlock Text="{Binding AvgExecutionTimeText}" FontSize="12"/>
                <TextBlock Text="✓" FontSize="12" Margin="24,0,0,0"/>
                <TextBlock Text="{Binding SuccessRateText}" FontSize="12"/>
            </StackPanel>
        </StackPanel>
    </controls:ExpandableCard.ExpandedContent>
</controls:ExpandableCard>
```

## 重构步骤

### 1. 准备工作
- 确保 ExpandableCard.xaml 和 ExpandableCard.xaml.cs 已添加到项目
- 确保 Page 中已引用 `xmlns:controls="clr-namespace:Pulsar.Views.Controls"`

### 2. 定义 Context Menu (在 Page.Resources 中)
```xml
<ContextMenu x:Key="PluginContextMenu">
    <ContextMenu.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ControlsDictionary/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </ContextMenu.Resources>
    <MenuItem Header="Configure" Command="{Binding ConfigureCommand}">
        <MenuItem.Icon><ui:SymbolIcon Symbol="Settings24"/></MenuItem.Icon>
    </MenuItem>
    <MenuItem Header="View Logs" Command="{Binding ViewLogsCommand}">
        <MenuItem.Icon><ui:SymbolIcon Symbol="DocumentText24"/></MenuItem.Icon>
    </MenuItem>
    <Separator/>
    <MenuItem Header="Refresh Analytics" Command="{Binding RefreshAnalyticsCommand}">
        <MenuItem.Icon><ui:SymbolIcon Symbol="ArrowClockwise24"/></MenuItem.Icon>
    </MenuItem>
</ContextMenu>
```

### 3. 替换 DataTemplate
找到 `<ItemsControl.ItemTemplate>` 中的 `<DataTemplate>`,用上面的 ExpandableCard 代码替换整个 `<ui:CardExpander>` 块。

### 4. 测试构建
```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
```

## 实际收益

| 指标 | 改造前 | 改造后 | 改进 |
|------|--------|--------|------|
| **Plugins 页面代码行数** | 554 行 | 513 行 | **7.4% ↓** |
| **Slots 页面代码行数** | 520 行 | 512 行 | **1.5% ↓** |
| **总代码行数** | 1074 行 | 1025 行 | **4.6% ↓** |
| **维护文件数** | 2 个页面 | 1 个控件 + 2 个页面 | 集中化维护 |
| **代码重复度** | 高（卡片结构重复） | 低（统一使用 ExpandableCard） | **显著改善** |
| **一致性** | 手动同步 | 自动一致 | **100% 一致** |

### 关键改进点

1. **代码复用**: 卡片头部结构（Icon + Title + Actions + Toggle）完全复用
2. **维护效率**: 修改卡片样式只需更新 ExpandableCard.xaml
3. **扩展性**: 新增页面可直接使用 ExpandableCard，无需重写
4. **一致性**: 所有卡片的交互行为、动画、样式完全统一

### 注意事项

虽然代码行数减少不如预期（原因：添加了 Context Menu 定义和更详细的 HeaderContent），但重构带来的真正价值在于：
- **可维护性**: 集中化管理卡片组件
- **可扩展性**: 未来新增页面可快速复用
- **一致性**: 避免手动同步导致的不一致

## 已完成

✅ 1. Plugins 页面已重构使用 ExpandableCard
✅ 2. Slots 页面已重构使用 ExpandableCard（保留拖拽功能）
✅ 3. 构建测试通过（0 警告，0 错误）

## 使用指南

### ExpandableCard 属性说明

- `IconKey`: JellyOrb 图标标识符（支持 Emoji、图片路径、文本）
- `Title`: 卡片标题
- `Subtitle`: 可选副标题
- `HeaderContent`: 自定义头部内容（如 Badges、进度条）
- `ExpandedContent`: 展开后的内容
- `PrimaryActionCommand/Icon/Tooltip/Visibility`: 主操作按钮
- `SecondaryActionCommand/Icon/Tooltip/Visibility`: 次操作按钮
- `IsToggleEnabled`: 切换开关状态（双向绑定）
- `CanToggle`: 是否允许切换
- `IsToggleVisible`: 是否显示切换开关
- `CardContextMenu`: 右键菜单

### 最佳实践

- 右键菜单需要在 Page.Resources 中定义为 StaticResource
- HeaderContent 和 ExpandedContent 使用 ContentPresenter，支持任意自定义内容
- 如需更多操作按钮，可扩展 ExpandableCard 添加 TertiaryActionCommand

---

**状态**: ✅ 重构完成
**实际工作量**: 约 1 小时
**实际节省**: 49 行 XAML 代码 + 显著提升可维护性
