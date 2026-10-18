# SteamP2PInfo Fluent UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the MahApps.Metro "Metro" UI with a modern Fluent (Windows 11) interface across the main window, config page, window picker, and overlay, without changing any behavior — per the approved spec `docs/superpowers/specs/2026-10-18-ui-redesign-design.md`.

**Architecture:** Swap the UI framework from MahApps.Metro 2.4.9 + ControlzEx to WPF-UI 4.3.0 (verified: package targets net462/net472/net481, so the net48 project consumes its net472 assets; net472 dependency group = `WPF-UI.Abstractions 4.3.0` + `System.Memory 4.6.3`). Rewrite `MainWindow` as a `FluentWindow` with `TitleBar` + `NavigationView`, move views into `Views/SessionView` and `Views/ConfigView` UserControls, keep the attribute-driven config UI (`ConfigUIBuilder`) but render Fluent "settings cards", write a small `HotKeyBox` control (WPF-UI has no hotkey control), and keep every piece of peer-detection / ETW / overlay-mechanism code untouched.

**Tech Stack:** C# / WPF / .NET Framework 4.8 / x64, packages.config + MSBuild + NuGet restore, WPF-UI 4.3.0 (NuGet id `WPF-UI`), Newtonsoft.Json, Fody (PropertyChanged + Costura), TraceEvent, Steamworks.NET.

**Testing note:** This repo has no test project and no CI; the spec explicitly excludes new subsystems. Verification per task is therefore: (a) the exact MSBuild commands below must succeed, and (b) the listed MANUAL UI checks (marked MANUAL) performed by the user, since the app needs UAC + a live Steam game. Known-benign build warnings in this repo: CS0067 (`GameConfig.PropertyChanged` / `OverlayConfig.PropertyChanged`) and NU1903 (old `System.*` packages).

**Build commands used throughout (run from repo root `C:\Users\zeer\Desktop\SteamP2PInfo`):**

```powershell
# restore (only needed after packages.config changes)
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -t:Restore -p:RestorePackagesConfig=true
# build Debug
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
# build Release
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Release -v:m
```

Expected success line: `SteamP2PInfo -> ...\SteamP2PInfo\bin\Debug\SteamP2PInfo.exe` and `0 Error(s)`.

**File map (target end state):**

| File | Responsibility |
| --- | --- |
| `SteamP2PInfo/App.xaml` | WPF-UI `ThemesDictionary Theme="Dark"` + `ControlsDictionary` (replaces MahApps dictionaries) |
| `SteamP2PInfo/MainWindow.xaml(.cs)` | FluentWindow shell: TitleBar, NavigationView (Session/Config), footer with credits; all attach/timer/update logic migrated |
| `SteamP2PInfo/Views/SessionView.xaml(.cs)` | Header (game + status), update banner, empty state, peer card list |
| `SteamP2PInfo/Views/ConfigView.xaml(.cs)` | Config page: STEAM card (always) + generated cards (after attach) |
| `SteamP2PInfo/Controls/HotKeyBox.cs` | Custom hotkey capture control (int `Value`, same encoding as before) |
| `SteamP2PInfo/Converters/UiConverters.cs` | Hex→Brush, quality→width, avatar color, initial, SteamID→avatar image |
| `SteamP2PInfo/SteamAvatar.cs` | Avatar loader + cache (SteamFriends/SteamUtils) |
| `SteamP2PInfo/Dialogs.cs` | WPF-UI MessageBox wrappers (error, copy-command, AppId prompt, update notice) |
| `SteamP2PInfo/Config/ConfigUIBuilder.cs` | Renders settings cards/rows (rewritten, still attribute-driven) |
| `SteamP2PInfo/Config/ConfigPingColorsPreviewAttribute.cs` | Read-only ping-color chips row (new) |
| `SteamP2PInfo/Controls/ColorBox.cs` | Hex-color TextBox with live tint (new; WPF-UI 4.3.0 ships no usable color picker) |
| `SteamP2PInfo/Config/{ConfigBindingElement,ConfigCategory,ConfigEnumComboBox,ConfigFontSelector}Attribute.cs` | Fluent control types / card rendering |
| `SteamP2PInfo/Config/{GameConfig,OverlayConfig}.cs` | Attribute type args updated; BannerFormat default changed |
| `SteamP2PInfo/WindowSelectDialog.xaml(.cs)` | Fluent picker with search |
| `SteamP2PInfo/OverlayWindow.xaml` | Column alignment polish only |
| `SteamP2PInfo/HotkeyManager.cs` | Drop MahApps `HotKey` overload (int overload kept) |
| `SteamP2PInfo/HotkeyConverter.cs` | **Deleted** |
| `SteamP2PInfo/packages.config`, `SteamP2PInfo.csproj`, `SteamP2PInfo/build.targets` | Dependency swap, new file items, post-build adjustment if needed |
| `SteamP2PInfo/Settings.settings`, `Settings.Designer.cs`, `app.config` | New default Steam log paths |
| `SteamP2PInfo/Resources/app.ico` | App icon (new) |
| `CLAUDE.md` | Updated to describe the new stack |

---

> Note: on this machine's MSBuild (VS 18.6.3) the `-v:m` summary block is sometimes omitted; when the expected `0 Error(s)` line does not appear, trust the process exit code or rerun with `-clp:Summary`.

### Task 1: Housekeeping — commit pending files

**Files:**
- Modify: `.gitignore` (already edited: `.superpowers/` line), `CLAUDE.md` (already created, untracked)

- [x] **Step 1: Commit the pending housekeeping files**

```bash
cd "C:/Users/zeer/Desktop/SteamP2PInfo"
git add CLAUDE.md .gitignore
git commit -m "chore: add CLAUDE.md and ignore .superpowers/ brainstorming artifacts"
```

Expected: commit succeeds, `git status --short` shows a clean tree.

---

### Task 2: Add WPF-UI dependency and verify it works on net48

**Files:**
- Modify: `SteamP2PInfo/packages.config`
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj` (Reference items)

- [x] **Step 1: Add WPF-UI to packages.config**

Insert these two lines into `SteamP2PInfo/packages.config` (keep list alphabetical-ish, exact ids matter):

```xml
  <package id="WPF-UI" version="4.3.0" targetFramework="net48" />
  <package id="WPF-UI.Abstractions" version="4.3.0" targetFramework="net48" />
```

- [x] **Step 2: Restore and inspect what NuGet produced**

```bash
cd "C:/Users/zeer/Desktop/SteamP2PInfo"
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -t:Restore -p:RestorePackagesConfig=true -v:m
ls packages/ | grep -iE "wpf-ui|system.memory|system.buffers|system.numerics.vectors|system.runtime.compilerservices.unsafe"
ls packages/WPF-UI.4.3.0/lib packages/WPF-UI.Abstractions.4.3.0/lib packages/System.Memory.4.6.3/lib
```

Expected: `WPF-UI.4.3.0`, `WPF-UI.Abstractions.4.3.0`, `System.Memory.4.6.3` and the transitive `System.Buffers.*`, `System.Numerics.Vectors.*`, `System.Runtime.CompilerServices.Unsafe.*` appear. The third command prints the real lib folder names (`net472`, `netstandard2.0`, `net462`, `net461`, …) — use exactly those in the next step instead of the example paths.

- [x] **Step 3: Add assembly references to the csproj**

In `SteamP2PInfo/SteamP2PInfo.csproj`, inside the existing `<ItemGroup>` that contains `<Reference Include="MahApps.Metro" ...>` (keep MahApps for now), add:

```xml
    <Reference Include="WPF-UI">
      <HintPath>..\packages\WPF-UI.4.3.0\lib\net472\WPF-UI.dll</HintPath>
    </Reference>
    <Reference Include="WPF-UI.Abstractions">
      <HintPath>..\packages\WPF-UI.Abstractions.4.3.0\lib\netstandard2.0\WPF-UI.Abstractions.dll</HintPath>
    </Reference>
    <Reference Include="System.Memory, Version=4.0.2.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51, processorArchitecture=MSIL">
      <HintPath>..\packages\System.Memory.4.6.3\lib\net462\System.Memory.dll</HintPath>
      <Private>True</Private>
    </Reference>
```

(If Step 2 showed different transitive versions, add matching `Reference` items for `System.Buffers`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe` following the same pattern, using their real `lib\net461` / `lib\net46` paths.)

- [x] **Step 4: Build to verify the net48 consumption works**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`. If MSBuild warns MSB3277 (conflicting versions), that is fine for now; Task 9 resolves the final state.

- [x] **Step 5: MANUAL — confirm the app still starts with the old UI**

Run `SteamP2PInfo/bin/Debug/SteamP2PInfo.exe` (accept the UAC prompt). Expected: the old Metro window opens exactly as before.

- [x] **Step 6: Commit**

```bash
git add SteamP2PInfo/packages.config SteamP2PInfo/SteamP2PInfo.csproj
git commit -m "build: add WPF-UI 4.3.0 dependency (keeps MahApps temporarily)"
```

---

### Task 3: HotKeyBox control; delete HotkeyConverter; trim HotkeyManager

**Files:**
- Create: `SteamP2PInfo/Controls/HotKeyBox.cs`
- Delete: `SteamP2PInfo/HotkeyConverter.cs`
- Modify: `SteamP2PInfo/HotkeyManager.cs`, `SteamP2PInfo/Config/OverlayConfig.cs`, `SteamP2PInfo/MainWindow.xaml`
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj`

- [x] **Step 1: Create `SteamP2PInfo/Controls/HotKeyBox.cs`**

```csharp
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SteamP2PInfo.Controls
{
    /// <summary>
    /// Read-only text box that captures a key combination.
    /// Value uses the same encoding as the config file and HotkeyManager:
    /// low byte = virtual key code, bits 8..11 = ModifierKeys (Alt=1, Control=2, Shift=4, Windows=8).
    /// Press Escape to clear, Tab to move focus.
    /// </summary>
    public class HotKeyBox : TextBox
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(HotKeyBox),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public HotKeyBox()
        {
            IsReadOnly = true;
            IsReadOnlyCaretVisible = false;
            MinWidth = 120; // Format(0) is empty: keep the box visible in the config card
            Text = HotkeyUtils.Format(Value);
            GotFocus += (s, e) => Text = "Press keys\u2026";
            LostFocus += (s, e) => Text = HotkeyUtils.Format(Value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (HotKeyBox)d;
            if (!box.IsFocused)
                box.Text = HotkeyUtils.Format((int)e.NewValue);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            Key key = e.Key;
            if (key == Key.System)
                key = e.SystemKey;
            else if (key == Key.ImeProcessed)
                key = e.ImeProcessedKey;
            else if (key == Key.DeadCharProcessed)
                key = e.DeadCharProcessedKey;

            if (key == Key.Tab)
                return; // let focus move on

            e.Handled = true;

            if (key == Key.Escape)
            {
                Value = 0;
                Text = HotkeyUtils.Format(0);
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return; // wait for a non-modifier key

            // Unmapped or synthetic keys (e.g. Key.None) resolve to vk 0;
            // storing that would silently clear the hotkey.
            if (KeyInterop.VirtualKeyFromKey(key) == 0)
                return;

            Value = HotkeyUtils.FromKeyboard(key);
            Text = HotkeyUtils.Format(Value);
        }
    }

    public static class HotkeyUtils
    {
        public static int FromKeyboard(Key key)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            return (int)Keyboard.Modifiers << 8 | vk;
        }

        public static string Format(int value)
        {
            if (value == 0)
                return "";

            var mods = (ModifierKeys)(value >> 8);
            var parts = new List<string>();
            if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(KeyInterop.KeyFromVirtualKey(value & 0xFF).ToString());
            return string.Join(" + ", parts);
        }
    }
}
```

- [x] **Step 2: Trim `SteamP2PInfo/HotkeyManager.cs`**

Remove `using MahApps.Metro.Controls;` and delete the `AddHotkey(IntPtr hWindow, Func<HotKey> getter, Action handler)` overload (the whole method, including its local `iget()` function). Keep everything else, including `AddHotkey(IntPtr hWindow, Func<int> getter, Action handler)` which `MainWindow` uses.

- [x] **Step 3: Replace the converter's two remaining references, then delete the file and its csproj entry**

`HotkeyConverter` is referenced in exactly two places (verified with `grep -rn "HotkeyConverter" SteamP2PInfo/ --include=*.cs --include=*.xaml`). Remove both **before** deleting the file, or the build breaks:

1. `SteamP2PInfo/Config/OverlayConfig.cs` lines 52–54 currently read:
```csharp
        [ConfigBindingElement("Overlay Hotkey", typeof(HotKeyBox), "HotKeyProperty",
            Tooltip: "Hotkey to show/hide the overlay.",
            ValueConverter: typeof(HotkeyConverter))]
```
Replace that attribute with (fully-qualified type on purpose: bare `HotKeyBox` is ambiguous while `using MahApps.Metro.Controls;` is still in the file; and the new control's DP is `ValueProperty`, not MahApps' `HotKeyProperty`):
```csharp
        [ConfigBindingElement("Overlay Hotkey", typeof(SteamP2PInfo.Controls.HotKeyBox), "ValueProperty",
            Tooltip: "Hotkey to show/hide the overlay.")]
```
Leave the `using MahApps.Metro.Controls;` line in that file alone — the other attributes still use MahApps types until Task 6.
2. `SteamP2PInfo/MainWindow.xaml` line 27: delete the line `<local:HotkeyConverter x:Key="HotkeyConverter" />` (nothing references that resource; it only kept the type alive).

Then delete the file and its csproj entry:

```bash
rm "SteamP2PInfo/HotkeyConverter.cs"
```

In `SteamP2PInfo/SteamP2PInfo.csproj`, remove the line `<Compile Include="HotkeyConverter.cs" />` and add after the `Config\...` compile items:

```xml
    <Compile Include="Controls\HotKeyBox.cs" />
```

- [x] **Step 4: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`. Both `HotkeyConverter` references were removed in Step 3, so the build should not fail on the missing type — if it does, find leftovers with `grep -rn "HotkeyConverter" SteamP2PInfo/ --include=*.cs --include=*.xaml` and remove them.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add HotKeyBox control, drop MahApps HotKey converter dependency"
```

---

### Task 4: Dialog helpers; replace all MahApps dialog calls in MainWindow

**Files:**
- Create: `SteamP2PInfo/Dialogs.cs`
- Modify: `SteamP2PInfo/MainWindow.xaml.cs`
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj`

- [ ] **Step 1: Create `SteamP2PInfo/Dialogs.cs`**

```csharp
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SteamP2PInfo
{
    // WPF-UI types are fully qualified on purpose: `Wpf.Ui.Controls` defines Button/TextBox/MessageBox,
    // which would collide with the System.Windows.Controls usings above.
    static class Dialogs
    {
        public static async Task ShowError(Window owner, string title, string message)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Owner = owner,
                Title = title,
                Content = message,
                CloseButtonText = "OK",
            }.ShowDialogAsync();
        }

        /// <summary>Shows the Steam console instructions; returns true if the command was copied.</summary>
        public static async Task<bool> ShowCopyCommand(Window owner, string command)
        {
            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = owner,
                Title = "Necessary Step",
                Content = $"The Steam console has just been opened. Please enter the following to enable matchmaking call logging:\n\n{command}",
                PrimaryButtonText = "Copy Command",
                CloseButtonText = "Close",
            }.ShowDialogAsync();
            return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }

        /// <summary>Prompts for a Steam AppId. Returns the parsed value, or null if cancelled/invalid.</summary>
        public static async Task<int?> PromptAppId(Window owner)
        {
            var input = new TextBox { MinWidth = 220 };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Please enter the Steam App ID to use with this game:", Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(input);

            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = owner,
                Title = "Steam App ID Required",
                Content = panel,
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Primary,
            }.ShowDialogAsync();

            if (result != MessageBoxResult.Primary)
                return null;

            return uint.TryParse(input.Text, out uint value) ? (int?)value : null;
        }

        public static async Task ShowUpdateAvailable(Window owner, string version, Uri url)
        {
            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = owner,
                Title = "New Version Available",
                Content = $"{version} is out! Click Download to get it.",
                PrimaryButtonText = "Download",
                CloseButtonText = "Close",
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri));
        }
    }
}
```

- [ ] **Step 2: Replace dialog calls in `MainWindow.xaml.cs`**

While `MainWindow` is still a MetroWindow, replace these call sites (do not restructure anything else in this task):

**Never** block on a WPF-UI dialog with `.GetAwaiter().GetResult()` — `ShowDialogAsync` may marshal through `Dispatcher.InvokeAsync`, which deadlocks a blocked UI thread. Use `_ =` for fire-and-forget from event handlers and `await` inside `async void` handlers.

1. Constructor's `this.ShowMessageAsync("New Version Available", ...)` inside the `Task.Run` → keep the same structure but marshal first:
```csharp
                    string v = VersionCheck.LatestRelease["tag_name"].ToString();
                    if (string.Compare(VersionCheck.CurrentVersion, v) < 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            string url = "https://github.com/tremwil/SteamP2PInfo/releases/tag/" + v;
                            linkUpdate.NavigateUri = new Uri(url);
                            textUpdate.Text = string.Format("NEW VERSION ({0}), DOWNLOAD HERE", v);
                            _ = Dialogs.ShowUpdateAvailable(this, v, new Uri(url));
                        });
                    }
```
2. `ShowUnhandledException`: make it `async void` and replace the MetroDialog block with:
```csharp
        private async void ShowUnhandledException(Exception err, string type, bool fatal)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowUnhandledException(err, type, fatal));
                return;
            }

            System.Media.SystemSounds.Exclamation.Play();
            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = $"Unhandled Exception: {err.GetType().Name}",
                Content = $"{err.Message}\n{err.StackTrace}",
                PrimaryButtonText = "Copy",
                CloseButtonText = "Close",
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                Clipboard.SetText($"{err.GetType().Name}: {err.Message}\n{err.StackTrace}");

            Close();
        }
```
   (Keep the call sites of `ShowUnhandledException` unchanged — a lambda or event handler can call an `async void` method. Remove the now-unused `using MahApps.Metro.Controls.Dialogs;` if no other usage remains.)
3. `SteamConsoleHelper`: change its signature to `private async void SteamConsoleHelper()`, keep the `Process.Start(new ProcessStartInfo("steam://open/console"))` line, and replace the Metro dialog block with:
```csharp
            if (await Dialogs.ShowCopyCommand(this, STEAM_COMMAND))
            {
                try
                {
                    Clipboard.SetText(STEAM_COMMAND);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to copy command to clipboard. Please enter '{STEAM_COMMAND}' manually.\n\n {e}", "Write to Clipboard Failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
```
4. The AppId prompt: change `labelGameState_RequestNavigate` to `private async void labelGameState_RequestNavigate(object sender, RequestNavigateEventArgs e)` and replace the `Microsoft.VisualBasic.Interaction.InputBox` block with:
```csharp
                    if (GameConfig.Current.SteamAppId == 0)
                    {
                        int? result = await Dialogs.PromptAppId(this);
                        if (result == null)
                        {
                            MessageBox.Show("Please input a valid number", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        GameConfig.Current.SteamAppId = result.Value;
                    }
```
5. The `this.Invoke(() => ...)` calls in `Timer_Tick` and the constructor: replace with `Dispatcher.Invoke(() => ...)` (MahApps extension being removed).

- [ ] **Step 3: Add `Dialogs.cs` to csproj**

```xml
    <Compile Include="Dialogs.cs" />
```

- [ ] **Step 4: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`.

- [ ] **Step 5: MANUAL — run the app, confirm the window still opens**

Run `SteamP2PInfo/bin/Debug/SteamP2PInfo.exe`. Expected: old UI, opens normally.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: centralize dialogs behind WPF-UI message boxes"
```

---

### Task 5: Fluent shell — App.xaml, MainWindow, SessionView, ConfigView scaffold

This is the big flip. After this task the app uses the Fluent shell; the Config page still hosts the old generated editor (fixed in Task 6) and MahApps is still referenced (removed in Task 9).

**Files:**
- Modify: `SteamP2PInfo/App.xaml`, `SteamP2PInfo/App.xaml.cs`
- Rewrite: `SteamP2PInfo/MainWindow.xaml`
- Rewrite: `SteamP2PInfo/MainWindow.xaml.cs`
- Create: `SteamP2PInfo/Views/SessionView.xaml(.cs)`
- Create: `SteamP2PInfo/Views/ConfigView.xaml(.cs)`
- Create: `SteamP2PInfo/Converters/UiConverters.cs`
- Create: `SteamP2PInfo/SteamAvatar.cs`
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj`

- [ ] **Step 1: Rewrite `SteamP2PInfo/App.xaml`**

```xml
<Application x:Class="SteamP2PInfo.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Dark" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Also rewrite `SteamP2PInfo/App.xaml.cs` so the accent is the fixed Windows blue from the spec (keep any extra members the file may have):

```csharp
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace SteamP2PInfo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            ApplicationAccentColorManager.Apply((Color)ColorConverter.ConvertFromString("#4CC2FF"), ApplicationTheme.Dark);
        }
    }
}
```

(If `ApplicationAccentColorManager` is not the class name in 4.3.0, the build error names the real type in `Wpf.Ui.Appearance`; switch to it and rebuild.)

- [ ] **Step 2: Create `SteamP2PInfo/Converters/UiConverters.cs`**

```csharp
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Steamworks;

namespace SteamP2PInfo.Converters
{
    /// <summary>Hex string ("#AARRGGBB" or "#RRGGBB") to SolidColorBrush. ConverterParameter optionally overrides the alpha (0-255).</summary>
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value as string ?? "#FFFFFFFF");
                if (parameter != null && byte.TryParse(parameter.ToString(), out byte alpha))
                    color = Color.FromArgb(alpha, color.R, color.G, color.B);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Connection quality (0..1) to a pixel width on a 44px bar.</summary>
    public class QualityWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double q = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return Math.Max(0, Math.Min(1, q)) * 44;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>SteamID to a stable avatar fallback color from a fixed palette.</summary>
    public class AvatarColorConverter : IValueConverter
    {
        private static readonly Color[] Palette =
        {
            (Color)ColorConverter.ConvertFromString("#3E6E9E"),
            (Color)ColorConverter.ConvertFromString("#5C4A8C"),
            (Color)ColorConverter.ConvertFromString("#8C6B3A"),
            (Color)ColorConverter.ConvertFromString("#3A7C6B"),
            (Color)ColorConverter.ConvertFromString("#7C3A4E"),
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ulong id = value is CSteamID sid ? sid.m_SteamID : 0;
            var brush = new SolidColorBrush(Palette[(int)(id % (ulong)Palette.Length)]);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Persona name to a single uppercase initial.</summary>
    public class InitialConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string name = value as string;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>SteamID to the peer's Steam avatar, or null (fallback circle shows through).</summary>
    public class AvatarImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is CSteamID sid ? SteamAvatar.Get(sid) : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
```

- [ ] **Step 3: Create `SteamP2PInfo/SteamAvatar.cs`**

```csharp
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Steamworks;

namespace SteamP2PInfo
{
    /// <summary>Loads and caches Steam avatars. Returns null when unavailable (UI falls back to an initial circle).</summary>
    static class SteamAvatar
    {
        private static readonly Dictionary<ulong, ImageSource> cache = new Dictionary<ulong, ImageSource>();

        public static ImageSource Get(CSteamID steamId)
        {
            if (steamId.m_SteamID == 0)
                return null;

            if (cache.TryGetValue(steamId.m_SteamID, out ImageSource cached))
                return cached;

            ImageSource source = null;
            try
            {
                int handle = SteamFriends.GetMediumFriendAvatar(steamId);
                if (handle > 0 && SteamUtils.GetImageSize(handle, out uint width, out uint height) && width > 0 && height > 0)
                {
                    byte[] data = new byte[width * height * 4];
                    if (SteamUtils.GetImageRGBA(handle, data, data.Length))
                    {
                        var bitmap = BitmapSource.Create((int)width, (int)height, 96, 96, PixelFormats.Bgra32, null, data, (int)width * 4);
                        bitmap.Freeze();
                        source = bitmap;
                    }
                }
            }
            catch
            {
                source = null;
            }

            cache[steamId.m_SteamID] = source;
            return source;
        }
    }
}
```

(If avatars render with swapped red/blue, change `PixelFormats.Bgra32` to `Rgba32` — Steam's byte order differs by platform.)

- [ ] **Step 4: Create `SteamP2PInfo/Views/SessionView.xaml`**

```xml
<UserControl x:Class="SteamP2PInfo.Views.SessionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             xmlns:conv="clr-namespace:SteamP2PInfo.Converters">
    <UserControl.Resources>
        <conv:HexToBrushConverter x:Key="HexBrush" />
        <conv:QualityWidthConverter x:Key="QualityWidth" />
        <conv:AvatarColorConverter x:Key="AvatarColor" />
        <conv:InitialConverter x:Key="Initial" />
        <conv:AvatarImageConverter x:Key="AvatarImage" />

        <DataTemplate x:Key="PeerCardTemplate">
            <Border Background="#2B2B2B" CornerRadius="6" Padding="10,10,14,10" Margin="0,0,0,8"
                    MouseDoubleClick="PeerCard_MouseDoubleClick" Cursor="Hand">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>

                    <Grid Width="32" Height="32" VerticalAlignment="Center">
                        <Ellipse Fill="{Binding SteamID, Converter={StaticResource AvatarColor}}" />
                        <TextBlock Text="{Binding Name, Converter={StaticResource Initial}}" Foreground="White"
                                   FontSize="13" FontWeight="SemiBold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                        <Image Source="{Binding SteamID, Converter={StaticResource AvatarImage}}" Stretch="UniformToFill" />
                    </Grid>

                    <StackPanel Grid.Column="1" Margin="12,0,0,0" VerticalAlignment="Center">
                        <TextBlock Text="{Binding Name}" FontSize="13.5" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" />
                        <TextBlock Text="{Binding SteamID}" FontSize="11" Foreground="#8F8F8F" FontFamily="Consolas"
                                   Typography.NumeralAlignment="Tabular" />
                    </StackPanel>

                    <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                        <Border CornerRadius="999" Padding="10,3"
                                Background="{Binding PingColor, Converter={StaticResource HexBrush}, ConverterParameter=36}">
                            <TextBlock Text="{Binding Ping, StringFormat={}{0:N0} ms}"
                                       Foreground="{Binding PingColor, Converter={StaticResource HexBrush}}"
                                       FontSize="12.5" FontWeight="SemiBold" Typography.NumeralAlignment="Tabular" />
                        </Border>
                        <Border Width="44" Height="4" CornerRadius="2" Background="#1AFFFFFF" Margin="14,0,0,0" VerticalAlignment="Center">
                            <Border HorizontalAlignment="Left" CornerRadius="2"
                                    Background="{Binding PingColor, Converter={StaticResource HexBrush}}"
                                    Width="{Binding ConnectionQuality, Converter={StaticResource QualityWidth}}" />
                        </Border>
                        <TextBlock Text="{Binding ConnectionQuality, StringFormat={}{0:N2}}" Foreground="#B8B8B8"
                                   FontSize="11.5" Margin="7,0,0,0" VerticalAlignment="Center" Typography.NumeralAlignment="Tabular" />
                    </StackPanel>
                </Grid>
            </Border>
        </DataTemplate>
    </UserControl.Resources>

    <Grid>
        <!-- empty state (shown until a game is attached) -->
        <Grid x:Name="emptyState">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <Border Width="54" Height="54" CornerRadius="13" Background="#0DFFFFFF" HorizontalAlignment="Center">
                    <ui:SymbolIcon Symbol="Games24" FontSize="24" Foreground="#B8B8B8" HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <TextBlock Text="No game attached" FontSize="15" FontWeight="SemiBold" HorizontalAlignment="Center" Margin="0,14,0,6" />
                <TextBlock TextAlignment="Center" FontSize="12" Foreground="#9A9A9A" LineHeight="20"
                           Text="Start your game, then attach to its window.&#x0a;First time? You will be asked for the game's Steam AppId." />
                <Button x:Name="attachButton" Content="Attach Game" HorizontalAlignment="Center" Margin="0,18,0,0"
                        Padding="24,8" FontWeight="SemiBold" Click="AttachButton_Click" />
            </StackPanel>
        </Grid>

        <!-- 3: attached content -->
        <Grid x:Name="attachedContent" Visibility="Collapsed">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <StackPanel Margin="0,0,0,14">
                <TextBlock x:Name="gameName" FontSize="19" FontWeight="SemiBold" />
                <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                    <Ellipse Width="7" Height="7" Fill="#6CCB5F" VerticalAlignment="Center" />
                    <TextBlock x:Name="statusText" FontSize="11.5" Foreground="#9A9A9A" Margin="6,0,0,0" />
                </StackPanel>
            </StackPanel>

            <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                <ItemsControl x:Name="peerList" ItemTemplate="{StaticResource PeerCardTemplate}" />
            </ScrollViewer>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 5: Create `SteamP2PInfo/Views/SessionView.xaml.cs`**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteamP2PInfo.Config;

namespace SteamP2PInfo.Views
{
    public partial class SessionView : UserControl
    {
        public event Action AttachRequested;

        public SessionView()
        {
            InitializeComponent();
        }

        public void SetPeerSource(ObservableCollection<SteamPeerBase> peers)
        {
            peerList.ItemsSource = peers;
        }

        public void ShowAttached(string title)
        {
            emptyState.Visibility = Visibility.Collapsed;
            attachedContent.Visibility = Visibility.Visible;
            gameName.Text = title;
        }

        public void UpdateStatus(int peerCount)
        {
            statusText.Text = $"Connected \u00b7 {peerCount} player{(peerCount == 1 ? "" : "s")}";
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            AttachRequested?.Invoke();
        }

        private void PeerCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is SteamPeerBase peer))
                return;

            if (GameConfig.Current.OpenProfileInOverlay)
                Steamworks.SteamFriends.ActivateGameOverlayToUser("steamid", peer.SteamID);
            else
                Process.Start($"https://steamcommunity.com/profiles/{peer.SteamID}");
        }
    }
}
```

- [ ] **Step 6: Create `SteamP2PInfo/Views/ConfigView.xaml`**

```xml
<UserControl x:Class="SteamP2PInfo.Views.ConfigView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:SteamP2PInfo">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <TextBlock Text="Config" FontSize="19" FontWeight="SemiBold" />
            <TextBlock x:Name="subtitleText" FontSize="11.5" Foreground="#9A9A9A" Margin="0,4,0,14"
                       Text="Settings are saved per game" />

            <StackPanel x:Name="generatedEditor" />

            <!-- STEAM card: always visible -->
            <Border Background="#2B2B2B" CornerRadius="6" Padding="2,0,14,2" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock Text="STEAM" FontSize="10.5" Foreground="#9A9A9A" Margin="0,10,0,6" />
                    <Grid Margin="0,7">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="IPC log file path" FontSize="12.5" VerticalAlignment="Center"
                                   ToolTip="Path to the Steam IPC log file. Only change this if you installed Steam somewhere else." />
                        <TextBox Grid.Column="1" Text="{Binding Source={x:Static local:Settings.Default}, Path=SteamLogPath, UpdateSourceTrigger=PropertyChanged}"
                                 FontSize="11.5" HorizontalAlignment="Right" MinWidth="420" />
                    </Grid>
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 7: Create `SteamP2PInfo/Views/ConfigView.xaml.cs`**

```csharp
using System.Windows.Controls;
using SteamP2PInfo.Config;

namespace SteamP2PInfo.Views
{
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();
        }

        /// <summary>Rebuilds the generated settings cards for GameConfig.Current (call after attach).</summary>
        public void RebuildEditor(string gameTitle)
        {
            generatedEditor.Children.Clear();

            if (GameConfig.Current == null)
                return;

            subtitleText.Text = $"Settings for {gameTitle} \u00b7 saved to config\\{GameConfig.Current.ProcessName}.json";
            generatedEditor.Children.Add(ConfigUIBuilder.CreateConfigEditor(GameConfig.Current));
        }
    }
}
```

(At this point `ConfigUIBuilder.CreateConfigEditor` still returns the old MahApps-based grid; it will be replaced in Task 6. The call signature is already the final one.)

- [ ] **Step 8: Rewrite `SteamP2PInfo/MainWindow.xaml`**

```xml
<ui:FluentWindow x:Class="SteamP2PInfo.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        xmlns:views="clr-namespace:SteamP2PInfo.Views"
        Title="Steam P2P Info"
        Width="800" Height="450" MinWidth="720" MinHeight="420"
        FontFamily="Segoe UI Variable Text, Segoe UI"
        WindowStartupLocation="CenterScreen"
        WindowBackdropType="Mica"
        WindowCornerPreference="Round">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ui:TitleBar Title="SteamP2PInfo" />

        <ui:Button x:Name="updateBadge" Grid.Row="0" Visibility="Collapsed" HorizontalAlignment="Right" VerticalAlignment="Center"
                   Margin="0,0,150,0" Padding="10,3" Click="UpdateBadge_Click" />

        <ui:NavigationView x:Name="navigationView" Grid.Row="1" OpenPaneLength="152">
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem x:Name="navSession" Content="Session" Icon="{ui:SymbolIcon People24}" />
                <ui:NavigationViewItem x:Name="navConfig" Content="Config" Icon="{ui:SymbolIcon Settings24}" />
            </ui:NavigationView.MenuItems>
            <ContentControl x:Name="rootContent" />
        </ui:NavigationView>

        <Border Grid.Row="2" BorderBrush="#14FFFFFF" BorderThickness="0,1,0,0" Padding="16,7">
            <Grid>
                <TextBlock Text="F2 toggles the overlay  &#183;  Double-click a player to open their Steam profile"
                           FontSize="11" Foreground="#8F8F8F" />
                <TextBlock Text="Made by tremwil &#183; Redesigned by zeer" FontSize="11" Foreground="#8F8F8F"
                           HorizontalAlignment="Right" />
            </Grid>
        </Border>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 9: Rewrite `SteamP2PInfo/MainWindow.xaml.cs`**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Steamworks;
using SteamP2PInfo.Config;
using SteamP2PInfo.Views;

namespace SteamP2PInfo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private ObservableCollection<SteamPeerBase> peers;
        private OverlayWindow overlay;
        private Timer timer;
        private int timerTicks = 0;
        private int overlayHotkey = 0;
        private int previousPeersAmount = 0;
        private string updateUrl;

        private const string STEAM_COMMAND = "log_ipc \"BeginAuthSession,EndAuthSession,LeaveLobby,SendClanChatMessage\"";

        private readonly SessionView sessionView = new SessionView();
        private readonly ConfigView configView = new ConfigView();

        private WindowSelectDialog.WindowInfo wInfo;

        public MainWindow()
        {
            if (Process.GetProcessesByName("SteamP2PInfo").Length > 1)
            {
                MessageBox.Show("Cannot run 2 instances of Steam P2P Info at once.", "Program Already Running", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowUnhandledException((Exception)e.ExceptionObject, "CurrentDomain", e.IsTerminating);
            TaskScheduler.UnobservedTaskException += (s, e) => ShowUnhandledException(e.Exception, "TaskScheduler", false);
            Dispatcher.UnhandledException += (s, e) => { if (!Debugger.IsAttached) ShowUnhandledException(e.Exception, "Dispatcher", true); };

            InitializeComponent();
            Closing += MainWindow_Closed;

            peers = new ObservableCollection<SteamPeerBase>();
            sessionView.SetPeerSource(peers);
            sessionView.AttachRequested += AttachGame;

            navigationView.SelectionChanged += (s, e) => UpdateRootContent();
            navigationView.SelectedItem = navSession;
            UpdateRootContent();

            Title = "Steam P2P Info " + VersionCheck.CurrentVersion;

            timer = new Timer(Timer_Tick, null, Timeout.Infinite, Timeout.Infinite);
            Settings.Default.PropertyChanged += (s, e) => Settings.Default.Save();

            Task.Run(() =>
            {
                if (VersionCheck.FetchLatest())
                {
                    string v = VersionCheck.LatestRelease["tag_name"].ToString();
                    if (string.Compare(VersionCheck.CurrentVersion, v) < 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            updateUrl = "https://github.com/tremwil/SteamP2PInfo/releases/tag/" + v;
                            updateBadge.Content = $"Update {v} available";
                            updateBadge.Visibility = Visibility.Visible;
                            _ = Dialogs.ShowUpdateAvailable(this, v, new Uri(updateUrl));
                        });
                    }
                }
            });
        }

        private void UpdateRootContent()
        {
            rootContent.Content = ReferenceEquals(navigationView.SelectedItem, navConfig) ? (object)configView : sessionView;
        }

        private void UpdateBadge_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(updateUrl))
                Process.Start(new ProcessStartInfo(updateUrl));
        }

        private void Timer_Tick(object o)
        {
            Dispatcher.Invoke(() =>
            {
                // Necessary to close the program after the game exits, as SteamAPI_Shutdown isn't
                // sufficient to have steam recognize the game is no longer running
                if (!WinAPI.User32.IsWindow(wInfo.Handle))
                    Close();

                if (HotkeyManager.Enabled && !GameConfig.Current.HotkeysEnabled)
                    HotkeyManager.Disable();

                if (!HotkeyManager.Enabled && GameConfig.Current.HotkeysEnabled)
                    HotkeyManager.Enable();

                if ((timerTicks = (timerTicks + 1) % 6) == 0)
                {
                    // Rather not have the settings update on a loop, but 
                    // Fody generated OnChange seems to break PropertyChanged 
                    // for GameConfig. So do this for now.
                    GameConfig.Current?.Save();
                    SteamPeerManager.UpdatePeerList();
                }

                peers.Clear();
                foreach (SteamPeerBase p in SteamPeerManager.GetPeers())
                    peers.Add(p);
                sessionView.UpdateStatus(peers.Count);

                if (GameConfig.Current.PlaySoundOnNewSession)
                {
                    if (peers.Count > 0 && previousPeersAmount == 0)
                    {
                        System.Media.SystemSounds.Beep.Play();
                    }
                    previousPeersAmount = peers.Count;
                }

                // Update overlay column sizes
                foreach (var col in overlay.dataGrid.Columns)
                {
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.Pixel);
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                }
                overlay.dataGrid.UpdateLayout();

                // Queue position update after the overlay has re-rendered
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(overlay.UpdatePosition));
                overlay.UpdateVisibility();
            });
        }

        private async void ShowUnhandledException(Exception err, string type, bool fatal)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowUnhandledException(err, type, fatal));
                return;
            }

            System.Media.SystemSounds.Exclamation.Play();
            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = $"Unhandled Exception: {err.GetType().Name}",
                Content = $"{err.Message}\n{err.StackTrace}",
                PrimaryButtonText = "Copy",
                CloseButtonText = "Close",
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                Clipboard.SetText($"{err.GetType().Name}: {err.Message}\n{err.StackTrace}");

            Close();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (GameConfig.Current != null) GameConfig.Current.Save();
            Settings.Default.Save();
            if (overlay != null) overlay.Close();
            HotkeyManager.Disable();
            ETWPingMonitor.Stop();
        }

        private async void AttachGame()
        {
            if (wInfo != null)
                return;

            WindowSelectDialog dialog = new WindowSelectDialog() { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            GameConfig.LoadOrCreate(dialog.SelectedWindow.ProcessName);

            if (!Directory.Exists(Path.GetDirectoryName(Settings.Default.SteamLogPath)))
            {
                await Dialogs.ShowError(this, "Directory Not Found", "Steam IPC log file directory does not exist. Please modify the config accordingly.");
                return;
            }

            if (GameConfig.Current.SteamAppId == 0)
            {
                int? appId = await Dialogs.PromptAppId(this);
                if (appId == null)
                {
                    await Dialogs.ShowError(this, "Input Error", "Please input a valid number");
                    return;
                }
                GameConfig.Current.SteamAppId = appId.Value;
            }

            Environment.SetEnvironmentVariable("SteamAppId", GameConfig.Current.SteamAppId.ToString());
            if (!SteamAPI.Init())
            {
                await Dialogs.ShowError(this, "Steam API Error", "Could not initialize Steam API. Make sure the provided AppId is valid!");
                GameConfig.Current.SteamAppId = 0;
                GameConfig.Current.Save();
                return;
            }

            wInfo = dialog.SelectedWindow;
            SteamPeerManager.Init();

            if (MustEnterSteamCommand())
                SteamConsoleHelper();

            HotkeyManager.RemoveHotkey(overlayHotkey);
            overlayHotkey = HotkeyManager.AddHotkey(wInfo.Handle, () => GameConfig.Current.OverlayConfig.Hotkey, () => GameConfig.Current.OverlayConfig.Enabled ^= true);

            overlay = new OverlayWindow(wInfo.Handle, wInfo.ProcessId, wInfo.ThreadId);
            overlay.dataGrid.DataContext = peers;
            if (!overlay.InstallMsgHook())
            {
                overlay.Close();
                await Dialogs.ShowError(this, "WINAPI Error", "Failed to setup overlay message hook");
                Close();
                return;
            }

            sessionView.ShowAttached(wInfo.Title);
            configView.RebuildEditor(wInfo.Title);

            ETWPingMonitor.Start();
            timer.Change(0, 1000);
        }

        private bool MustEnterSteamCommand()
        {
            DateTime ipcLogDate;
            String startupDateString = null;

            // Check if the program was recently updated -- We'll want to enter the command again if so
            if (Settings.Default.LastRunVersion != VersionCheck.CurrentVersion)
            {
                Settings.Default.LastRunVersion = VersionCheck.CurrentVersion;
                Settings.Default.Save();
                return true;
            }

            if (!File.Exists(Settings.Default.SteamLogPath))
                return true;

            try
            {
                ipcLogDate = File.GetLastWriteTime(Settings.Default.SteamLogPath);
            }
            catch (Exception)
            {
                return true;
            }

            try
            {
                using (FileStream stream = new FileStream(Settings.Default.SteamBootstrapLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var reader = new ReverseTextReader(stream, Encoding.UTF8);
                    var today = DateTime.Today;
                    int dateCheckCountdown = 20;
                    while (!reader.EndOfStream)
                    {
                        String line = reader.ReadLine();
                        if (line.Trim().Length == 0)
                            continue;
                        else if (line.Contains("Startup - updater built"))
                        {
                            int substringStartIndex = line.IndexOf("[") + 1;
                            startupDateString = line.Substring(substringStartIndex, line.IndexOf("]") - substringStartIndex);
                            break;
                        }
                        else
                        {
                            if (--dateCheckCountdown == 0)
                            {
                                int substringStartIndex = line.IndexOf("[") + 1;
                                DateTime lineDate = DateTime.Parse(line.Substring(substringStartIndex, line.IndexOf("]") - substringStartIndex));
                                if (today.Subtract(lineDate).TotalHours > 24)
                                    return true; // let's assume Steam hasn't been running for 24+ hours
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return true;
            }

            return startupDateString == null || DateTime.Parse(startupDateString) > ipcLogDate;
        }

        private async void SteamConsoleHelper()
        {
            Process.Start(new ProcessStartInfo("steam://open/console"));

            if (await Dialogs.ShowCopyCommand(this, STEAM_COMMAND))
            {
                try
                {
                    Clipboard.SetText(STEAM_COMMAND);
                }
                catch (Exception e)
                {
                    await Dialogs.ShowError(this, "Write to Clipboard Failed!", $"Failed to copy command to clipboard. Please enter '{STEAM_COMMAND}' manually.\n\n {e}");
                }
            }
        }
    }
}
```

Notes: the old `headerFmt_MouseDoubleClick` and `webLink_RequestNavigate` handlers are intentionally dropped (their XAML elements no longer exist); the old `dataGridSession_DoubleClick` logic now lives in `SessionView.PeerCard_MouseDoubleClick`.

- [ ] **Step 10: Update csproj items**

Add to `SteamP2PInfo/SteamP2PInfo.csproj`:

```xml
    <Compile Include="Converters\UiConverters.cs" />
    <Compile Include="SteamAvatar.cs" />
    <Compile Include="Views\ConfigView.xaml.cs">
      <DependentUpon>ConfigView.xaml</DependentUpon>
    </Compile>
    <Compile Include="Views\SessionView.xaml.cs">
      <DependentUpon>SessionView.xaml</DependentUpon>
    </Compile>
    <Page Include="Views\ConfigView.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
    <Page Include="Views\SessionView.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
```

- [ ] **Step 11: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`. If `Symbol="Games24"` or `OpenPaneLength` does not exist, the error message lists valid alternatives — pick the nearest match (e.g. another 24px game/people icon) and rebuild.

- [ ] **Step 12: MANUAL — run and check the Fluent shell**

Run `SteamP2PInfo/bin/Debug/SteamP2PInfo.exe`. Expected: dark Fluent window with title bar, left navigation with Session/Config, empty state with **Attach Game** button, bottom bar showing the credits **"Made by tremwil · Redesigned by zeer"**. Window should have rounded corners and a Mica backdrop. Config tab shows the STEAM card (the generated cards are still the old-styled ones until Task 6).

Known risk to watch here: `Wpf.Ui` references `System.Runtime.CompilerServices.Unsafe 6.0.3.0`, but this repo ships `System.Runtime.CompilerServices.Unsafe 6.0.0.0` (Task 2 recorded 18 MSB3277 warnings for exactly this). Costura's version-agnostic `AssemblyResolve` normally makes that harmless. If startup instead fails with a `FileLoadException`/`TypeLoadException` naming `System.Runtime.CompilerServices.Unsafe`, add this inside `<configuration>` in `SteamP2PInfo/app.config` (create the `<runtime>` section if absent), rebuild, and retest:

```xml
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Runtime.CompilerServices.Unsafe" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-6.0.3.0" newVersion="6.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
```

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "feat: Fluent shell (FluentWindow + NavigationView), SessionView and ConfigView"
```

---

### Task 6: Config system in Fluent style

**Files:**
- Rewrite: `SteamP2PInfo/Config/ConfigUIBuilder.cs`
- Rewrite: `SteamP2PInfo/Config/ConfigCategoryAttribute.cs`
- Modify: `SteamP2PInfo/Config/ConfigBindingElementAttribute.cs`
- Modify: `SteamP2PInfo/Config/ConfigEnumComboBoxAttribute.cs`
- Modify: `SteamP2PInfo/Config/ConfigFontSelectorAttribute.cs`
- Create: `SteamP2PInfo/Config/ConfigPingColorsPreviewAttribute.cs`
- Create: `SteamP2PInfo/Controls/ColorBox.cs`
- Modify: `SteamP2PInfo/Config/GameConfig.cs`, `SteamP2PInfo/Config/OverlayConfig.cs`
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj`

- [ ] **Step 1: Create `SteamP2PInfo/Controls/ColorBox.cs`**

`Wpf.Ui.Controls.ColorPicker` in 4.3.0 was verified by reflection to declare **no members at all** (no properties, events, or fields — an empty `Control` shell), so it cannot edit a color. The approved design spec (§5.1) already allows this fallback: edit the hex string directly in a self-tinting TextBox.

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Controls
{
    /// <summary>
    /// Hex color editor for the config UI: a TextBox holding a "#AARRGGBB" (or "#RRGGBB") string
    /// that tints itself to the parsed color. The value round-trips as the same string the config
    /// JSON stores, so no value converter is involved.
    /// </summary>
    public class ColorBox : TextBox
    {
        private static readonly Brush InvalidBorder = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

        public ColorBox()
        {
            MinWidth = 120;
            FontFamily = new FontFamily("Consolas");
            ApplyPreview();
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            ApplyPreview();
        }

        private void ApplyPreview()
        {
            if (TryParse(Text, out Color color))
            {
                BorderBrush = new SolidColorBrush(color);
                Background = new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B));
            }
            else
            {
                BorderBrush = InvalidBorder;
                Background = Brushes.Transparent;
            }
        }

        private static bool TryParse(string text, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                color = (Color)ColorConverter.ConvertFromString(text.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: Create `SteamP2PInfo/Config/ConfigPingColorsPreviewAttribute.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    /// <summary>Read-only preview of the ping color thresholds (editing stays JSON-only for now).</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigPingColorsPreviewAttribute : Attribute, IConfigUIElement
    {
        public string OptionName { get; }
        public string Tooltip { get; }
        public bool Multicolumn => false;

        public ConfigPingColorsPreviewAttribute(string optionName, string tooltip = null)
        {
            OptionName = optionName;
            Tooltip = tooltip;
        }

        public UIElement CreateUIElement(object source, string path)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var ranges = source.GetType()
                .GetProperty(path, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.GetValue(source) as IEnumerable<OverlayConfig.PingColorRange>;

            if (ranges != null)
            {
                foreach (var range in ranges)
                {
                    var dot = new Border
                    {
                        Width = 10,
                        Height = 10,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(range.Color)),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var text = new TextBlock
                    {
                        Text = range.Threshold + "+",
                        FontSize = 10.5,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A9A9A")),
                        Margin = new Thickness(5, 0, 12, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    panel.Children.Add(dot);
                    panel.Children.Add(text);
                }
            }

            return panel;
        }
    }
}
```

- [ ] **Step 3: Rewrite `SteamP2PInfo/Config/ConfigCategoryAttribute.cs`** (renders a settings card containing the nested config's rows)

```csharp
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigCategoryAttribute : Attribute, IConfigUIElement
    {
        public string OptionName { get; }
        public string Tooltip { get; }
        public bool Multicolumn => true;

        public ConfigCategoryAttribute(string categoryName, string tooltip = null)
        {
            OptionName = categoryName;
            Tooltip = tooltip;
        }

        public UIElement CreateUIElement(object source, string path)
        {
            object subConfig = source.GetType()
                .GetProperty(path, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .GetValue(source);

            return ConfigUIBuilder.CreateCard(OptionName, Tooltip, ConfigUIBuilder.CreateRowsPanel(subConfig));
        }
    }
}
```

- [ ] **Step 4: Rewrite `SteamP2PInfo/Config/ConfigUIBuilder.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    public static class ConfigUIBuilder
    {
        /// <summary>Full editor: a GENERAL card for top-level options plus one card per [ConfigCategory] property.</summary>
        public static StackPanel CreateConfigEditor(object config)
        {
            var panel = new StackPanel();

            var generalRows = CreateRowsPanel(config, skipCategories: true);
            if (generalRows.Children.Count > 0)
                panel.Children.Add(CreateCard("GENERAL", null, generalRows));

            foreach (var prop in GetEditableProperties(config))
            {
                var attr = GetAttribute(prop);
                if (attr is ConfigCategoryAttribute)
                    panel.Children.Add((UIElement)attr.CreateUIElement(config, prop.Name));
            }

            return panel;
        }

        /// <summary>Rows only (label left, control right, hairline separators) — no outer card.</summary>
        public static StackPanel CreateRowsPanel(object config, bool skipCategories = false)
        {
            var rows = new StackPanel();
            bool first = true;

            foreach (var prop in GetEditableProperties(config))
            {
                var attr = GetAttribute(prop);
                if (skipCategories && attr is ConfigCategoryAttribute)
                    continue;

                UIElement element = attr.CreateUIElement(config, prop.Name);

                var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var label = new TextBlock
                {
                    Text = attr.OptionName,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = attr.Tooltip,
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                element.SetValue(Grid.ColumnProperty, 1);
                if (element is FrameworkElement fe)
                    fe.HorizontalAlignment = HorizontalAlignment.Right;
                row.Children.Add(element);

                if (!first)
                {
                    var separator = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14FFFFFF")),
                    };
                    rows.Children.Add(separator);
                }
                first = false;
                rows.Children.Add(row);
            }

            return rows;
        }

        /// <summary>Card container matching the approved design: #2B2B2B, radius 6, uppercase header.</summary>
        public static Border CreateCard(string header, string tooltip, UIElement content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 10.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A9A9A")),
                Margin = new Thickness(0, 10, 0, 2),
                ToolTip = tooltip,
            });
            stack.Children.Add(content);

            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2B2B")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 0, 14, 2),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack,
            };
        }

        private static PropertyInfo[] GetEditableProperties(object config)
        {
            return config.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(p => GetAttribute(p) != null)
                .ToArray();
        }

        private static IConfigUIElement GetAttribute(PropertyInfo property)
        {
            return property.GetCustomAttributes().FirstOrDefault(a => a is IConfigUIElement) as IConfigUIElement;
        }
    }
}
```

- [ ] **Step 5: Small updates to the remaining attribute files**

`ConfigBindingElementAttribute.cs` and `ConfigEnumComboBoxAttribute.cs` and `ConfigFontSelectorAttribute.cs` keep their current logic. Apply only these tweaks:

1. `ConfigEnumComboBoxAttribute`: change the ComboBox creation so it renders right-aligned and themed — no change needed to code (plain `ComboBox` is themed by `ControlsDictionary`); leave the file as is.
2. `ConfigFontSelectorAttribute`: no code change needed (plain `Button` is themed; the `System.Windows.Forms.FontDialog` stays).
3. `ConfigBindingElementAttribute`: add a guard where it resolves the target DependencyProperty by reflection. Today a wrong DP name throws a bare `NullReferenceException` the first time the config tab renders (hit twice during planning: MahApps `IsOnProperty` vs WPF-UI `IsCheckedProperty`, and the ColorPicker's non-existent `SelectedColorProperty`). Read the attribute's existing resolution code and make a missing field fail loudly: keep the same `GetField(..., BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)` call, but when it returns null throw an `InvalidOperationException` naming the DP string, the control type, and the option it belongs to (use the option name the attribute already holds). No other behavior change.

- [ ] **Step 6: Update `SteamP2PInfo/Config/GameConfig.cs` attribute arguments**

Replace `using MahApps.Metro.Controls;` with `using Wpf.Ui.Controls;`. Then fix the toggle DP name: WPF-UI's `ToggleSwitch` derives from `ToggleButton`, and its state DP is **`IsCheckedProperty`** — there is no `IsOnProperty` in 4.3.0 (verified by reflection). `OnContent`/`OffContent` do exist, so leave those UIElementProperties alone. Change the DP name in every toggle attribute in both files:

```bash
sed -i 's/"IsOnProperty"/"IsCheckedProperty"/g' SteamP2PInfo/Config/GameConfig.cs SteamP2PInfo/Config/OverlayConfig.cs
grep -rn "IsOnProperty" SteamP2PInfo/Config/ || echo "OK: zero hits"
grep -c "IsCheckedProperty" SteamP2PInfo/Config/GameConfig.cs
```

Expected: no `IsOnProperty` hits; 5 hits in `GameConfig.cs` (its five toggles).

- [ ] **Step 7: Update `SteamP2PInfo/Config/OverlayConfig.cs`**

Replace `using MahApps.Metro.Controls;` with `using Wpf.Ui.Controls;` and update attribute arguments:

1. `Hotkey`: already migrated to the custom control in Task 3 — verify it reads exactly `[ConfigBindingElement("Overlay Hotkey", typeof(SteamP2PInfo.Controls.HotKeyBox), "ValueProperty", Tooltip: "Hotkey to show/hide the overlay.")]` and change nothing here.
2. `XOffset` / `YOffset`: `typeof(NumberBox)`, `"ValueProperty"`, with `UIElementProperties: new object[] { new object[] { "Minimum", 0d }, new object[] { "Maximum", 1d } }` (drop the MahApps-only `Interval`/`ParsingNumberStyle`/`StringFormat` entries).
3. `StrokeWidth`: `typeof(NumberBox)`, `"ValueProperty"`, with `UIElementProperties: new object[] { new object[] { "Minimum", 0d } }`.
4. `TextColor` / `StrokeColor`: replace `typeof(ColorPicker), "SelectedColorProperty"` with `typeof(SteamP2PInfo.Controls.ColorBox), "TextProperty"` and **remove** the `ValueConverter: typeof(ColorStringConverter)` argument — `ColorBox` binds the inherited `TextBox.TextProperty` directly, and both the config value and the control's text are `"#AARRGGBB"` strings. Same change for Stroke Color.
5. `BannerFormat` default value: change
```csharp
        public string BannerFormat { get; set; } = "[{time:HH:mm:ss}] SteamP2PInfo - by tremwil";
```
to
```csharp
        public string BannerFormat { get; set; } = "{time:HH:mm:ss}";
```
6. `PingColors`: add the preview attribute above the existing `[JsonProperty]`:
```csharp
        [JsonProperty("ping_colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [ConfigPingColorsPreview("Ping Colors", "Ping color thresholds. Edit the JSON config to change them.")]
        public List<PingColorRange> PingColors { get; set; } = new List<PingColorRange>()
```

- [ ] **Step 8: Update `SteamP2PInfo/Views/ConfigView.xaml` if needed**

No change expected — `RebuildEditor` already calls the new `CreateConfigEditor` signature. `ConfigUIBuilder.CreateConfigEditor` returns `StackPanel`, matching `generatedEditor.Children.Add(...)`.

- [ ] **Step 9: Add new files to csproj**

```xml
    <Compile Include="Controls\ColorBox.cs" />
    <Compile Include="Config\ConfigPingColorsPreviewAttribute.cs" />
```

- [ ] **Step 10: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`.

- [ ] **Step 11: MANUAL — config page check**

Run the app, attach to a game if convenient (or just open Config before attaching to check the STEAM card). After attach: GENERAL and OVERLAY cards appear; toggles switch; the hotkey box shows "F2" after clicking and pressing F2; color rows open the WPF-UI color picker; editing anything then closing the app updates `config\{process}.json` (check the `overlay.hotkey`, `overlay.banner_format` fields).

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: Fluent config cards, WPF-UI controls, ping color preview, new banner default"
```

---

### Task 7: Fluent window picker with search

**Files:**
- Rewrite: `SteamP2PInfo/WindowSelectDialog.xaml`
- Rewrite: `SteamP2PInfo/WindowSelectDialog.xaml.cs`

- [ ] **Step 1: Rewrite `SteamP2PInfo/WindowSelectDialog.xaml`**

```xml
<ui:FluentWindow x:Class="SteamP2PInfo.WindowSelectDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        Title="Select game window"
        Width="400" Height="480"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner"
        WindowBackdropType="Mica"
        WindowCornerPreference="Round">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel>
            <TextBlock Text="Select game window" FontSize="15" FontWeight="SemiBold" />
            <TextBlock Text="Pick the window of the game you want to monitor" FontSize="11" Foreground="#9A9A9A" Margin="0,3,0,0" />
        </StackPanel>

        <ui:TextBox x:Name="searchBox" Grid.Row="1" Margin="0,12,0,8" FontSize="12"
                    PlaceholderText="Search windows&#8230;" TextChanged="SearchBox_TextChanged" />

        <ListBox x:Name="WindowListBox" Grid.Row="2" MouseDoubleClick="WindowListBox_MouseDoubleClick"
                 SelectionChanged="WindowListBox_SelectionChanged">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Width="330">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="{Binding Title}" FontSize="12.5" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
                        <TextBlock Grid.Column="1" Text="{Binding ProcessName}" FontSize="11" Foreground="#9A9A9A"
                                   Margin="10,0,0,0" VerticalAlignment="Center" />
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Cancel" Padding="16,7" Margin="0,0,8,0" Click="CancelButton_Click" />
            <Button x:Name="btnOpen" Content="Attach" Padding="18,7" IsEnabled="False" Click="OpenButton_Click" />
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 2: Rewrite `SteamP2PInfo/WindowSelectDialog.xaml.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteamP2PInfo.WinAPI;
using Wpf.Ui.Controls;

namespace SteamP2PInfo
{
    /// <summary>
    /// Interaction logic for WindowSelectDialog.xaml
    /// </summary>
    public partial class WindowSelectDialog : FluentWindow
    {
        public class WindowInfo
        {
            public IntPtr Handle;
            public string Title;
            public string ProcessName;
            public uint ProcessId;
            public uint ThreadId;
        }

        private List<WindowInfo> windows = new List<WindowInfo>();
        public WindowInfo SelectedWindow = null;

        public WindowSelectDialog()
        {
            InitializeComponent();

            IntPtr shellWindow = User32.GetShellWindow();

            User32.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == shellWindow) return true;
                if (!User32.IsWindowVisible(hWnd)) return true;

                int length = User32.GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                User32.GetWindowText(hWnd, builder, length + 1);

                WindowInfo wInfo = new WindowInfo() { Handle = hWnd, Title = builder.ToString() };
                wInfo.ThreadId = User32.GetWindowThreadProcessId(hWnd, out wInfo.ProcessId);
                wInfo.ProcessName = Process.GetProcessById((int)wInfo.ProcessId).ProcessName;
                windows.Add(wInfo);

                return true;
            }, 0);

            ApplyFilter("");
        }

        private void ApplyFilter(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? windows
                : windows.Where(w =>
                    w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    w.ProcessName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            WindowListBox.ItemsSource = filtered;
            btnOpen.IsEnabled = WindowListBox.SelectedItem != null;
        }

        private WindowInfo GetSelected()
        {
            // ItemsSource is always the filtered list, so SelectedItem is the only correct source:
            // never index into `windows` with the ListBox's index (that index refers to the filtered view).
            return WindowListBox.SelectedItem as WindowInfo;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(searchBox.Text);
        }

        private void WindowListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            WindowInfo selected = GetSelected();
            if (selected == null) return;
            SelectedWindow = selected;
            DialogResult = true;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            WindowInfo selected = GetSelected();
            if (selected == null) return;
            SelectedWindow = selected;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void WindowListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnOpen.IsEnabled = WindowListBox.SelectedItem != null;
        }
    }
}
```

- [ ] **Step 3: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`.

- [ ] **Step 4: MANUAL — picker check**

Run the app, click **Attach Game**. Expected: Fluent picker with search box, window list showing "Title (process)", typing filters the list, double-click or Attach closes it and the attach flow continues.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: Fluent window picker with search filter"
```

---

### Task 8: Overlay column alignment polish

**Files:**
- Modify: `SteamP2PInfo/OverlayWindow.xaml`

- [ ] **Step 1: Align the overlay columns**

In `OverlayWindow.xaml`, keep the structure but apply these changes:

1. On the peer columns' `DataGridTemplateColumn`s, give Ping and Connection Quality a fixed cell alignment: in the Ping template, add `TextAlignment="Right"` to the `OverlayTextBlock`; in the Connection Quality template, add `TextAlignment="Right"`.
2. Add `MinWidth` consistency: set the Ping column's `OverlayTextBlock` text to `{Binding Ping, StringFormat=N0}` (already the case) and give the connection-quality text `StringFormat=N2` (already the case).
3. Give the data grid `Margin="0,0,0,0"` and the peer grid `RowHeight="26"` for even spacing.

Exact replacements:

```xml
                <DataGridTemplateColumn Header="Ping">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <local:OverlayTextBlock Text="{Binding Ping, StringFormat=N0}" TextAlignment="Right" Fill="{Binding PingColor}" Style="{StaticResource defaultStyle}" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
                <DataGridTemplateColumn Header="Connection Quality" Visibility="{Binding Source={x:Static config:GameConfig.Current}, Path=OverlayConfig.ShowConnectionQuality, Converter={StaticResource VisibleIfTrueConverter}}">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <local:OverlayTextBlock Text="{Binding ConnectionQuality, StringFormat=N2}" TextAlignment="Right" Style="{StaticResource defaultStyle}" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
```

and on the `DataGrid` element add `RowHeight="26"`.

- [ ] **Step 2: Build**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`.

- [ ] **Step 3: MANUAL — overlay still works**

With a game running and attached, press the overlay hotkey: ping/quality columns should be right-aligned and evenly spaced; position/anchor behavior unchanged.

- [ ] **Step 4: Commit**

```bash
git add SteamP2PInfo/OverlayWindow.xaml
git commit -m "style: align overlay ping/quality columns"
```

---

### Task 9: Remove MahApps.Metro and ControlzEx

**Files:**
- Modify: `SteamP2PInfo/packages.config`, `SteamP2PInfo/SteamP2PInfo.csproj`
- Modify: `SteamP2PInfo/build.targets` (only if Step 4 requires it)

- [ ] **Step 1: Sweep for remaining usages**

```bash
cd "C:/Users/zeer/Desktop/SteamP2PInfo"
grep -rniE "mahapps|controlzex" --include="*.cs" --include="*.xaml" --include="*.config" --include="*.csproj" SteamP2PInfo/ | grep -v obj/
grep -rn "Microsoft.VisualBasic" --include="*.cs" --include="*.csproj" SteamP2PInfo/ | grep -v obj/
```

Expected: the first command matches only `packages.config` and `SteamP2PInfo.csproj`; the second matches only the `Microsoft.VisualBasic` reference line in `SteamP2PInfo.csproj` (its only consumer, `Interaction.InputBox`, was replaced in Task 4). If any `.cs`/`.xaml` hits remain, fix them first (they should have been handled in Tasks 3–7).

- [ ] **Step 2: Remove the packages and references**

1. `SteamP2PInfo/packages.config`: delete the `ControlzEx` and `MahApps.Metro` `<package>` lines.
2. `SteamP2PInfo/SteamP2PInfo.csproj`: delete the `ControlzEx`, `MahApps.Metro` and `Microsoft.VisualBasic` `<Reference>` blocks (including the MahApps/ControlzEx `<HintPath>`s).
3. Restore (prunes nothing but keeps packages dir consistent) and rebuild:

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -t:Restore -p:RestorePackagesConfig=true -v:m
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

Expected: `0 Error(s)`.

Two dependency-workflow facts recorded during Task 2 (both verified there by measurement): (a) this `packages.config` restore does **not** resolve transitive dependencies — every assembly the app loads must be listed explicitly in both `packages.config` and the csproj, or a fresh clone will not build; (b) if you ever choose to bump the pinned `System.Runtime.CompilerServices.Unsafe` instead of using the Task 5 step-12 binding redirect, the correct package version is **6.1.2** — its assembly version is 6.0.3.0, which is what `Wpf.Ui` and `System.Memory` reference (there is no published package "6.0.3"). Rebuild afterwards and confirm the 18 MSB3277 warnings drop to 0.

- [ ] **Step 3: Build Release and check the exe.config question**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Release -v:m
ls SteamP2PInfo/bin/Release/
```

The Release post-build in `build.targets` deletes `*.config`. For Debug, check whether a binding-redirect config was generated:

```bash
ls SteamP2PInfo/bin/Debug/*.config && grep -l "bindingRedirect" SteamP2PInfo/bin/Debug/SteamP2PInfo.exe.config
```

- [ ] **Step 4: Decide on the config file (only if Step 3 found binding redirects)**

If `SteamP2PInfo.exe.config` contains `<bindingRedirect>` entries, the Release post-build must stop deleting it — otherwise the shipped exe can fail to load `System.Memory`/`Unsafe` on machines where versions differ. In that case edit `SteamP2PInfo/build.targets`:

```xml
  <Target Name="PostBuildRelease" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
    <ItemGroup>
      <FilesToDelete Include="$(OutputPath)*.pdb; $(OutputPath)*.xml"/>
    </ItemGroup>
```
(removing `$(OutputPath)*.config` from the delete list). If there are no binding redirects, leave `build.targets` unchanged.

- [ ] **Step 5: MANUAL — run the Debug exe**

Run `SteamP2PInfo/bin/Debug/SteamP2PInfo.exe`. Expected: identical Fluent UI, no startup crash. If it crashes with a type-load/assembly error, re-check Step 2 (a MahApps reference may still be needed by a file missed by the sweep).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "build: remove MahApps.Metro and ControlzEx, keep WPF-UI only"
```

---

### Task 10: New default Steam log paths

**Files:**
- Modify: `SteamP2PInfo/Settings.settings`
- Modify: `SteamP2PInfo/Settings.Designer.cs`
- Modify: `SteamP2PInfo/app.config`

- [ ] **Step 1: Replace both default paths in all three files**

Replace the string `C:\Program Files (x86)\Steam\logs\ipc_SteamClient.log` with `D:\Steam\logs\ipc_SteamClient.log` and `C:\Program Files (x86)\Steam\logs\bootstrap_log.txt` with `D:\Steam\logs\bootstrap_log.txt` in:

1. `SteamP2PInfo/Settings.settings` (the `<Value Profile="(Default)">` entries for `SteamLogPath` and `SteamBootstrapLogPath`)
2. `SteamP2PInfo/Settings.Designer.cs` (the `[DefaultSettingValueAttribute("...")]` attributes — note the escaped `\\`)
3. `SteamP2PInfo/app.config` (the `<value>` entries)

```bash
cd "C:/Users/zeer/Desktop/SteamP2PInfo"
grep -rn "Steam\\\\logs" SteamP2PInfo/Settings.settings SteamP2PInfo/Settings.Designer.cs SteamP2PInfo/app.config
grep -rn "Program Files" SteamP2PInfo/Settings.settings SteamP2PInfo/Settings.Designer.cs SteamP2PInfo/app.config
```

Expected: the first command shows `D:\Steam\logs\...` in all three files; the second shows no hits.

- [ ] **Step 2: Build and verify the defaults land in the config**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

MANUAL check: the Config page's IPC log path field shows `D:\Steam\logs\ipc_SteamClient.log` for a fresh profile (if your user.config already has the old path saved, the field keeps the old value — that is the intended upgrade behavior).

- [ ] **Step 3: Commit**

```bash
git add SteamP2PInfo/Settings.settings SteamP2PInfo/Settings.Designer.cs SteamP2PInfo/app.config
git commit -m "feat: default Steam log paths to D:\\Steam\\logs"
```

---

### Task 11: App icon

**Files:**
- Create: `SteamP2PInfo/Resources/app.ico` (generated by script)
- Modify: `SteamP2PInfo/SteamP2PInfo.csproj`, `SteamP2PInfo/MainWindow.xaml`, `SteamP2PInfo/WindowSelectDialog.xaml`

- [ ] **Step 1: Generate the icon with a PowerShell script**

Save as `tools/make-icon.ps1` in the repo (create `tools/`), run it with PowerShell:

```powershell
Add-Type -AssemblyName System.Drawing
$sizes = 16,24,32,48,64,128,256
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap($s, $s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)

  $bg = [System.Drawing.Color]::FromArgb(255, 32, 32, 32)
  $accent = [System.Drawing.Color]::FromArgb(255, 76, 194, 255)
  $bgBrush = New-Object System.Drawing.SolidBrush($bg)
  $accentPen = New-Object System.Drawing.Pen($accent, [Math]::Max(1.5, $s / 10))
  $g.FillEllipse($bgBrush, 0, 0, $s - 1, $s - 1)

  # two connected dots (network motif)
  $r = [Math]::Max(1.5, $s / 10)
  $g.DrawLine($accentPen, $s * 0.32, $s * 0.68, $s * 0.68, $s * 0.32)
  $g.FillEllipse((New-Object System.Drawing.SolidBrush($accent)), $s * 0.22, $s * 0.58, $r * 2, $r * 2)
  $g.FillEllipse((New-Object System.Drawing.SolidBrush($accent)), $s * 0.68 - $r, $s * 0.22, $r * 2, $r * 2)

  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += , ($ms.ToArray())
  $g.Dispose(); $bmp.Dispose()
}

# assemble a PNG-compressed ICO
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $data = $pngs[$i]
  $dim = 0
  if ($s -lt 256) { $dim = $s }
  $w.Write([Byte]$dim)
  $w.Write([Byte]$dim)
  $w.Write([Byte]0); $w.Write([Byte]0)
  $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]$data.Length); $w.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach ($data in $pngs) { $w.Write($data) }
$w.Flush()
New-Item -ItemType Directory -Force -Path "SteamP2PInfo/Resources" | Out-Null
[System.IO.File]::WriteAllBytes("SteamP2PInfo/Resources/app.ico", $out.ToArray())
Write-Host "wrote SteamP2PInfo/Resources/app.ico"
```

Run: `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1`

- [ ] **Step 2: Wire the icon up**

1. In `SteamP2PInfo.csproj` add (near the other `ItemGroup`s):
```xml
  <ItemGroup>
    <Resource Include="Resources\app.ico" />
  </ItemGroup>
```
   and in the main `<PropertyGroup>` add `<ApplicationIcon>Resources\app.ico</ApplicationIcon>`.
2. In `MainWindow.xaml` and `WindowSelectDialog.xaml`, add `Icon="pack://application:,,,/Resources/app.ico"` to the `ui:FluentWindow` root attributes (this sets the taskbar and alt-tab icon; `ui:TitleBar` itself shows the title text, so leave its `Icon` property alone unless you also want a symbol glyph there).

- [ ] **Step 3: Build and check**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
```

MANUAL: `SteamP2PInfo/bin/Debug/SteamP2PInfo.exe` shows the new icon in Explorer and the taskbar; the window title bar shows it too.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: app icon (dark circle + accent network glyph)"
```

---

### Task 12: Update CLAUDE.md and run the final verification

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update `CLAUDE.md`**

Apply these edits:
1. Find the UI-stack mention (`grep -n "MahApps" CLAUDE.md`) and replace that Architecture sentence with:
```
UI framework is WPF-UI 4.3.0 (FluentWindow + TitleBar + NavigationView; the app runs dark-only with `ThemesDictionary Theme="Dark"`). The main window hosts two views in `Views/`: `SessionView` (peer cards bound to `ObservableCollection<SteamPeerBase>`, avatars via `SteamAvatar`, update banner) and `ConfigView` (STEAM card + generated settings cards). Config controls come from WPF-UI (`ToggleSwitch` — its state DP is `IsCheckedProperty` — and `NumberBox`) plus custom `Controls/HotKeyBox` (int encoding `vk | mods << 8`, same as the config JSON) and `Controls/ColorBox` (hex string with live tint; WPF-UI 4.3.0 ships no usable color picker). Dialogs go through `Dialogs.cs` (WPF-UI `MessageBox`).
```
2. In **Build**, after the "Known-benign warnings" paragraph, add: "`WPF-UI` restores from NuGet like the other packages; the Release post-build deletes `*.config` only if no binding redirects are needed (see `build.targets`)."
3. In **Gotchas**, remove the bullet mentioning MahApps dialogs if present; adjust the bullet about `ILRepack`/`Directory.builds.targets` unchanged.

- [ ] **Step 2: Full Debug + Release verification**

```bash
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Debug -v:m
"D:/App/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamP2PInfo.sln -p:Configuration=Release -v:m
ls SteamP2PInfo/bin/Release/
```

Expected: both builds `0 Error(s)`; Release contains `SteamP2PInfo.exe`, `steam_api64.dll`, TraceEvent DLLs, `amd64/`.

- [ ] **Step 3: MANUAL — full smoke test against a live game (spec §6 checklist)**

Run `SteamP2PInfo/bin/Release/SteamP2PInfo.exe` with a Steam game running, then check, one by one: attach flow incl. Steam console command dialog and clipboard copy; peers appear with name/ID/ping/quality (avatar or initial circle); F2 toggles the overlay; overlay follows the game window (move & focus); double-click opens profile; config edits persist to `config\{process}.json`; single-instance message; closing the game closes the app; credits line visible.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md for the WPF-UI stack"
```

---

### Task 13: Publish to the user's repo (gated)

**Do not run until the user confirms Step 3 of Task 12 passed.**

**Files:** none (git only)

- [ ] **Step 1: Push the backup branch of the old version**

```bash
cd "C:/Users/zeer/Desktop/SteamP2PInfo"
git push origin backup/react-version
```

Expected: new remote branch `backup/react-version` on github.com/ZEERDEER/SteamP2PInfo pointing at `aabfe6c` ("Fix overlay config and Steam console flow").

- [ ] **Step 2: Force-push the redesigned master**

```bash
git push --force origin master
git branch --set-upstream-to=origin/master master
git branch -vv | head -5
```

Expected: `master` now tracks `origin/master`; the user's repo shows the redesign commits on top of upstream master.

- [ ] **Step 3: Confirm the remote state**

```bash
git ls-remote --heads origin
```

Expected: `refs/heads/master` = current local HEAD, `refs/heads/backup/react-version` = `aabfe6c`.
