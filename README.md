# AvatarGifTool

`AvatarGifTool` 是一个冒险岛纸娃娃 GIF 导出工具。程序读取本地 `Data/Base/Base.wz`，根据输入的角色外观生成 GIF，适合快速预览发型、脸型、肤色、衣服、武器、披风等搭配效果。

## 功能

- 通过 ID 或物品名输入外观，顺序不限。
- 自动识别肤色、脸型、发型和装备位置。
- 普通模式：按所选动作生成角色 GIF 拼图。
- 染色模式：对 Gear 外观做 12 档色相预览，并支持饱和度、亮度微调。
- 支持中文关键字或 ID 片段搜索外观。
- 搜索结果双击可查看接近原版的道具说明 UI。
- 支持纯色、透明、PNG/JPG 图片背景。
- 异常会写入程序同目录的 `error.log`。

## 下载

在 [Releases](https://github.com/Mechorca/AvatarGifTool/releases) 页面下载最新的 `AvatarGifTool_v*.exe`。

## 使用

1. 启动 `AvatarGifTool.exe`。
2. 点击 `选择...`，选择游戏目录下的 `Data/Base/Base.wz`。
3. 在 `模板` 输入框填入外观 ID 或名称，使用英文逗号分隔。
4. 选择普通模式或染色模式。
5. 染色模式下，在 `Gear` 输入框填入需要染色的外观。
6. 点击 `导出设置` 选择动作。
7. 点击 `导出`。

导出的 GIF 会生成在程序同目录下，文件名会自动包含外观名、导出模式和动作名。

## 输入规则

模板示例：

```text
2000,54506,30000,1051001,1104025
```

说明：

- ID 和名称可以混合输入。
- 名称需要能被搜索索引唯一匹配。
- 肤色、脸型、发型会自动识别，不要求固定顺序。
- 未填写肤色时会尝试使用默认肤色。
- 同一装备位置后输入的外观会覆盖前面的外观。
- 染色模式下 `Gear` 可以一次填写多个外观，都会参与染色。

## Base.wz

程序不包含任何游戏数据。首次使用时需要手动选择游戏目录下的：

```text
Data/Base/Base.wz
```

请只使用你有权访问和使用的游戏数据文件。

## 编译

需要 .NET 8 SDK。目标运行环境是 Windows 10/11 x64。

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

自包含发布后，目标机器不需要单独安装 .NET 运行时。

CLI 入口仍然保留，可运行以下命令查看参数：

```powershell
AvatarGifTool.exe --help
```

## 致谢

本项目基于并改造自 [WzComparerR2-CMS](https://github.com/Jancy-49/WzComparerR2-CMS)。

WZ 读取、StringLinker、角色纸娃娃渲染、装备 Tooltip、GIF 编码和大量底层渲染逻辑都来自原项目及其上游项目。本仓库主要新增和整理了独立的 `AvatarGifTool` 程序、简化 UI、GIF 导出流程、搜索辅助、染色逻辑以及相关修复。

原项目 README 保留在 [docs/upstream/WzComparerR2-CMS-README.md](docs/upstream/WzComparerR2-CMS-README.md)。

## 许可证

本仓库保留原项目 MIT License。详见 [LICENSE](LICENSE)。
