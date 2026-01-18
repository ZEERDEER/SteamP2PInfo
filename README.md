<p align="center">
  <img src="icon.png" width="128" height="128" alt="Steam P2P Info Icon">
</p>

<h1 align="center">Steam P2P Info Modern</h1>

<p align="center">
  基于 <a href="https://github.com/tremwil/SteamP2PInfo">tremwil/SteamP2PInfo</a> 的现代化重构版本
</p>

<p align="center">
  <img src="screenshot.png" alt="Preview">
</p>

## ✨ 新特性

- 🎨 **现代化界面** - 使用 React + Tailwind CSS 打造的精美 UI
- 🌙 **深色主题** - 暖橙色调，游戏感十足
- 💨 **流畅动画** - 玻璃态效果、平滑过渡
- 📦 **单文件分发** - 打包为单个 exe 文件

## 🛠️ 技术栈

- **后端**: .NET 8 + WPF + WebView2
- **前端**: React 18 + TypeScript + Tailwind CSS
- **构建**: Vite

## 📋 系统要求

- Windows 10 1903+ 或 Windows 11
- Steam 客户端
- **管理员权限**（用于 ETW 网络监控）

## 🚀 快速开始

### 使用预编译版本

1. 从 Releases 下载 `SteamP2PInfo.exe`
2. 右键以管理员身份运行
3. 点击"连接游戏"选择要监控的游戏窗口
4. 输入 Steam App ID（首次需要）

### 从源码构建

```powershell
# 1. 克隆仓库
git clone https://github.com/ZEERDEER/SteamP2PInfo-Modern.git
cd SteamP2PInfo-Modern

# 2. 运行构建脚本（需要管理员权限）
.\build.ps1
```

构建产物位于 `publish` 目录。

## 🎮 支持的游戏

理论上支持所有使用 Steam P2P 网络的游戏，包括但不限于：

- ELDEN RING（艾尔登法环）
- Dark Souls III（黑暗之魂3）
- Dark Souls Remastered
- 其他使用 Steamworks P2P 的游戏

## ⚙️ 配置说明

| 选项 | 说明 |
|------|------|
| 添加到最近一起玩 | 让队友出现在 Steam 最近玩家列表 |
| 在 Steam 悬浮层打开资料 | 双击玩家时在游戏内打开 Steam 资料 |
| 记录活动日志 | 保存玩家连接/断开记录 |
| 新会话提示音 | 有新玩家加入时播放声音 |

## 📁 配置文件位置（便携模式）

配置文件存储在程序所在目录：

```
SteamP2PInfo/
├── config/
│   ├── settings.json      # 全局设置
│   └── games/
│       ├── eldenring.json # 游戏特定配置
│       └── DarkSoulsIII.json
├── logs/                  # 活动日志
└── cache/                 # WebView2 缓存
```

## 🙏 致谢

- [tremwil](https://github.com/tremwil) - 原版 SteamP2PInfo 作者
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)
- [MahApps.Metro](https://github.com/MahApps/MahApps.Metro) - 原版 UI 框架

## 📄 许可证

MIT License
