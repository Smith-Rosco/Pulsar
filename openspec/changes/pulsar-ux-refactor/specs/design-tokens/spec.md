## ADDED Requirements

### Requirement: 间距 Token 定义
Theme 资源文件 SHALL 在 Theme.Dark.xaml 和 Theme.Light.xaml 中定义统一的间距 Token，供 UI 组件通过 StaticResource 引用，所有 Token key 使用 `Pulsar.Spacing.` 前缀。

#### 间距 Token 规范
| Key | Value |
|-----|-------|
| Pulsar.Spacing.XS | 4 |
| Pulsar.Spacing.SM | 8 |
| Pulsar.Spacing.MD | 16 |
| Pulsar.Spacing.LG | 24 |
| Pulsar.Spacing.XL | 32 |

#### Scenario: 控件引用间距 Token
- **WHEN** XAML 控件使用 `{StaticResource Pulsar.Spacing.MD}` 设置 Margin
- **THEN** 运行时正确解析为对应数值，不抛出 ResourceNotFoundException

### Requirement: 动效时长 Token 定义
Theme 资源文件 SHALL 定义统一的动效时长和缓动曲线 Token，供动画 Storyboard 引用，所有 Token key 使用 `Pulsar.Animation.` 前缀。

#### 动效 Token 规范
| Key | Value |
|-----|-------|
| Pulsar.Animation.Duration.Fast | 0:0:0.15 |
| Pulsar.Animation.Duration.Normal | 0:0:0.25 |
| Pulsar.Animation.Duration.Slow | 0:0:0.35 |

#### Scenario: 动画引用时长 Token
- **WHEN** Storyboard 中 DoubleAnimation 的 Duration 绑定到 Token
- **THEN** 运行时动画以正确时长执行

### Requirement: Token 仅在涉及文件中替换
本次变更 SHALL 仅在本 change 涉及的 XAML 文件中将硬编码间距/时长值替换为 Token 引用，不做全量替换。
