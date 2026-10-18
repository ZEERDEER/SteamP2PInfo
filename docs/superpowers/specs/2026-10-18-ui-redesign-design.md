# SteamP2PInfo 界面重设计 · 设计文档

日期：2026-10-18
状态：已通过用户评审（视觉稿 v1 + v2）
视觉稿存档：`.superpowers/brainstorm/1992-1792297928/content/design-v1.html`、`design-v2.html`（本地文件，已 gitignore）

## 1. 背景与目标

现有界面基于 MahApps.Metro 2.x 的 "Metro" 风格（深色 + 蓝色标题栏 + 平铺表格），观感停留在 Windows 8/10 时代。本次重设计：

- 把全部界面换成现代的 Fluent（Windows 11）风格，方向已由用户选定。
- 功能与行为完全不变：attach 流程、IPC 日志解析、ETW ping、悬浮层机制、配置系统语义都保持原样。
- 用户明确提出的三处附加要求（已完成设计确认）：底部署名、Banner Format 默认值、IPC 日志路径默认值。

## 2. 非目标

- 不加新功能（无会话历史、无延迟曲线图、无主题切换）；第 3.5 节的延迟颜色只读预览属展示性改动，不扩展配置编辑能力。
- 不改技术栈：仍是 .NET Framework 4.8 / WPF / x64，不改 Steam 交互逻辑。
- 悬浮层的运行机制（WinEvent 钩子、置顶、点击穿透、DPI 换算、偏移量）不动，只调文本排版。

## 3. 视觉设计

### 3.1 设计语言

- 方向：Fluent（Windows 11 原生感）。深色主题（应用原本就是深色，不新增明暗切换）。
- 强调色：固定 Windows 蓝 `#4CC2FF`（用户选定，不跟随系统强调色）。

### 3.2 设计 Token

| 类别 | 值 |
| --- | --- |
| 窗口底色 | `#202020` |
| 卡片 / 层 | `#2B2B2B`（hover `#323232`） |
| 描边 | `rgba(255,255,255,0.08)`，分隔线 `0.05` |
| 文本 | 主 `#FFFFFF` / 次 `#C8C8C8` / 三级 `#9A9A9A` |
| 强调色 | `#4CC2FF` |
| 状态色 | 由配置的 `PingColors` 驱动：延迟色块文字 = 配置色，底色 = 同色 14% 透明度；连接质量条填充同色 |
| 字体 | `Segoe UI Variable Text`（回退 `Segoe UI`）；数字一律 tabular-nums |
| 圆角 | 窗口 8 / 卡片 6 / 控件 5 / 胶囊 999 |
| 间距 | 内容边距 16–18，行间距 8，行高内边距 10×14 |

### 3.3 主窗口（已连接）

- 窗口：FluentWindow，自定义标题栏；Windows 11 上使用 Mica 背景，非 Win11 回退纯色 `#202020`；默认 800×450。
- 标题栏：左为应用图标（新绘制，见 4.5）+ "SteamP2PInfo"；右为更新徽标（仅有新版本时显示）+ 最小化/最大化/关闭。
- 左侧导航（宽 152）：Session（人员图标）、Config（设置图标）；选中项为浅色胶囊 + 左侧 3px 强调色竖条。
- 内容区头部：游戏名（19px semibold）+ 状态行（绿点 + "Connected · N players"）。
- 玩家列表：每人一张圆角卡片行，从左到右：
  1. 32px 头像（优先取 Steam 头像；取不到时用彩色圆形 + 名字首字母，颜色由 SteamID 哈希决定）；
  2. 名字（13.5 semibold）与 SteamID64（11 三级色、tabular）；
  3. 右侧：延迟胶囊（如 "94 ms"，颜色按上述规则）+ 44px 连接质量条 + 质量数值（11 三级色）。
- 行为保持：双击玩家打开 Steam 个人资料（或浏览器，取决于既有配置）。
- 底部状态条：左侧提示 "F2 toggles the overlay · Double-click a player to open their Steam profile"；右侧署名 **"Made by tremwil · Redesigned by zeer"**。

### 3.4 主窗口（未连接空状态）

- 内容区居中：圆角图标块 + "No game attached"（15 semibold）+ 两行说明 "Start your game, then attach to its window. / First time? You will be asked for the game's Steam AppId." + 强调色按钮 "Attach Game"。
- 点击逻辑与原 "ATTACH GAME" 链接一致（attach 前置校验、AppId 询问等全部保留）。

### 3.5 配置页

- 头部：标题 "Config" + 副标题 "Settings for {游戏名} · saved to config\{进程名}.json"。
- 布局：分组卡片，行内左标签（可带 "?" 提示气泡）右控件，行间分隔线。
- 分组与内容（与现有配置项一一对应，字段名不变）：
  - GENERAL：Set Played With、Show profiles in Steam overlay、Log Activity、Enable Hotkeys、Play sound on new session（均 ToggleSwitch）。
  - OVERLAY：Overlay Enabled、Show Steam ID、Show Connection Quality（ToggleSwitch）；Overlay Hotkey（HotKeyBox）；Banner Format（TextBox）；Font（字体选择按钮，仍用系统字体对话框）；Anchor（下拉框）；X Offset / Y Offset（数字输入）；Text Color / Stroke Color（颜色控件）；Stroke Width（数字输入）；Ping Colors（**新增只读预览**：显示阈值色块，解决"只能改 JSON"的已知问题；编辑功能不在本次范围）。
  - STEAM：IPC log file path（TextBox）。
- 未 attach 时只显示 STEAM 分组（即现有"先改路径"的能力），attach 后出现其余分组。
- 生成方式不变：继续用属性（Attribute）+ `ConfigUIBuilder` 运行时生成，不写逐项 XAML。

### 3.6 选窗口对话框

- 400px 宽 Fluent 对话框：标题 "Select game window" + 说明 + 搜索框（按标题/进程名过滤）+ 窗口列表（行 = 窗口标题 + 右侧进程名，选中行强调色浅底 + 左竖条）+ 底部 Cancel / Attach 按钮。
- 交互保持：双击行或选中后点 Attach 即返回；取消关闭。

### 3.7 游戏内悬浮层

- 机制全部不动（无边框、置顶、点击穿透、WinEvent 跟随、锚点 + 偏移、OverlayTextBlock 描边渲染）。
- 排版调整：头部 Banner Format（默认只剩时间，见 4.2），正文改为对齐网格：Player | SteamID（可隐藏）| Ping（按配置变色）| Quality（可隐藏）。
- 字体、文字色、描边色、描边宽度、位置仍由配置控制；字体默认值不变（"Segoe UI, 20.25pt"）。

### 3.8 文案（全部英文）

新增/修改的界面字符串：`No game attached`、`Start your game, then attach to its window.`、`First time? You will be asked for the game's Steam AppId.`、`Attach Game`、`Connected · N players`、`Select game window`、`Pick the window of the game you want to monitor`、`Search windows…`、`Made by tremwil · Redesigned by zeer`、`F2 toggles the overlay`、`Double-click a player to open their Steam profile`。视觉稿里的中文只是注解，界面一律英文。

## 4. 默认值与设置变更

1. **底部署名**：主窗口底部状态条右侧增加 "Made by tremwil · Redesigned by zeer"（纯文本，不做链接）。
2. **`OverlayConfig.BannerFormat` 默认值**：由 `"[{time:HH:mm:ss}] SteamP2PInfo - by tremwil"` 改为 `"{time:HH:mm:ss}"`（悬浮层顶部默认只显示时间）。
3. **`Settings.SteamLogPath` 默认值**：由 `C:\Program Files (x86)\Steam\logs\ipc_SteamClient.log` 改为 `D:\Steam\logs\ipc_SteamClient.log`。
4. **`Settings.SteamBootstrapLogPath` 默认值**：同步改为 `D:\Steam\logs\bootstrap_log.txt`（与上一项配套，必须一致）。
   - 以上 3、4 同时改 `Settings.settings` 与 `app.config`；已保存过 user.config 的旧用户不受影响，仅新配置使用新默认值。
5. Ping 颜色默认阈值表不变。

## 5. 技术方案

### 5.1 框架替换

- 用 **WPF-UI（lepoco, NuGet 包 "WPF-UI"）** 替换 **MahApps.Metro 2.4.9 + ControlzEx 5.0.1**。
  - 版本：4.x 最新稳定版（实现时锁定确切版本）。
  - net48 兼容性已核实：包目标框架含 net462/net472/net481，net48 项目将引用其 net472 资产。
  - 添加方式：编辑 `packages.config` + csproj 引用，然后 `msbuild SteamP2PInfo.sln -t:Restore -p:RestorePackagesConfig=true`（本仓库 packages.config 流程，见 CLAUDE.md）。
- 关键控件映射：
  - `MetroWindow` → `Wpf.Ui.Controls.FluentWindow`（Mica 背景 + 圆角；非 Win11 回退纯色）。
  - `MetroTabControl` → `Wpf.Ui.Controls.NavigationView`（Session / Config 两项，内容为两个 UserControl）。
  - ToggleSwitch / NumericUpDown / 枚举下拉 / ColorPicker → `ui:ToggleSwitch` / `ui:NumberBox` / 标准 ComboBox（由 WPF-UI 主题样式接管）/ `ui:ColorPicker`（若该控件不可用则自绘小弹出：十六进制输入 + 预设色板，实现时验证）。
  - **HotKeyBox：WPF-UI 无等价控件，自写一个小的 HotKeyBox 控件**；按键编码沿用现有格式（`vk | modifiers << 8`），保证配置文件 JSON 兼容。
  - 对话框：`Wpf.Ui.Controls.MessageBox.ShowDialogAsync` 替换 MahApps 的 Message/Modal 弹窗；AppId 输入由 WinForms `InputBox` 换成一个 Fluent 文本输入对话框（ContentDialog）；Steam 控制台提示沿用文本 + "Copy Command" 按钮。
- 移除项：MahApps.Metro、ControlzEx、`HotkeyConverter`（依赖 MahApps 的 HotKey 类型）、`Microsoft.VisualBasic` 引用（仅 InputBox 用）。
- Fody（PropertyChanged + Costura）不变：WPF-UI 的托管程序集由 Costura 自动嵌入；原生 DLL 列表由既有 post-build 处理。

### 5.2 配置生成器

- `ConfigUIBuilder` 改为生成"设置卡片"版式（分组标题 + 行 + 右对齐控件）。
- 属性集调整：`ConfigBindingElement`（控件类型换成 WPF-UI 类型）、`ConfigEnumComboBox`、`ConfigFontSelector`（按钮样式化，行为不变）、`ConfigCategory`（渲染为分组卡片）；新增 `ConfigPingColorsPreview`（只读色块预览）。
- 配置模型（`GameConfig` / `OverlayConfig`）字段与 JSON 结构完全不变。

### 5.3 涉及文件

- 重写：`App.xaml(.cs)`、`MainWindow.xaml(.cs)`、`WindowSelectDialog.xaml(.cs)`、`Config/ConfigUIBuilder.cs` 与各 Attribute、`Config/ConfigBindingElementAttribute` 等。
- 轻改：`OverlayWindow.xaml`（栅格排版）、`Config/OverlayConfig.cs`（BannerFormat 默认值）、`Settings.settings` + `app.config`（路径默认值）、`SteamP2PInfo.csproj` + `packages.config`（依赖增删）。
- 不动：`SteamPeerManager`、`SteamPeerBase/NewAPI/OldAPI`、`ETWPingMonitor`、`ReverseTextReader`、`Logger`、`VersionCheck`、`OverlayTextBlock`、`HotkeyManager`（仅换编码来源，逻辑不动）、`WinAPI/*`。
- 新增：自写 `HotKeyBox` 控件、Session/Config 两个 UserControl、应用图标资源。

### 5.4 图标

- 现有 exe 无应用图标（任务栏显示默认图标）。新增一个简单图标：深色圆角底 + 强调色网络/连接符号；产出 `.ico` 并配置 csproj `ApplicationIcon` + 窗口图标。优先级低，可作为收尾项。

## 6. 行为保持清单（逐项验证）

- attach 全流程（选窗口 → 校验日志目录 → 首次询问 AppId → 设置 `SteamAppId` 环境变量 → `SteamAPI.Init` → Steam 控制台指令提示/复制 → 热键注册 → 悬浮层 + 钩子安装 → 配置编辑器生成 → ETW 启动 → 1 秒计时器）。
- 游戏退出自动关闭、单实例检查、未处理异常弹窗（可复制）。
- 热键仅在游戏窗口前台生效；F2 开关悬浮层。
- 更新检查与"有新版本"提示。
- 双击玩家打开资料（悬浮层与主窗口行为一致）。
- 日志开关与写入路径不变；配置保存节奏不变（6 tick 一次 + 关闭时保存）。
- 配置 JSON 完全向后兼容（字段、类型、路径 `config\{进程名}.json`）。

## 7. 风险与备选

| 风险 | 缓解 |
| --- | --- |
| WPF-UI 在 net48 上的兼容性问题（XAML 解析、运行时异常） | 第一步先做"框架替换 + 空壳窗口"的最小验证；若不可行，退回自绘 Fluent 风格模板（零新依赖） |
| `ui:ColorPicker` 不存在或表现不佳 | 实现时验证；备选自绘小弹窗（hex 输入 + 预设色板） |
| Mica 在非 Win11 环境异常 | 平台检测，回退纯色深底 |
| NavigationView 的页面缓存行为不稳（官方文档标注未充分测试） | 不用其页面导航缓存，采用简单的内容切换 |
| 大幅重写 MainWindow 引入行为回归 | 事件与计时器逻辑整体搬迁，只改挂接点；对照第 6 节清单逐项手动验证 |
| 移除 MahApps 影响面大 | 一次性替换，逐界面编译通过后继续下一个 |

## 8. 验收标准

1. `msbuild SteamP2PInfo.sln -p:Configuration=Debug` 与 `Release` 均构建通过（Release 产物同现有规则）。
2. Win11 + 实际 Steam 游戏手动冒烟：attach → 控制台指令 → 主窗口与悬浮层均显示玩家（名字、ID、延迟、质量）；F2 开关悬浮层；配置修改保存到 JSON 并生效；底部署名显示。
3. 第 6 节清单逐项核对通过。
4. 新默认值生效：新环境下 IPC 路径为 `D:\Steam\logs\...`、Banner Format 为 `{time:HH:mm:ss}`。
5. 悬浮层在窗口化模式下对位、DPI、置顶行为与改版前一致。
