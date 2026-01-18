using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using SteamP2PInfo.Core.Config;
using SteamP2PInfo.Core.Models;
using SteamP2PInfo.Core.Services;
using Steamworks;

// Alias WPF types to avoid conflict with System.Windows.Forms
using WpfMessageBox = System.Windows.MessageBox;
using WpfClipboard = System.Windows.Clipboard;

namespace SteamP2PInfo.App;

public partial class MainWindow : Window
{
    private const string STEAM_IPC_COMMAND = "log_ipc \"BeginAuthSession,EndAuthSession,LeaveLobby,SendClanChatMessage\"";
    
    // DWM API for Windows 11 rounded corners
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    
    private WindowInfo? _attachedWindow;
    private System.Threading.Timer? _updateTimer;
    private OverlayWindow? _overlayWindow;
    
    public MainWindow()
    {
        InitializeComponent();
        AppPaths.EnsureDirectories();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;
    }
    
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 rounded corners
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 or older - rounded corners not supported, window will have square corners
        }
    }
    
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebView();
    }
    
    private async Task InitializeWebView()
    {
        try
        {
            var userDataFolder = Path.Combine(AppPaths.CacheDir, "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);
            webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            
            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            var indexPath = Path.Combine(wwwrootPath, "index.html");
            
            if (Directory.Exists(wwwrootPath) && File.Exists(indexPath))
            {
                // 使用虚拟主机映射来加载本地资源，避免 file:// 协议的安全限制
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local", 
                    wwwrootPath, 
                    CoreWebView2HostResourceAccessKind.Allow);
                webView.CoreWebView2.Navigate("https://app.local/index.html");
            }
            else
            {
                // 开发模式
                webView.CoreWebView2.Navigate("http://localhost:5173");
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"WebView2 初始化失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // 使用 TryGetWebMessageAsString 获取原始字符串，避免双重 JSON 编码
            var jsonString = e.TryGetWebMessageAsString();
            System.Diagnostics.Debug.WriteLine($"[Bridge] Received: {jsonString}");
            
            var message = JsonSerializer.Deserialize<BridgeMessage>(jsonString);
            if (message == null) return;
            
            System.Diagnostics.Debug.WriteLine($"[Bridge] Type: {message.Type}, Id: {message.Id}");
            
            object? result = message.Type switch
            {
                "getWindows" => GetWindows(),
                "attachGame" => await AttachGame(message.Payload),
                "detachGame" => DetachGame(),
                "getSession" => GetSession(),
                "getConfig" => GetConfig(),
                "updateConfig" => UpdateConfig(message.Payload),
                "getAppConfig" => GetAppConfig(),
                "updateAppConfig" => UpdateAppConfig(message.Payload),
                "openProfile" => OpenProfile(message.Payload),
                "openSteamConsole" => OpenSteamConsole(),
                "copyToClipboard" => CopyToClipboard(message.Payload),
                "browseFile" => BrowseFile(message.Payload),
                "showFontDialog" => ShowFontDialog(message.Payload),
                "getSystemFonts" => GetSystemFonts(),
                _ => null
            };
            
            System.Diagnostics.Debug.WriteLine($"[Bridge] Sending response for {message.Id}");
            SendResponse(message.Id, result);
        }
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"[Bridge] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[Bridge] Stack: {ex.StackTrace}");
        }
    }
    
    private void SendResponse(string id, object? data)
    {
        var response = JsonSerializer.Serialize(new { id, data });
        webView.CoreWebView2.PostWebMessageAsString(response);
    }
    
    private List<object> GetWindows()
    {
        try
        {
            var windows = WindowService.GetVisibleWindows();
            System.Diagnostics.Debug.WriteLine($"[GetWindows] Found {windows.Count} windows");
            foreach (var w in windows)
            {
                System.Diagnostics.Debug.WriteLine($"  - {w.ProcessName}: {w.Title}");
            }
            return windows.Select(w => (object)new { handle = w.Handle.ToString(), title = w.Title, processName = w.ProcessName, processId = w.ProcessId, threadId = w.ThreadId }).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetWindows] Error: {ex.Message}");
            return new List<object>();
        }
    }
    
    private async Task<bool> AttachGame(JsonElement? payload)
    {
        if (payload == null) return false;
        var handleStr = payload.Value.GetProperty("handle").GetString();
        if (handleStr == null) return false;
        var handle = new IntPtr(long.Parse(handleStr));
        var steamAppId = payload.Value.TryGetProperty("steamAppId", out var appIdEl) ? appIdEl.GetInt32() : 0;
        
        // 检查是否启用调试模式（用于测试 overlay，无需真实联机）
        var debugMode = payload.Value.TryGetProperty("debugMode", out var dbgEl) && dbgEl.GetBoolean();
        SteamPeerManager.DebugMode = debugMode;
        
        var windows = WindowService.GetVisibleWindows();
        var window = windows.FirstOrDefault(w => w.Handle == handle);
        if (window == null) return false;
        var config = GameConfig.LoadOrCreate(window.ProcessName);
        if (config.SteamAppId == 0 && steamAppId > 0) { config.SteamAppId = steamAppId; config.Save(); }
        
        // 调试模式下跳过 Steam 初始化
        if (debugMode)
        {
            _attachedWindow = window;
            _updateTimer = new System.Threading.Timer(_ => Dispatcher.Invoke(UpdatePeers), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            CreateOverlayWindow();
            return true;
        }
        
        if (config.SteamAppId == 0) return false;
        Environment.SetEnvironmentVariable("SteamAppId", config.SteamAppId.ToString());
        if (!SteamAPI.Init()) return false;
        if (!SteamPeerManager.Initialize()) return false;
        ETWPingMonitor.Start();
        _attachedWindow = window;
        _updateTimer = new System.Threading.Timer(_ => Dispatcher.Invoke(UpdatePeers), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        

        
        // 创建并显示 Overlay 窗口
        CreateOverlayWindow();
        
        return true;
    }
    
    private bool DetachGame()
    {
        // 关闭 Overlay 窗口
        CloseOverlayWindow();
        
        _updateTimer?.Dispose();
        _updateTimer = null;
        SteamPeerManager.Shutdown();
        ETWPingMonitor.Stop();
        try { SteamAPI.Shutdown(); } catch { }
        _attachedWindow = null;
        return true;
    }
    
    private void CreateOverlayWindow()
    {
        if (_attachedWindow == null) return;
        
        CloseOverlayWindow();
        
        _overlayWindow = new OverlayWindow(
            _attachedWindow.Handle,
            (uint)_attachedWindow.ProcessId,
            (uint)_attachedWindow.ThreadId);
        
        _overlayWindow.Show();
    }
    
    private void CloseOverlayWindow()
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }
    }
    
    private object? GetSession()
    {
        if (_attachedWindow == null) return null;
        var peers = SteamPeerManager.GetPeers();
        return new
        {
            isAttached = true,
            gameName = _attachedWindow.Title,
            processName = _attachedWindow.ProcessName,
            steamAppId = GameConfig.Current?.SteamAppId ?? 0,
            peers = peers.Select(p => new { steamId = p.SteamId.ToString(), name = p.Name, ping = p.Ping, connectionQuality = p.ConnectionQuality, isOldAPI = p.IsOldAPI, connectionType = p.ConnectionType, pingColor = p.PingColor }).ToList(),
            lastUpdate = DateTime.UtcNow.ToString("O")
        };
    }
    
    private object? GetConfig()
    {
        var config = GameConfig.Current;
        if (config == null) return null;
        return new
        {
            processName = config.ProcessName,
            steamAppId = config.SteamAppId,
            setPlayedWith = config.SetPlayedWith,
            openProfileInOverlay = config.OpenProfileInOverlay,
            logActivity = config.LogActivity,
            hotkeysEnabled = config.HotkeysEnabled,
            playSoundOnNewSession = config.PlaySoundOnNewSession,
            overlay = new
            {
                enabled = config.Overlay.Enabled,
                showSteamId = config.Overlay.ShowSteamId,
                showConnectionQuality = config.Overlay.ShowConnectionQuality,
                hotkey = config.Overlay.Hotkey,
                bannerFormat = config.Overlay.BannerFormat,
                font = config.Overlay.Font,
                xOffset = config.Overlay.XOffset,
                yOffset = config.Overlay.YOffset,
                anchor = config.Overlay.Anchor,
                textColor = config.Overlay.TextColor,
                strokeColor = config.Overlay.StrokeColor,
                strokeWidth = config.Overlay.StrokeWidth
            }
        };
    }
    
    private bool UpdateConfig(JsonElement? payload)
    {
        if (payload == null || GameConfig.Current == null) return false;
        var config = GameConfig.Current;
        if (payload.Value.TryGetProperty("setPlayedWith", out var spw)) config.SetPlayedWith = spw.GetBoolean();
        if (payload.Value.TryGetProperty("openProfileInOverlay", out var opio)) config.OpenProfileInOverlay = opio.GetBoolean();
        if (payload.Value.TryGetProperty("logActivity", out var la)) config.LogActivity = la.GetBoolean();
        if (payload.Value.TryGetProperty("hotkeysEnabled", out var he)) config.HotkeysEnabled = he.GetBoolean();
        if (payload.Value.TryGetProperty("playSoundOnNewSession", out var psons)) config.PlaySoundOnNewSession = psons.GetBoolean();
        if (payload.Value.TryGetProperty("overlay", out var overlay))
        {
            if (overlay.TryGetProperty("enabled", out var oe)) config.Overlay.Enabled = oe.GetBoolean();
            if (overlay.TryGetProperty("showSteamId", out var ssi)) config.Overlay.ShowSteamId = ssi.GetBoolean();
            if (overlay.TryGetProperty("showConnectionQuality", out var scq)) config.Overlay.ShowConnectionQuality = scq.GetBoolean();
            if (overlay.TryGetProperty("bannerFormat", out var bf)) config.Overlay.BannerFormat = bf.GetString() ?? "";
            if (overlay.TryGetProperty("font", out var font)) config.Overlay.Font = font.GetString() ?? "";
            if (overlay.TryGetProperty("anchor", out var anchor)) config.Overlay.Anchor = anchor.GetString() ?? config.Overlay.Anchor;
            if (overlay.TryGetProperty("xOffset", out var xo)) config.Overlay.XOffset = xo.GetDouble();
            if (overlay.TryGetProperty("yOffset", out var yo)) config.Overlay.YOffset = yo.GetDouble();
            if (overlay.TryGetProperty("textColor", out var tc)) config.Overlay.TextColor = tc.GetString() ?? config.Overlay.TextColor;
            if (overlay.TryGetProperty("strokeColor", out var sc)) config.Overlay.StrokeColor = sc.GetString() ?? config.Overlay.StrokeColor;
            if (overlay.TryGetProperty("strokeWidth", out var sw)) config.Overlay.StrokeWidth = sw.GetDouble();
        }
        config.Save();
        return true;
    }
    
    private object GetAppConfig()
    {
        var config = AppConfig.Load();
        return new
        {
            steamLogPath = config.SteamLogPath,
            steamBootstrapLogPath = config.SteamBootstrapLogPath,
            theme = config.Theme,
            accentColor = config.AccentColor
        };
    }
    
    private bool UpdateAppConfig(JsonElement? payload)
    {
        if (payload == null) return false;
        var config = AppConfig.Load();
        if (payload.Value.TryGetProperty("steamLogPath", out var slp))
            config.SteamLogPath = slp.GetString() ?? config.SteamLogPath;
        if (payload.Value.TryGetProperty("steamBootstrapLogPath", out var sblp))
            config.SteamBootstrapLogPath = sblp.GetString() ?? config.SteamBootstrapLogPath;
        if (payload.Value.TryGetProperty("theme", out var theme))
            config.Theme = theme.GetString() ?? config.Theme;
        if (payload.Value.TryGetProperty("accentColor", out var accent))
            config.AccentColor = accent.GetString() ?? config.AccentColor;
        config.Save();
        return true;
    }
    
    private bool OpenProfile(JsonElement? payload)
    {
        if (payload == null) return false;
        var steamIdStr = payload.Value.GetProperty("steamId").GetString();
        if (string.IsNullOrEmpty(steamIdStr)) return false;
        var steamId = new CSteamID(ulong.Parse(steamIdStr));
        if (GameConfig.Current?.OpenProfileInOverlay == true)
        {
            SteamFriends.ActivateGameOverlayToUser("steamid", steamId);
        }
        else
        {
            var psi = new System.Diagnostics.ProcessStartInfo($"https://steamcommunity.com/profiles/{steamIdStr}")
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        return true;
    }
    
    private void UpdatePeers()
    {
        if (_attachedWindow == null) return;
        
        // 调试模式下跳过 Steam API 调用
        if (!SteamPeerManager.DebugMode)
        {
            SteamAPI.RunCallbacks();
            SteamPeerManager.UpdatePeerList();
        }
    }
    
    private bool OpenSteamConsole()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("steam://open/console")
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch { return false; }
    }
    
    private bool CopyToClipboard(JsonElement? payload)
    {
        if (payload == null) return false;
        try
        {
            var text = payload.Value.GetProperty("text").GetString();
            if (string.IsNullOrEmpty(text)) return false;
            WpfClipboard.SetText(text);
            return true;
        }
        catch { return false; }
    }
    
    private string? BrowseFile(JsonElement? payload)
    {
        try
        {
            var filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*";
            var title = "选择文件";
            var initialDir = @"C:\Program Files (x86)\Steam\logs";
            
            if (payload != null)
            {
                if (payload.Value.TryGetProperty("filter", out var f)) filter = f.GetString() ?? filter;
                if (payload.Value.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                if (payload.Value.TryGetProperty("initialDir", out var d)) initialDir = d.GetString() ?? initialDir;
            }
            
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title,
                InitialDirectory = Directory.Exists(initialDir) ? initialDir : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }
        catch { return null; }
    }
    
    private object? ShowFontDialog(JsonElement? payload)
    {
        try
        {
            var currentFont = "Segoe UI";
            var currentSize = 14.0;
            
            if (payload != null)
            {
                if (payload.Value.TryGetProperty("currentFont", out var cf))
                    currentFont = cf.GetString() ?? currentFont;
                if (payload.Value.TryGetProperty("currentSize", out var cs))
                    currentSize = cs.GetDouble();
            }
            
            // 使用 Windows Forms 的 FontDialog
            using var fontDialog = new System.Windows.Forms.FontDialog
            {
                Font = new System.Drawing.Font(currentFont, (float)currentSize),
                ShowColor = false,
                ShowEffects = false,
                AllowVerticalFonts = false
            };
            
            if (fontDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return new
                {
                    fontFamily = fontDialog.Font.FontFamily.Name,
                    fontSize = fontDialog.Font.Size,
                    fontString = $"{fontDialog.Font.FontFamily.Name}, {fontDialog.Font.Size}pt"
                };
            }
            return null;
        }
        catch { return null; }
    }
    
    private List<string> GetSystemFonts()
    {
        try
        {
            return System.Windows.Media.Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .OrderBy(f => f)
                .ToList();
        }
        catch { return new List<string>(); }
    }
    
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CloseOverlayWindow();
        DetachGame();
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class BridgeMessage
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [System.Text.Json.Serialization.JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}
