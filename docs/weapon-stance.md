# 武器姿势功能说明

## 功能概述

`AvatarGifTool` 的设置窗口新增 `武器姿势设置`，用于在导出或预览角色动作时选择武器动画的姿势类型：

- 单手
- 双手
- 枪

武器姿势会随配置保存，并应用于：

- 普通模式动作导出
- 精确染色模式动作导出
- 染色模式的单动作导出
- 实时预览
- 命令行导出

## 动作映射

选择姿势后，工具会对标准动作名做有限映射。映射只在两个条件同时满足时生效：

1. 角色本体包含目标动作；
2. 当前装备的武器节点包含目标动作。

任一条件不满足时保留原动作，避免武器或身体动作缺失。映射后如果多个原动作得到同一个实际动作，只导出一次。

| 原动作 | 单手 | 双手 | 枪 |
| --- | --- | --- | --- |
| `stand1` / `stand2` | `stand1` | `stand2` | `stand1` |
| `walk1` / `walk2` | `walk1` | `walk2` | `walk1` |
| `swingO1` / `swingO2` / `swingO3` / `swingOF` | 原样 | `swingT1` / `swingT2` / `swingT3` / `swingTF` | `shoot1` / `shoot2` / `shoot3` / `shootF` |
| `swingT1` / `swingT2` / `swingT3` / `swingTF` | `swingO1` / `swingO2` / `swingO3` / `swingOF` | 原样 | `shoot1` / `shoot2` / `shoot3` / `shootF` |
| `stabO1` / `stabO2` / `stabOF` | 原样 | `stabT1` / `stabT2` / `stabTF` | `shoot1` / `shoot2` / `shootF` |
| `stabT1` / `stabT2` / `stabTF` | `stabO1` / `stabO2` / `stabOF` | 原样 | `shoot1` / `shoot2` / `shootF` |
| `shoot1` / `shoot2` / `shootF` | 原样 | 原样 | 原样 |
| `jump` | 原样 | 原样 | 原样 |

当前的普通模式默认动作仍为：

```text
stand1, swingO1, swingO2, shoot1, jump, walk1
```

因此选择双手时，通常会导出：

```text
stand2, swingT1, swingT2, shoot1, jump, walk2
```

选择枪时，通常会导出：

```text
stand1, shoot1, shoot2, jump, walk1
```

工具会去掉映射后的重复动作。

## 点装武器形态

点装武器的 WZ 节点里可能同时包含多个武器形态子节点，例如：

- `30`：单手形态
- `40`：双手形态
- `49`：枪形态

旧逻辑默认取排序后的第一个节点，导致选择了枪姿势但实际仍使用 `30` 的单手动画，看起来和单手模式相同。

现在会根据武器姿势优先选择对应节点：

| 武器姿势 | 优先形态 ID |
| --- | --- |
| 单手 | `30` |
| 双手 | `40` |
| 枪 | `49` |

如果武器没有对应形态节点，则回退到之前选中的形态，或该武器可用的第一个形态节点。

## 界面

设置弹窗顶部新增 `武器姿势设置` 分组：

- 单选：单手 / 双手 / 枪
- 下方提示当前普通模式动作的优先映射结果
- 提示会明确说明：只有武器包含对应动作时才生效，否则保留原动作

## 配置与命令行

配置文件新增字段：

```json
{
  "WeaponStance": 1
}
```

取值：

- `1`：单手
- `2`：双手
- `3`：枪

命令行新增参数：

```text
--weapon-stance one-hand|two-hand|gun
```

同时兼容 `1h`、`2h`、`1`、`2`、`3`、`单手`、`双手`、`枪`。

示例：

```text
AvatarGifTool.exe --base-wz D:\Maple\Data\Base\Base.wz --template 1051001,30000,2000,20000 --gear 1302000 --output D:\out\weapon.gif --weapon-stance two-hand
```

## 实现位置

- `AvatarGifTool/Program.cs`
  - 新增 `WeaponStance` 枚举和动作映射函数
  - `AvatarBuilder` 记录当前武器节点支持的动作
  - 渲染动作时解析姿势映射，并去重
  - 命令行新增 `--weapon-stance`
- `AvatarGifTool/MainForm.cs`
  - 设置弹窗新增武器姿势分组
  - 保存/读取武器姿势配置
  - 实时预览、导出和预览文本接入武器姿势
- `AvatarGifTool/AppConfig.cs`
  - 新增 `WeaponStance` 持久化字段
- `AvatarGifTool/AvatarGifTool.csproj`
  - 版本更新到 `2.26.1`
- `AvatarGifTool/CHANGELOG.md`
  - 记录 `2.26.0` 武器姿势功能和 `2.26.1` 枪形态修复
- `README.md`
  - 补充功能说明和设置步骤

## 构建产物

`2.26.1` 已使用 Release / `win-x64` 配置发布：

- 可运行单文件：`AvatarGifTool/dist/AvatarGifTool_v2.26.1.exe`
- 发布中间产物：`AvatarGifTool/bin/x64/Release/net8.0-windows/win-x64/`
- 构建输出：`AvatarGifTool/bin/x64/Release/net8.0-windows/`

本次发布文件大小约 179 MB，SHA-256：

```text
31a54d8a845bfb4efad2d7a030ed13dd8d6ff42ee24fbca86829b54304996377
```

## 验证

本工作区没有游戏 `Base.wz` 数据，因此无法在本地实际渲染并目视核对每个武器动作。已完成：

- 项目编译验证：`AvatarGifTool` 构建成功，0 个错误（140 个既有警告）
- 单文件发布验证：`AvatarGifTool_v2.26.1.exe` 已生成，PE32+ x86-64 Windows GUI 程序
- 代码路径核对：设置弹窗、配置保存、实时预览、普通/精确染色导出、CLI 均已接入
- 映射规则核对：所有替换都要求武器节点存在目标动作，否则回退原动作

建议在 Windows 环境使用实际 `Base.wz` 分别测试单手剑、双手剑和枪武器。
