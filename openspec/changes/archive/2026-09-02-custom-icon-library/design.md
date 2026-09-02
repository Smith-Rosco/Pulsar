## Context

`IconHelper` 是静态类,`GetIconFromPath` 只处理 PNG/ICO/JPG/BMP(直载)与 EXE/LNK(提取),带 `ConcurrentDictionary` 缓存;`IconPickerViewModel` 已是 slot/profile 共用的图标选取入口,代码库已有 `Microsoft.Win32.OpenFileDialog` 使用先例(见 SettingsViewModel.cs:984 / IconPickerViewModel.cs:138)。`IconKey` 持久化为字符串引用。需求契约见 specs;此处只讲怎么落地。

## Goals / Non-Goals

**Goals:**
- `IconHelper` 支持 `.svg` 直载(解析 `SvgPathData` → `DrawingImage`),沿用静态缓存。
- 新增 `CustomIconStore` 持久化到 `%AppData%\Pulsar\CustomIcons\`,支持导入/列表/删除/按 key 解析,跨重启可用。
- `IconPicker` 增加「导入自定义图标」入口,导入后立即可选。

**Non-Goals:**
- 不做渲染器形态体系(独立 change `radial-style-renderers`)。
- 不做批量图标管理 UI(重命名/分类)、无图标裁剪/调色。
- 不改 `Profiles.json` 数据模型;图标引用仍是 `IconKey` 字符串。

## Decisions

### D1: `IconHelper` SVG 直载用 `Geometry.Parse` + 现有静态缓存

`.svg` 分支:读文本 → 正则提取 `d="..."` path data → `Geometry.Parse` → 构建 `GeometryDrawing`(fill 用当前前景色 + 透明背景)→ `DrawingImage` → `Freeze()`。文件路径作为缓存 key 复用 `GetIconFromPath` 的 `ConcurrentDictionary`(与 `SaveIconToCache`/`GetGlyph` 同款模式)。解析失败返回 null(调用方回退原图标)。

- **为什么**:`Geometry.Parse` 是 WPF 内置 SVG path 语法解析器,零第三方依赖;`DrawingImage` 天然支持 path 渲染,免去 bitmap 光栅化步骤。SVG 的 fill/stroke 语义简化处理:只认首个 `<path d>` + 单色填充,覆盖 Pulsar 图标使用场景(单色 icon)。
- **风险**:复杂 SVG(渐变/多 path/非 path 元素)不支持 → 设计上明确「单 path 单色」支持范围,失败回退,不抛异常。

### D2: `CustomIconStore` 用「key = 时间戳+随机」扁平存储

`Services/Interfaces/ICustomIconStore.cs` + `Services/CustomIconStore.cs`:根目录 `%AppData%\Pulsar\CustomIcons\`。`Import(sourcePath)` 复制文件到 store 目录并返回相对文件名作为 key(`pulsar-icon-{yyyyMMddHHmmss}-{4位随机}.{ext}`);`GetIcon(key)` 调 `IconHelper.GetIconFromPath(join(dir, key))`(天然复用 SVG/光栅两条路径);`List()` 扫目录返回 key+预览;`Delete(key)` 删文件。目录不存在时懒创建。无 DB、无 JSON 索引——文件名即索引,重启后文件仍在,满足「跨重启持久」需求。

- **为什么**:图标以文件形式落在用户目录,零元数据维护;文件名 key 与 `IconKey` 字符串引用天然一致;`IconHelper` 复用避免二次实现解析。
- **替代方案**:SQLite/JSON 索引(过度,图标数量少);GUID key(不可读,调试难)。
- **风险**:用户手动删文件 → `GetIcon` 返回 null,`List` 跳过缺失文件,静默自愈。

### D3: `IconPickerViewModel` 注入 `ICustomIconStore`,导入用 `OpenFileDialog`(现成先例)

`IconPickerViewModel` 构造可选注入 `ICustomIconStore`(不注入则导入入口隐藏,保持现有测试/调用兼容)。新增 `ImportIconCommand`:`OpenFileDialog`(Filter 含 SVG/PNG/ICO/JPG/BMP,参照 SettingsViewModel.cs:984 先例)→ `store.Import` → 刷新自定义图标列表 → `SelectedKey` 设为新 key 触发预览。`IconPickerContent.xaml` 加「导入自定义图标」按钮 + 自定义图标区。slot/profile 保存时 `IconKey` 已是文件名 key 字符串,无需改动数据模型。

- **为什么**:`IconPickerViewModel` 已是 slot/profile 共用的图标入口,在这里加导入覆盖所有使用方;`OpenFileDialog` 是代码库既有模式,不引入新对话框依赖。
- **风险**:`IconPickerViewModel` 构造签名变化波及多处调用 → 用可选参数 + 无 store 时隐藏导入,保持旧调用零改动。

## Risks / Trade-offs

- **SVG 解析范围窄** → 明确单 path 单色契约,复杂 SVG 返回 null 走回退;测试覆盖合法/非法 path。
- **图标目录占用/损坏** → `GetIcon`/`List` 对缺失/损坏文件静默跳过,不抛异常;日志走 `ILogger`。
- **文件系统竞态** → `Import`/`Delete` 用唯一文件名(时间戳+随机)避免覆盖;并发调用低,简单 try/catch 即可。

## Migration Plan

1. 纯新增,无破坏性迁移:`IconHelper` 加 `.svg` 分支(不影响既有扩展名路径),`CustomIconStore` 新增注册。
2. 部署顺序:IconHelper SVG → CustomIconStore + 接口 + DI → IconPicker 导入 UI。
3. 回滚:`CustomIconStore` 目录可整体删除无副作用;去掉 picker 导入入口即回退。

## Open Questions

- SVG 是否需要对 `fill` 属性颜色做解析(而非固定前景色)?——Pulsar 图标为单色风格,首期固定前景色;按色解析留后续,故列为 deferrable。
