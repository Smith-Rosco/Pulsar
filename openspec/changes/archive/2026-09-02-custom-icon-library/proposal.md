## Why

`IconHelper` 目前只支持 PNG/ICO/JPG/BMP 与 EXE/LNK 图标提取,不支持 SVG 矢量解析,也没有「用户自定义图标库」持久化——用户无法把自有图标(SVG/PNG)用于 slot/profile(参考 StarPie 的 SVG 导入 + 自定义图标库能力)。`IconPicker` 已是 slot/profile 共用的图标选取入口,正好在此补上导入能力。

## What Changes

- **SVG 图标解析**(roadmap 3.4.4):
  - `IconHelper` 扩展 `.svg` 直载:解析 `SvgPathData`(`Geometry.Parse`)→ 渲染为 `DrawingImage`,复用既有缓存。
- **自定义图标库**(roadmap 3.4.4):
  - 新增 `CustomIconStore`:把用户导入的图标(SVG/PNG/ICO)持久化到 `%AppData%\Pulsar\CustomIcons\`,提供导入、列表、删除、按 key 解析图标;文件名即 key,零元数据索引。
  - `IconPicker` 新增「导入自定义图标」入口,导入后作为 `IconKey` 可选值参与 slot/profile 图标,持久化不触 `Profiles.json` 数据模型(仍是字符串 key)。
- **不包含**:渲染器形态体系(独立 change `radial-style-renderers`);批量图标管理 UI(重命名/分类)。

## Capabilities

### New Capabilities

- `custom-icon-library`: 自定义图标库 —— `IconHelper` SVG 解析、`CustomIconStore` 持久化到 `%AppData%\Pulsar\CustomIcons\`、图标选择器导入入口。

### Modified Capabilities

(无既有能力需求变化;图标加载/选择器行为均为新增。)

## Impact

- **Affected code**:
  - `Helpers/IconHelper.cs`(SVG 解析分支)。
  - `Services/CustomIconStore.cs`(新)+ `Services/Interfaces/ICustomIconStore.cs`(新)。
  - `ViewModels/Dialogs/IconPickerViewModel.cs`(导入命令,可选注入 store)、`Views/Dialogs/Contents/IconPickerContent.xaml`(导入按钮/列表)。
  - `App.xaml.cs` `ConfigureServices`(注册 `ICustomIconStore`)。
  - `Resources/Strings.resx` + `Strings.zh-CN.resx`(图标导入本地化键)。
- **Dependencies**: `CustomIconStore` 复用 `IconHelper.GetIconFromPath`(天然覆盖 SVG/光栅两条路径);`IconPickerViewModel` 可选注入保持现有测试/调用零改动。
- **No breaking changes**: 图标引用仍是 `IconKey` 字符串;未导入 store 时 picker 行为与现在一致。
