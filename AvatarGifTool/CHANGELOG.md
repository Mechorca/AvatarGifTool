# AvatarGifTool Changelog

## Versioning

- Patch: bug fix, update the last number.
- Minor: small feature, update the middle number.
- Major: large feature, update the first number.

## Known Versions

> Note:
> - This project directory is not currently backed by git history in this workspace.
> - The records below are reconstructed from confirmed build artifacts and recent development history.
> - Versions between `2.0.0` and `2.4.1` do not currently have enough local evidence to restore precise per-version details.

### 2.21.1

- 调整头发 / 瞳色混染 UI：去掉混染滑条刻度，混染行控件垂直居中，并增加上下间距。

### 2.21.0

- 新增头发 / 瞳色混染功能，使用原项目 `AvatarPart.MixColor` / `MixOpacity` 混合逻辑。
- 混染参数支持 UI 保存、预览说明、导出文件名区分，并同步支持命令行参数。

### 2.20.0

- `色系` 选择框右侧新增 `初始化` 按钮，可一键恢复整体色系，并将颜色、饱和度、亮度重置为 0。

### 2.19.10

- 将 `历史搭配` 保存和显示上限从 4 条调整为 7 条，并保持单排紧凑布局。

### 2.19.9

- 压缩 `GIF 背景` / `背景图片` / `历史搭配` 区域高度，历史搭配 4 条记录改为单行紧凑摆放。

### 2.19.8

- 取消 `历史搭配` 模块滚动条，历史记录保存上限调整为 4 条，超过后自动移除最旧记录。
- 增加 `GIF 背景` 与 `背景图片` 的间距，并拉长背景设置区域，改善右侧历史搭配边框显示。

### 2.19.7

- 修复 `历史搭配` 模块底部边框在高 DPI / 自动布局下可能被遮挡的问题。

### 2.19.6

- 调整 `GIF 背景` / `背景图片` 右侧的 `历史搭配` 模块，使其跟随左侧背景控件靠左摆放，不再被推到参数区最右侧。

### 2.19.5

- 将 `历史搭配` 模块移动到 `GIF 背景` 右侧，并让其跨过 `GIF 背景` 与 `背景图片` 两行，高度与两块合计高度一致。

### 2.19.4

- 去掉精确染色模式下 `颜色` 与 `饱和度` 之间的额外垂直间隔。

### 2.19.3

- 修复精确染色模式下 `颜色` 数字输入框未按 `饱和度` / `亮度` 同样参数垂直居中，导致颜色行看起来偏上的问题。

### 2.19.2

- 修复精确染色模式下 `颜色` 行上下间隔不均，视觉位置明显偏上的问题。
- 染色参数区改用独立空隙行控制 `色系`、`颜色`、`饱和度` 之间的间距，避免控件自身边距影响对齐。

### 2.19.1

- 将染色参数区的 `色系` 下拉框宽度恢复为较短宽度。
- 调整 `色系` 与 `颜色`、`颜色` 与 `饱和度` 之间的垂直间距。

### 2.19.0

- 修复首次打开软件后从普通模式切换到染色模式时，染色微调控件仍保持禁用，必须先触发一次导出才可操作的问题。
- 放宽染色参数区的 `色系` 行宽度，使其与饱和度、亮度行的可用宽度一致。
- 精确染色模式的 `颜色` 参数新增滑条，可拖动调整 0..359 色相值，并与精确数值框同步。

### 2.18.0

- 染色模式新增 `色系` 选项，可选择整体、红、黄、绿、祖母绿、青、紫等与原项目 Prism 逻辑一致的色系范围。
- 新增 `精确染色模式`，可手动指定色系、颜色、饱和度和亮度，并使用普通模式的动作选择与拼图布局。
- CLI 新增 `--mode exact` / `--exact-dye`、`--hue`，`--prism-type` 支持数字或中文色系名。

### 2.17.0

- ID 搜索结果新增 `道具说明` 列。
- 搜索栏新增 `搜索道具名 / 搜索道具说明` 选择框，默认搜索道具名，选择说明时会在道具说明中匹配关键字。

### 2.16.1

- 修复配置文件设为隐藏后可能无法再次覆盖保存，导致历史搭配退出程序后丢失的问题。
- 配置读写失败时会写入 `error.log`，便于定位权限或文件占用问题。

### 2.16.0

- 默认模板预设改为 `53065,64460,12015`。

### 2.15.1

- 修复启动时默认模板文本被自动选中高亮的问题。

### 2.15.0

- 模板输入框默认填入 `萌兔附体脸型,黑色乖巧萝莉发型,松软花瓣皮肤`，方便启动后直接校验或导出默认角色模板。

### 2.14.0

- 染色模式 GIF 在原有 3 列色相预览右侧新增第 4 列极值预览。
- 新增极值格依次为：饱和度 -99 / 亮度 -99、饱和度 -99 / 亮度 99、饱和度 99 / 亮度 -99、饱和度 99 / 亮度 99。

### 2.13.0

- 在 ID 搜索右侧新增 `仅搜索外观道具` 勾选框，默认勾选并保持原有外观搜索逻辑。
- 取消勾选后，搜索范围扩展到全部装备与普通道具名称/ID。
- 全道具搜索结果支持双击预览；装备继续显示装备说明，普通道具显示道具说明。

### 2.12.5

- 修复双击预览搜索结果 UI 错位 BUG。
- 双击搜索结果预览现在临时使用接近参考项目的 DPI 兼容缩放上下文，避免在 2K/4K 或高缩放屏幕上出现预览窗口尺寸异常、内容错位或过小的问题。

### 2.12.0

- Replaced the single `导出` action row with side-by-side `导出设置` and `导出` buttons using the same button size.
- Added an export-settings dialog:
  - normal mode now lets you multi-select from `stand1`, `swingO1`, `swingO2`, `shoot1`, `jump`, `walk1`
  - dye mode now lets you choose exactly one action from the same set
- Export filenames now append both the export mode and the selected action names, so different action combinations no longer overwrite each other.
- Normal-mode GIF layout now auto-adjusts toward a square grid based on the number of selected actions, while dye mode keeps the existing 3-column hue grid.
- Persisted the export-action selections in `AvatarGifTool.config` so the dialog reopens with the last-used settings.

### 2.12.1

- Fixed newer special-effect capes such as `1104025` being misclassified as skin.
- Skin node lookup now only runs for real skin-like ID types (`body`, `head`, `head_n`) instead of applying the broad `% 2000` skin fallback to every appearance ID.
- Gear IDs that happen to map to an existing skin body/head pair are now correctly kept as gear during template parsing and search resolution.

### 2.11.0

- Added a `备注` column to the search results list.
- Search results now analyze appearance metadata for likely character-hover gear and mark matching rows as `可悬浮`.
- When hover height can be inferred from `bodyRelMove` or forced character actions, the note now includes the upward offset in pixels, such as `可悬浮（上移12px）`.
- Kept the new hover analysis off the hot search path by resolving and caching notes asynchronously per visible results page.

### 2.11.1

- Rolled back the search-result hover remark feature and restored the pre-`2.11.0` search list behavior.
- Kept the project-local `build.env` helper so this workspace can continue to build with the pinned local .NET SDK path.

### 2.10.3

- Kept the config filename as `AvatarGifTool.config` and automatically applied the Windows hidden-file attribute after loading or saving it.

### 2.10.4

- Added the same name-display / ID-mapping behavior to the `Gear` box: gear IDs now normalize to names after validation, and manually typed exact item names are resolved back to the correct gear IDs for preview and export.

### 2.10.2

- Fixed the `历史搭配` area to keep all three lines visible by replacing the native multi-line button text with a custom-drawn history card control.
- History entries now persist `ID + 名字` together in `AvatarGifTool.config`, so hair / face / skin names can render immediately before `Base.wz` finishes loading.
- The template box now normalizes recognized IDs into display names and keeps an internal name-to-ID mapping so validation and export still use the correct appearance IDs.

### 2.9.11

- Replaced the custom vertically centered textbox implementation with stable native single-line `TextBox` behavior.
- Fixed cases where the template placeholder text and the `Gear` input text could still appear slightly top-shifted inside taller input boxes.
- Rebuilt the single-file release as `AvatarGifTool_v2.9.11.exe` under `AvatarGifTool/dist`.

### 2.9.12

- Removed the extra helper line under the ID search box and kept the guidance in the placeholder, reducing the blank space below the search input.
- Tightened the Gear row hint alignment so it sits on the same visual centerline as the input box.
- Fixed the dye adjustment rows so `饱和度` / `亮度`, their numeric inputs, and the sliders use consistent vertical alignment.

### 2.9.13

- Replaced the main template / gear / search inputs with a custom aligned input control so real text and placeholder text share the same vertical center.
- Reduced the extra bottom spacing under the template, mode, gear, and dye-adjustment rows.
- Explicitly forwarded focus events from the custom search input so Enter-to-search behavior and search focus handling continue to work.

### 2.9.14

- Fixed the startup crash in `AlignedInputBox` where `BackColor` / `ForeColor` change handlers could run before the inner controls were created.
- Moved the custom input's initial color setup to after child-control construction and added guards for early lifecycle callbacks such as font, resize, and enabled-state changes.

### 2.9.15

- Fixed the custom input placeholder layer so the hint text is explicitly kept in front of the inner textbox when it should be visible.
- This restores the template and search hint text that could disappear after the `2.9.14` input-control refactor.

### 2.9.16

- Replaced the custom placeholder overlay with WinForms native `TextBox.PlaceholderText` inside the aligned input control.
- Fixed the missing hint text in the template, gear, and search inputs while keeping the custom vertical centering for actual typed text.

### 2.10.0

- Fixed newer face IDs failing to render by changing face/hair lookup from a single fixed path to a cached recursive character-node fallback, aligning export-time lookup with the broader search index.
- Added a `历史搭配` area next to `背景图片`, showing up to 9 unique skin/face/hair combinations in a 3-column button grid.
- Clicking a history entry now writes its hair, face, and skin IDs back into the template field for quick reuse.
- History entries are de-duplicated, capped at 9, and stored in `AvatarGifTool.config`.

### 2.10.1

- Fixed face rendering for newer face IDs such as `54506` by reloading face emotions after the face part is applied and choosing the default export emotion from the current face node instead of only from the legacy fallback face.
- Reworked the `历史搭配` buttons into a tighter adaptive flow layout so the number of buttons per row follows the available width.
- Tightened the history button text format so hair / face / skin all fit more reliably, and aligned the `背景图片` row label with the larger history area.

### 2.9.0

- Removed the separate large “当前生效外观” pane from the main area.
- Changed the main window to an upper/lower layout: parameters on top, search results below.
- Moved the appearance resolution output into a compact “识别结果” box inside the parameter area.

### 2.9.1

- Removed the visible “识别结果” box from the parameter area.
- The tool still resolves template data internally for validation and export, but no longer shows that extra text box in the UI.

### 2.9.2

- Tightened the parameter area layout to remove extra whitespace, especially around the export row.
- Increased the overall parameter/control sizing so the top panel reads larger on screen.
- Improved alignment across parameter rows and search pagination controls.
- Normal mode now collapses the hidden Gear / dye rows more cleanly instead of leaving visual gaps.

### 2.9.10

- Reworked the custom vertically centered textbox so placeholder text is also drawn with the same vertical alignment as real input text.
- Fixed cases where the `Gear` textbox and the template hint text could still look slightly top-shifted.

### 2.9.9

- Replaced the main editable single-line inputs with a custom vertically centered textbox.
- Input text now stays left-aligned horizontally while being centered vertically inside the taller input boxes.

### 2.9.8

- Center-aligned the text inside the main single-line input boxes so template, gear, and search input content no longer sits left-aligned.

### 2.9.7

- Re-centered the main window after applying the saved startup size so the program opens in the middle of the screen instead of keeping the old top-left offset.
- The Base.wz file picker now opens with the main window as its owner, so that dialog also follows the centered app window more reliably.

### 2.9.6

- Adjusted the search pagination footer to bottom-align the jump labels, page-number input, and buttons within the same row.
- The `跳到` area now follows the button baseline more closely instead of only sharing a common row height.

### 2.9.5

- Rebuilt the search pagination footer to use a fixed row height instead of per-control top-margin offsets.
- The page count text, `跳到` / `页` labels, page-number input, and `跳转` button now stay on the same horizontal line more reliably under DPI scaling.

### 2.9.4

- Fixed the search pagination footer vertical alignment after the `2.9.3` layout change.
- The current page text, jump labels, page-number input, and jump button now use aligned top offsets again instead of appearing on different horizontal lines.

### 2.9.3

- Restored visible spacing between text inputs and their adjacent action buttons instead of letting them touch edge-to-edge.
- Added clearer vertical gaps between parameter rows.
- Reworked the search pagination footer layout so the page count, jump label, input box, and button align more cleanly after the larger UI sizing changes.

### 2.6.2

- Fixed skin search so skin entries are indexed from real character skin nodes instead of relying only on `StringEqp`.
- Search now matches category aliases such as `皮肤` / `肤色`.
- Removed the silent fallback that could map an explicitly entered skin ID back to the default `2000` skin.
- Manual entry of non-default valid skin IDs now uses the actual skin data instead of quietly rendering the default skin.

### 2.6.3

- Fixed skin search alias matching for extended skin IDs such as `12016`.
- Skin searches now match both canonical node IDs like `2016` and extended IDs like `12016`.

### 2.6.4

- Fixed skin search indexing to use the real searchable skin IDs shown to users, including IDs like `12016`.
- Fixed skin name resolution so skin entries prefer the actual item-string name instead of falling back to `肤色2000` / `肤色2016` style placeholders.
- Skin search results now display as `皮肤` entries and continue to match both body-form and head-form IDs.
- Moved the Base usage hint to the right side of the load status.
- Moved the template input hint into the input box placeholder.
- Removed the transparent swatch from preset GIF background colors while keeping explicit transparent background support.

### 2.6.5

- Fixed the UI bug where normal mode silently dropped the hidden `gear` value during preview and export.
- Normal mode now keeps applying extra appearance IDs already entered in the `Gear` field, which restores cases such as special-effect capes disappearing only outside dye mode.
- Removed the “文件会导出到程序目录...” hint from the UI.

### 2.7.0

- Changed the publish layout so versioned single-file builds can be placed directly under `AvatarGifTool/dist` without an extra nested version directory.
- Publish cleanup now keeps existing versioned EXEs in `dist` while still removing temporary publish byproducts.

### 2.7.1

- Fixed the publish cleanup so `AvatarGifTool/dist` keeps only versioned EXEs at the root level.
- New single-file releases no longer leave extra nested publish folders under `dist`.

### 2.7.2

- Published the latest single-file build directly into `AvatarGifTool/dist` as a flat, versioned EXE.
- Cleaned the legacy nested publish folders from the current `dist` output directory.

### 2.8.0

- Added version text to the main window title bar.
- Added custom background image support for PNG/JPG, with cover scaling and center crop during GIF rendering.
- GIF export and CLI now support selecting a background image in addition to solid-color or transparent backgrounds.
- Improved effect action selection for animated appearance layers so normal-mode rendering can fall back to effect-specific actions such as `effect` and `effect2`.
- Avatar loading now refreshes effect metadata after parts are added, aligning the tool more closely with the original avatar viewer's effect-loading flow.

### 2.8.1

- Removed the visible status/log output row from the parameter area below the background image controls.
- Search, load, and export progress still run internally, but no longer write UI text into that bottom parameter slot.

### 2.6.1

- Dye mode now applies dye to all extra appearance IDs entered in the `Gear` box, not only one item.
- Auto-generated output filenames now append the export mode suffix:
  - `_普通模式`
  - `_染色模式`
- Preview text now shows the full dyed gear set instead of only a single dye target.

### 2.6.0

- Added search page jump controls.
- Moved dye numeric input boxes to the left side of the sliders.
- Added support for multiple comma-separated extra appearance IDs in `gear`.
- Unified CLI and UI multi-gear parsing behavior.
- Changed normal mode GIF layout to `3 x 2`.

### 2.5.0

- Improved search performance by building a search index when `Base.wz` is loaded.
- Published single-file builds into versioned output directories and versioned EXE names.
- Search preview window supports `Esc` to close.
- Right-clicking a search result can append its ID to the template field.
- Added precise numeric inputs for dye saturation and brightness offsets.

### 2.4.3

- Single-file EXE output uses a versioned filename suffix.
- Confirmed build artifact:
  - `AvatarGifTool_v2.4.3.exe`

### 2.4.2

- Confirmed single-file EXE release exists.
- Confirmed build artifact:
  - `AvatarGifTool.exe`

### 2.0.0

- Introduced the major search feature set.
- Template input changed to free-form ID input with automatic appearance type detection.
- Added search by Chinese keyword or ID fragment.
- Added paged search results.
- Added double-click preview for search results.

## Unresolved Historical Range

### 2.0.1 - 2.4.1

- Release artifacts or changelog entries are not available in the current workspace.
- Exact per-version changes are currently unknown.
