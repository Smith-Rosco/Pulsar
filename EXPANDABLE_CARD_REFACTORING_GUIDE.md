# ExpandableCard 重构指南

## 当前状态
- ✅ ExpandableCard 通用控件已创建
- ✅ AGENTS.md 已更新组件化最佳实践
- ⏳ Plugins 页面待重构
- ⏳ Slots 页面待重构

## 重构示例: Plugins 页面

### 改造前 (当前代码 ~220 行):
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

## 预期收益

| 指标 | 改造前 | 改造后 | 改进 |
|------|--------|--------|------|
| **Plugins 页面代码行数** | ~220 行 | ~100 行 | **55% ↓** |
| **Slots 页面代码行数** | ~200 行 | ~90 行 | **55% ↓** |
| **总代码行数** | 420 行 | 190 行 | **55% ↓** |
| **维护文件数** | 2 个 | 1 个 (ExpandableCard) | **50% ↓** |

## 下一步

1. 按照上述示例重构 Plugins 页面
2. 按照相同模式重构 Slots 页面
3. 测试构建并验证功能
4. 提交 Git 记录

## 注意事项

- ExpandableCard 的 `HeaderContent` 和 `ExpandedContent` 使用 `ContentPresenter`,支持任意自定义内容
- 如果需要更多操作按钮,可以扩展 ExpandableCard 添加 `TertiaryActionCommand`
- 右键菜单需要在 Page.Resources 中定义为 StaticResource,然后通过 `CardContextMenu` 属性引用

---

**状态**: ExpandableCard 控件已就绪,等待应用到实际页面
**预计工作量**: 1-2 小时
**预计节省**: ~230 行 XAML 代码
