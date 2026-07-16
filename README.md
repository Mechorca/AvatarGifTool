# AvatarGifTool

`AvatarGifTool` 是一个基于 WzComparerR2-CMS 的冒险岛纸娃娃 GIF 导出工具。它读取本地 `Base.wz`，根据角色模板和外观 ID 合成角色动作 GIF，支持普通动作拼图、染色预览、道具搜索和背景自定义。

准备上传的仓库地址：

```text
https://github.com/Mechorca/AvatarGifTool
```

## 致谢

本项目基于并改造自：

```text
https://github.com/Jancy-49/WzComparerR2-CMS
```

WZ 读取、StringLinker、角色纸娃娃渲染、装备 Tooltip、GIF 编码和大量底层渲染逻辑都来自 WzComparerR2-CMS 及其上游项目。本仓库主要新增和整理了独立的 `AvatarGifTool` 程序、简化 UI、GIF 导出流程、搜索辅助、染色逻辑以及相关修复。

原项目 README 已保留在：

```text
docs/upstream/WzComparerR2-CMS-README.md
```

## 功能

- 选择并记住本地 `Data/Base/Base.wz` 路径。
- 模板栏可输入外观 / 物品 ID，顺序不限。
- 自动识别肤色、脸型、发型、衣服、武器、披风等外观类型。
- 支持用中文关键字或 ID 片段搜索外观。
- 搜索结果分页显示，双击装备结果可查看原版道具说明 UI。
- 普通模式可导出多个动作拼成的 GIF。
- 染色模式默认按 30 度色相步进导出 12 格染色预览。
- 染色模式支持同时染多个 Gear 外观。
- 支持饱和度、亮度微调。
- 支持纯色、透明和图片背景。
- 支持导出动作自定义。
- 运行异常会在程序同目录输出 `error.log`。
- 配置保存到 `AvatarGifTool.config`，包括窗口大小、Base.wz 路径、历史搭配、背景和导出设置。

## 需要的游戏数据

程序本身不包含任何冒险岛游戏数据。首次使用时需要手动选择游戏目录下的：

```text
Data/Base/Base.wz
```

示例：

```text
C:\Program Files\...\冒险岛online\mxd\Data\Base\Base.wz
```

请只使用你有权访问和使用的游戏数据文件。

## 图形界面使用

1. 启动 `AvatarGifTool.exe`。
2. 点击 `选择...`，选择游戏目录下的 `Data/Base/Base.wz`。
3. 在 `模板` 输入框中填入外观 ID 或已经能被程序识别到的精确名称。
4. ID 使用英文逗号分隔。
5. 选择 `普通模式` 或 `染色模式`。
6. 染色模式下，在 `Gear` 输入框填入一个或多个需要染色的外观 ID / 名称。
7. 点击 `导出设置`，选择普通模式动作或染色模式动作。
8. 点击 `导出`。

导出的 GIF 会生成在程序同目录下。文件名会根据外观名称、导出模式和动作名自动拼接，避免普通模式和染色模式互相覆盖。

## 模板输入规则

模板栏可以混合输入：

```text
2000,54506,30000,1051001,1104025
```

也可以在校验后使用程序替换出的名称。只要名称能唯一匹配到搜索索引，程序会在导出时自动还原成对应 ID。

识别规则：

- 肤色、脸型、发型会自动识别，顺序不限。
- 如果没有显式填写肤色，程序会尝试使用默认肤色。
- 外观装备会按装备位置生效。
- 如果模板中已有某个位置的外观，后续输入同位置外观时会覆盖前面的外观。
- 戒指类外观不会互相覆盖。
- 染色模式下 `Gear` 只能填写可装备外观，且所有填写的 Gear 会一起染色。

## 普通模式

默认动作：

```text
stand1, swingO1, swingO2, shoot1, jump, walk1
```

普通模式会把所选动作拼成一张 GIF。布局会尽量接近正方形：

| 动作数量 | 布局 |
| --- | --- |
| 6 | 3 x 2 |
| 5 | 3 + 2 |
| 4 | 2 x 2 |
| 3 | 2 + 1 |
| 2 | 横向并排 |
| 1 | 单格 |

## 染色模式

染色模式只使用一个动作，默认是 `stand1`。

默认色相步进为 `30`，会生成：

```text
0, 30, 60, ..., 330
```

共 12 个小 GIF 单元，并按 3 列排列成一张大 GIF。

染色微调：

- 饱和度：`-99` 到 `+99`
- 亮度：`-99` 到 `+99`
- 默认都是 `0`

## 背景设置

GIF 背景支持：

- 纯白背景
- 调色盘选择颜色
- 灰度预设
- 透明背景
- PNG / JPG 背景图片

如果背景图片分辨率小于本次导出的 GIF 画布，程序会按覆盖方式放大并居中裁剪。

## 命令行使用

普通模式示例：

```powershell
AvatarGifTool.exe --base-wz "D:\Maple\Base.wz" `
  --template "1051001,30000,2000,1072153,20000" `
  --output "D:\out\avatar.gif"
```

染色模式示例：

```powershell
AvatarGifTool.exe --base-wz "D:\Maple\Base.wz" `
  --template "1051001,30000,2000,20000" `
  --gear "1053345,1072153" `
  --output "D:\out\dye.gif" `
  --dye --saturation 12 --brightness -6
```

指定普通模式动作：

```powershell
AvatarGifTool.exe --base-wz "D:\Maple\Base.wz" `
  --template "1051001,30000,2000,1072153,20000" `
  --actions "stand1,jump,walk1" `
  --output "D:\out\avatar.gif"
```

常用参数：

| 参数 | 说明 |
| --- | --- |
| `--base-wz` | `Base.wz` 完整路径。 |
| `--template` | 模板外观 ID，肤色、脸型、发型自动识别。 |
| `--gear` | 额外 Gear ID，染色模式必填。 |
| `--output` | 输出 GIF 路径。 |
| `--mode normal|dye` | 导出模式。 |
| `--dye` | 等同于 `--mode dye`。 |
| `--actions` | 普通模式动作列表。 |
| `--dye-action` | 染色模式使用的单个动作。 |
| `--saturation` | 饱和度偏移，范围 `-99..99`。 |
| `--brightness` | 亮度偏移，范围 `-99..99`。 |
| `--bg-color` | 背景色，支持 `#RRGGBB`、`#AARRGGBB` 或 `transparent`。 |
| `--bg-image` | PNG / JPG 背景图片路径。 |
| `--cell-gap` | 单元格间距。 |
| `--canvas-padding` | 画布边距。 |

## 编译环境

目标运行环境：

- Windows 10 x64
- Windows 11 x64

开发 / 编译环境：

- .NET 8 SDK
- Windows 上编译最直接
- 非 Windows 环境编译时需要启用 Windows targeting

普通构建：

```powershell
dotnet build AvatarGifTool\AvatarGifTool.csproj -c Release -p:Platform=x64
```

发布自包含单文件 EXE：

```powershell
dotnet publish AvatarGifTool\AvatarGifTool.csproj `
  -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:SelfContained=true `
  -p:EnableWindowsTargeting=true `
  -p:Platform=x64 `
  -o AvatarGifTool\dist
```

发布后会在 `AvatarGifTool/dist` 下生成类似下面的文件：

```text
AvatarGifTool_v2.12.1.exe
```

使用 `SelfContained=true` 发布时，目标机器不需要单独安装 .NET 运行时。

## 本地构建配置

`AvatarGifTool/build.env` 是本机专用配置文件，里面可能包含本地 dotnet 路径，因此不会上传到仓库。

仓库里只保留示例：

```text
AvatarGifTool/build.env.example
```

如果需要在 Linux 或其他环境中复用构建脚本，可以复制这个示例并改成本机路径。

## 目录说明

本仓库只纳入编译和维护 `AvatarGifTool` 所需的原项目模块，不是完整的 WzComparerR2-CMS 镜像。

```text
AvatarGifTool/           独立 GIF 导出工具，包含 UI 和 CLI
WzComparerR2/            原项目主程序，当前工具复用其渲染基础设施
WzComparerR2.Common/     通用渲染、Tooltip、CharaSim 相关代码
WzComparerR2.PluginBase/ WZ 查找和插件基础设施
WzComparerR2.WzLib/      WZ 文件读取库
CharaSimResource/        纸娃娃渲染资源
References/              原项目依赖的第三方 DLL
Build/                   MSBuild 公共配置
docs/upstream/           原项目 README 备份
```

## 不应上传的内容

`.gitignore` 已经排除了常见缓存和本地文件。上传前建议检查：

```bash
git status --ignored
```

不要提交以下内容：

- `bin/`
- `obj/`
- `AvatarGifTool/dist/`
- `AvatarGifTool/build.env`
- `AvatarGifTool/AvatarGifTool.config`
- `AvatarGifTool/error.log`
- `.vs/`
- 本地游戏数据，例如 `Base.wz`

## 准备上传到 GitHub

目标远程仓库：

```bash
git remote add origin https://github.com/Mechorca/AvatarGifTool.git
```

首次提交示例：

```bash
git init
git add .
git status
git commit -m "Initial AvatarGifTool source release"
git branch -M main
git remote add origin https://github.com/Mechorca/AvatarGifTool.git
git push -u origin main
```

如果远程仓库已经有内容，请先拉取或确认是否需要强制覆盖，避免误删远端内容。

## 许可证

本仓库保留原项目 MIT License。详见：

```text
LICENSE
```

`References/` 下的第三方二进制依赖继承自原 WzComparerR2-CMS 项目，分别受其对应许可证约束。
