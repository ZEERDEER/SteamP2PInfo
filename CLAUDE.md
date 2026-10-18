# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

SteamP2PInfo is a Windows-only WPF utility (.NET Framework 4.8, x64) that attaches to a running Steam P2P game and shows the connected peers (SteamID, ping, connection quality) in a main window and an in-game overlay. It was built for Elden Ring but works for any Steam Networking game that authenticates peers with `ISteamUser::BeginAuthSession`. Single project (`SteamP2PInfo/`), no tests, no CI. Code style follows `.editorconfig` (PascalCase, block-scoped namespaces, `I`-prefixed interfaces) but nothing enforces it outside the IDE.

The exe is manifested `requireAdministrator` (app.manifest): pings for old-style `ISteamNetworking` peers come from an ETW kernel network session, which needs elevation.

## Build

The project is a classic (non-SDK) csproj using `packages.config`, so restore goes through MSBuild, not `dotnet restore`. Plain `msbuild -restore` / `-t:Restore` is a silent no-op here; the `RestorePackagesConfig` property is required:

```powershell
msbuild SteamP2PInfo.sln -t:Restore -p:RestorePackagesConfig=true
msbuild SteamP2PInfo.sln -p:Configuration=Release   # or Debug
```

Run these from a Developer PowerShell (or use the full path to MSBuild under the Visual Studio install). Packages restore to `packages/` in the repo root (gitignored). Both configurations are x64 and run the post-build targets in `SteamP2PInfo/build.targets`: copy `steamworks_bin/steam_api64.dll` next to the exe; Release additionally deletes `*.pdb` / `*.xml` / `*.config` from the output and strips stray `x86/`, `arm64/`, `de/` folders. Costura (Fody) embeds the managed references into the exe, so the contents of `SteamP2PInfo/bin/Release/` are what ships in a release ZIP: the exe, `steam_api64.dll`, TraceEvent's support DLLs (`Dia2Lib.dll`, `OSExtensions.dll`, `TraceReloggerLib.dll`) and `amd64/` (KernelTraceControl, msdia, VC runtimes).

Known-benign warnings: CS0067 on `GameConfig.PropertyChanged` / `OverlayConfig.PropertyChanged` (Fody generates the raise code at build time) and NU1903 for old `System.*` packages pulled in by TraceEvent 2.0.76.

`WPF-UI` restores from NuGet like the other packages; the Release post-build's file pruning is configured in `build.targets`.

There are no automated tests. Verifying anything requires a live game, Steam and admin rights:

1. Start the game, then run `SteamP2PInfo.exe` (UAC prompt, x64).
2. Click "ATTACH GAME" (top right), pick the game window; on first attach enter the game's Steam AppId.
3. The app opens the Steam console; run `log_ipc "BeginAuthSession,EndAuthSession,LeaveLobby,SendClanChatMessage"` there (the app can copy it to the clipboard).
4. Peers appear in the "Session Info" tab and in the overlay. The overlay is windowed/borderless only.

`VersionCheck.CurrentVersion` ("V2.0.0") is the hardcoded version compared against the GitHub releases API at startup; bump it together with the README when releasing.

## Architecture

Attach flow, driven from the "ATTACH GAME" link (`labelGameState_RequestNavigate` in MainWindow): `WindowSelectDialog` lists visible top-level windows, `GameConfig.LoadOrCreate(processName)` loads or creates `config/{processName}.json`, the AppId is set via the `SteamAppId` environment variable and `SteamAPI.Init()` runs (the tool impersonates the game's AppId, which is why it must exit when the game does; the timer also force-closes it once the game window handle dies), then `SteamPeerManager.Init()`, the Steam console prompt, hotkey registration, `OverlayWindow` with its WinEvent hooks, the generated config UI, and `ETWPingMonitor.Start()`. A 1-second `System.Threading.Timer` drives everything after that.

Peer detection (`SteamPeerManager`): tails Steam's IPC log (`Settings.Default.SteamLogPath`, default `...\Steam\logs\ipc_SteamClient.log`) with a `FileSystemWatcher` and forward reads from the saved position. Only lines mentioning the attached game's process name count; `BeginAuthSession` / `EndAuthSession` add and remove peers, `LeaveLobby` clears them. SteamID3 `[U:1:n]` converts to SteamID64 as `n + 0x0110000100000000`. Every update sends a dummy `SteamFriends.SendClanChatMessage` so Steam flushes the log. Peers are dropped after `PEER_TIMEOUT_MS` (5 s) without a live P2P connection.

Peer connection types: `SteamPeerManager.PEER_FACTORIES` reflects over `SteamPeerBase` subclasses, so a new subclass is tried automatically with no registration list:
- `SteamPeerNewAPI`: `ISteamNetworkingMessages.GetSessionConnectionInfo`; ping and quality come from Steam.
- `SteamPeerOldAPI` (deprecated `ISteamNetworking`): `GetP2PSessionState` for connectivity; ping and jitter come from `ETWPingMonitor`, keyed by `(port << 32) | ipv4`; quality is `1 / (0.01 * jitter + 1)`.

Ping text color is resolved by `SteamPeerBase.PingColor`, which picks the highest `OverlayConfig.PingColors` threshold that is <= the ping.

`ETWPingMonitor` opens a kernel ETW session (NetworkTCPIP keyword) and matches UDP packets by STUN payload sizes (56 bytes sent, 68 received) to compute ping, a 10-sample average, jitter, and a late-packet ratio.

Overlay: `OverlayWindow` is a transparent, topmost, click-through WPF window kept aligned with the game window (DPI-aware) from `OverlayAnchor` plus X/Y offsets; its extended styles also hide it from Alt+Tab, which is why dragging the overlay is a documented non-working feature. `OverlayTextBlock` is a custom `FrameworkElement` that draws outlined text (fill + stroke geometry). Position and visibility are driven by WinEvent hooks (game window moved, foreground change) plus Z-order juggling relative to the game window.

Config: per-game JSON at `config/{processName}.json` (Newtonsoft, path relative to the process working directory) via `GameConfig.LoadOrCreate` into static `GameConfig.Current`; app-wide values live in `Settings.settings` (user.config). The Config tab is generated at runtime from attributes on the config classes (`[ConfigBindingElement]`, `[ConfigEnumComboBox]`, `[ConfigFontSelector]`, `[ConfigCategory]`, `[ConfigPingColorsPreview]`); `ConfigUIBuilder` reflects over them. To add a setting, add a `[JsonProperty]` property with one of these attributes; no XAML changes are needed. Fody's `PropertyChanged` weaver generates the `INotifyPropertyChanged` plumbing, and a Fody quirk breaks change notification for `GameConfig`, worked around by re-saving the config every 6 timer ticks in `MainWindow.Timer_Tick`.

Other pieces: `HotkeyManager` is a WH_KEYBOARD_LL hook whose hotkeys fire only while the attached game window is foreground (combo encoded as `vk | modifiers << 8`, the same encoding `Controls/HotKeyBox` uses in the config UI). `Logger` writes `logs/{processName}/{processName}-{date}.log` only when the per-game "Log Activity" option is enabled. `MustEnterSteamCommand` re-prompts for the Steam console command when the version changed (`Settings.LastRunVersion`), the IPC log is missing, or Steam started after the IPC log was last written; it reads `bootstrap_log.txt` backwards with `ReverseTextReader`.

UI framework is WPF-UI 4.3.0 (FluentWindow + TitleBar + NavigationView; the app runs dark-only with `ThemesDictionary Theme="Dark"`). The main window hosts two views in `Views/`: `SessionView` (peer cards bound to `ObservableCollection<SteamPeerBase>`, avatars via `SteamAvatar`, update banner) and `ConfigView` (STEAM card + generated settings cards). Config controls come from WPF-UI (`ToggleSwitch` — its state DP is `IsCheckedProperty` — and `NumberBox`) plus custom `Controls/HotKeyBox` (int encoding `vk | mods << 8`, same as the config JSON) and `Controls/ColorBox` (hex string with live tint; WPF-UI 4.3.0 ships no usable color picker). Dialogs go through `Dialogs.cs` (WPF-UI `MessageBox`).

## Gotchas and vestigial code

- `config/` and `logs/` are runtime state next to the exe (so normally inside `bin/`); `logs/` is gitignored, `config/` is not. Do not commit either.
- `WinAPI/Kernel32.cs` exposes remote-process APIs (`CreateRemoteThread`, `VirtualAllocEx`, `Read/WriteProcessMemory`, `OpenProcess`) that nothing calls. They are leftovers from an abandoned approach; the project deliberately avoids game memory and anti-cheat interaction.
- `SteamPeerBase.MainSteamID` (commented as obsolete) and `Settings.SteamWebApiKey` are unused.
- `ILRepack` in `packages.config` is imported but never invoked, and `Directory.builds.targets` is empty.
- The overlay window style flags are re-applied after Z-order changes because Windows clears `WS_EX_TOOLWINDOW` when a topmost window is demoted (`OverlayWindow.UpdateVisibility`).
