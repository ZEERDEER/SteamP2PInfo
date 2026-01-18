using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SteamP2PInfo.Core.Config;
using SteamP2PInfo.Core.Models;
using SteamP2PInfo.Core.Services;

// Alias WPF types to avoid conflict with System.Drawing from Windows Forms
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace SteamP2PInfo.App;

/// <summary>
/// Overlay 窗口 - 在游戏中显示玩家信息
/// </summary>
public partial class OverlayWindow : Window
{
    #region Win32 API
    
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOPMOST = 0x00000008;
    
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;
    private const uint VK_HOME = 0x24;  // Home 键
    
    #endregion
    
    private readonly IntPtr _targetHandle;
    private readonly uint _targetProcessId;
    private readonly uint _targetThreadId;
    
    private WindowInteropHelper? _interopHelper;
    private HwndSource? _hwndSource;
    private WinEventDelegate? _locationEventDelegate;
    private WinEventDelegate? _foregroundEventDelegate;
    private IntPtr _locationHook;
    private IntPtr _foregroundHook;
    
    private DispatcherTimer? _updateTimer;
    private bool _isDragging;
    private bool _isClosed;
    private int _tickCount = 0;  // 用于控制不同更新频率
    
    private bool _hotkeyRegistered;
    private bool _manuallyHidden;  // 用户通过热键手动隐藏
    
    public ObservableCollection<OverlayPeerViewModel> Peers { get; } = new();
    
    public OverlayWindow(IntPtr targetHandle, uint targetProcessId, uint targetThreadId)
    {
        _targetHandle = targetHandle;
        _targetProcessId = targetProcessId;
        _targetThreadId = targetThreadId;
        
        InitializeComponent();
        DataContext = this;
        peerList.ItemsSource = Peers;
    }
    
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _interopHelper = new WindowInteropHelper(this);
        
        // 设置窗口样式：透明 + 工具窗口（不在 Alt+Tab 显示）+ 点击穿透
        int exStyle = GetWindowLong(_interopHelper.Handle, GWL_EXSTYLE);
        SetWindowLong(_interopHelper.Handle, GWL_EXSTYLE, 
            exStyle | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        
        // 添加 HwndSource hook 用于接收 WM_HOTKEY 消息
        _hwndSource = HwndSource.FromHwnd(_interopHelper.Handle);
        _hwndSource?.AddHook(WndProc);
        
        // 注册热键
        RegisterOverlayHotkey();
        
        // 安装窗口事件钩子
        InstallHooks();
        
        // 启动更新计时器 - 50ms 间隔实现丝滑更新
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _updateTimer.Tick += OnUpdateTick;
        _updateTimer.Start();
        
        // 初始位置
        UpdatePosition();
        UpdateVisibility();
    }
    
    private void InstallHooks()
    {
        _locationEventDelegate = OnLocationChange;
        _foregroundEventDelegate = OnForegroundChange;
        
        _locationHook = SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _locationEventDelegate,
            _targetProcessId, _targetThreadId, WINEVENT_OUTOFCONTEXT);
        
        _foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundEventDelegate,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }
    
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _manuallyHidden = !_manuallyHidden;
            UpdateVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }
    
    private void RegisterOverlayHotkey()
    {
        if (_interopHelper == null) return;
        
        var config = GameConfig.Current;
        if (config == null || !config.HotkeysEnabled) return;
        
        // 如果已注册，不需要重新注册
        if (_hotkeyRegistered) return;
        
        // 注册 Home 键热键
        _hotkeyRegistered = RegisterHotKey(_interopHelper.Handle, HOTKEY_ID, 0, VK_HOME);
    }
    
    private void UnregisterOverlayHotkey()
    {
        if (_hotkeyRegistered && _interopHelper != null)
        {
            UnregisterHotKey(_interopHelper.Handle, HOTKEY_ID);
            _hotkeyRegistered = false;
        }
    }
    
    private void OnLocationChange(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == _targetHandle && idObject == 0)
            Dispatcher.BeginInvoke(UpdatePosition);
    }
    
    private void OnForegroundChange(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        Dispatcher.BeginInvoke(UpdateVisibility);
    }
    
    /// <summary>
    /// 获取指定窗口所在显示器的 DPI 缩放比例
    /// </summary>
    private static double GetDpiScaleForWindow(IntPtr hwnd)
    {
        try
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                int result = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint _);
                if (result == 0) // S_OK
                {
                    return dpiX / 96.0;
                }
            }
        }
        catch { }
        
        // 回退到系统默认 DPI
        return 1.0;
    }
    
    private void OnUpdateTick(object? sender, EventArgs e)
    {
        _tickCount++;
        
        // 每次 tick (50ms) 更新位置和样式 - 实现丝滑效果
        UpdatePosition();
        UpdateOverlayStyle();
        UpdateVisibility();
        
        // 检查热键配置是否变化
        var config = GameConfig.Current;
        if (config != null)
        {
            bool hotkeysEnabled = config.HotkeysEnabled;
            
            if (hotkeysEnabled && !_hotkeyRegistered)
            {
                RegisterOverlayHotkey();
            }
            else if (!hotkeysEnabled && _hotkeyRegistered)
            {
                UnregisterOverlayHotkey();
            }
        }
        
        // 每 20 次 tick (1秒) 更新 peers 和 header - 避免频繁 API 调用
        if (_tickCount >= 20)
        {
            _tickCount = 0;
            UpdateHeaderText();
            UpdatePeers();
        }
    }
    
    private void UpdateHeaderText()
    {
        var config = GameConfig.Current?.Overlay;
        if (config == null) return;
        
        var format = config.BannerFormat;
        var text = format.Replace("{time:HH:mm:ss}", DateTime.Now.ToString("HH:mm:ss"))
                         .Replace("{time}", DateTime.Now.ToString("HH:mm:ss"));
        headerText.Text = text;
    }
    
    private void UpdateOverlayStyle()
    {
        var config = GameConfig.Current?.Overlay;
        if (config == null) return;
        
        // 解析颜色
        try
        {
            if (!string.IsNullOrEmpty(config.TextColor))
            {
                var textColor = (WpfColor)WpfColorConverter.ConvertFromString(config.TextColor);
                headerText.Fill = new SolidColorBrush(textColor);
            }
            if (!string.IsNullOrEmpty(config.StrokeColor))
            {
                var strokeColor = (WpfColor)WpfColorConverter.ConvertFromString(config.StrokeColor);
                headerText.Stroke = new SolidColorBrush(strokeColor);
            }
            headerText.StrokeThickness = config.StrokeWidth;
        }
        catch { }
        
        // 解析字体
        try
        {
            if (!string.IsNullOrEmpty(config.Font))
            {
                var fontParts = config.Font.Split(',');
                var fontFamily = fontParts[0].Trim();
                var fontSize = 16.0;
                
                if (fontParts.Length > 1)
                {
                    var sizeStr = fontParts[1].Trim().Replace("pt", "").Replace("px", "");
                    if (double.TryParse(sizeStr, out var parsedSize) && parsedSize > 0)
                        fontSize = parsedSize;
                }
                
                if (!string.IsNullOrEmpty(fontFamily))
                {
                    headerText.FontFamily = new System.Windows.Media.FontFamily(fontFamily);
                    headerText.FontSize = fontSize;
                }
            }
        }
        catch { }
    }
    
    private void UpdatePeers()
    {
        var config = GameConfig.Current?.Overlay;
        if (config == null) return;
        
        var peers = SteamPeerManager.GetPeers();
        
        // 解析字体配置
        var fontFamily = "Segoe UI";
        var fontSize = 14.0;
        try
        {
            if (!string.IsNullOrEmpty(config.Font))
            {
                var fontParts = config.Font.Split(',');
                fontFamily = fontParts[0].Trim();
                if (fontParts.Length > 1)
                {
                    var sizeStr = fontParts[1].Trim().Replace("pt", "").Replace("px", "");
                    if (double.TryParse(sizeStr, out var parsedSize) && parsedSize > 0)
                        fontSize = parsedSize;
                }
            }
        }
        catch { }
        
        Peers.Clear();
        foreach (var peer in peers)
        {
            Peers.Add(new OverlayPeerViewModel
            {
                Name = peer.Name,
                SteamId = peer.SteamId,
                Ping = peer.Ping,
                ConnectionQuality = peer.ConnectionQuality,
                ShowSteamId = config.ShowSteamId,
                ShowQuality = config.ShowConnectionQuality,
                PingColor = peer.PingColor,
                // 传递字体配置
                FontFamily = fontFamily,
                FontSize = fontSize,
                TextColor = config.TextColor ?? "#FFFFFF",
                StrokeColor = config.StrokeColor ?? "#000000",
                StrokeWidth = config.StrokeWidth
            });
        }
    }
    
    public void UpdateVisibility()
    {
        if (_isClosed) return;
        
        var config = GameConfig.Current?.Overlay;
        
        // 检查目标窗口是否存在且未最小化
        bool targetValid = !IsIconic(_targetHandle);
        
        // 检查当前前台窗口是否是目标窗口
        var foreground = GetForegroundWindow();
        bool targetIsForeground = foreground == _targetHandle;
        
        // 只在目标窗口是前台窗口且启用 overlay 且用户没有手动隐藏时显示
        bool shouldShow = config?.Enabled == true && targetValid && targetIsForeground && !_manuallyHidden;
        
        if (shouldShow && !IsVisible)
        {
            Show();
            UpdatePosition();
        }
        else if (!shouldShow && IsVisible)
        {
            Hide();
        }
        
        // 当目标窗口获得焦点时置顶
        if (IsVisible && _interopHelper != null && targetIsForeground)
        {
            SetWindowPos(_interopHelper.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
    
    public void UpdatePosition()
    {
        if (_isDragging || _isClosed) return;
        
        var config = GameConfig.Current?.Overlay;
        if (config == null) return;
        
        if (!GetWindowRect(_targetHandle, out RECT rect)) return;
        
        // 获取目标窗口所在显示器的 DPI（而不是 overlay 窗口的 DPI）
        double dpiScale = GetDpiScaleForWindow(_targetHandle);
        
        double targetWidth = (rect.Right - rect.Left) / dpiScale;
        double targetHeight = (rect.Bottom - rect.Top) / dpiScale;
        double targetLeft = rect.Left / dpiScale;
        double targetTop = rect.Top / dpiScale;
        
        // 根据锚点计算位置
        double xOffset = config.XOffset;
        double yOffset = config.YOffset;
        
        switch (config.Anchor)
        {
            case "TopLeft":
                Left = targetLeft + xOffset * targetWidth;
                Top = targetTop + yOffset * targetHeight;
                break;
            case "TopRight":
                Left = targetLeft + (1 - xOffset) * targetWidth - ActualWidth;
                Top = targetTop + yOffset * targetHeight;
                break;
            case "BottomLeft":
                Left = targetLeft + xOffset * targetWidth;
                Top = targetTop + (1 - yOffset) * targetHeight - ActualHeight;
                break;
            case "BottomRight":
                Left = targetLeft + (1 - xOffset) * targetWidth - ActualWidth;
                Top = targetTop + (1 - yOffset) * targetHeight - ActualHeight;
                break;
        }
    }
    
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            DragMove();
        }
    }
    
    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        
        // 保存新位置到配置
        var config = GameConfig.Current?.Overlay;
        if (config == null) return;
        
        if (!GetWindowRect(_targetHandle, out RECT rect)) return;
        
        double dpiScale = GetDpiScaleForWindow(_targetHandle);
        double targetWidth = (rect.Right - rect.Left) / dpiScale;
        double targetHeight = (rect.Bottom - rect.Top) / dpiScale;
        double targetLeft = rect.Left / dpiScale;
        double targetTop = rect.Top / dpiScale;
        
        switch (config.Anchor)
        {
            case "TopLeft":
                config.XOffset = (Left - targetLeft) / targetWidth;
                config.YOffset = (Top - targetTop) / targetHeight;
                break;
            case "TopRight":
                config.XOffset = 1 - (Left + ActualWidth - targetLeft) / targetWidth;
                config.YOffset = (Top - targetTop) / targetHeight;
                break;
            case "BottomLeft":
                config.XOffset = (Left - targetLeft) / targetWidth;
                config.YOffset = 1 - (Top + ActualHeight - targetTop) / targetHeight;
                break;
            case "BottomRight":
                config.XOffset = 1 - (Left + ActualWidth - targetLeft) / targetWidth;
                config.YOffset = 1 - (Top + ActualHeight - targetTop) / targetHeight;
                break;
        }
        
        GameConfig.Current?.Save();
    }
    
    private void Window_Closed(object sender, EventArgs e)
    {
        _isClosed = true;
        _updateTimer?.Stop();
        
        // 注销热键
        UnregisterOverlayHotkey();
        
        // 移除窗口消息 hook
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        
        if (_locationHook != IntPtr.Zero)
            UnhookWinEvent(_locationHook);
        if (_foregroundHook != IntPtr.Zero)
            UnhookWinEvent(_foregroundHook);
    }
}

/// <summary>
/// Overlay 中显示的玩家信息 ViewModel
/// </summary>
public class OverlayPeerViewModel
{
    public string Name { get; set; } = "";
    public ulong SteamId { get; set; }
    public double Ping { get; set; }
    public double ConnectionQuality { get; set; }
    public bool ShowSteamId { get; set; }
    public bool ShowQuality { get; set; }
    public string PingColor { get; set; } = "#FFFFFF";
    
    // 字体配置（从 GameConfig 读取）
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public string TextColor { get; set; } = "#FFFFFF";
    public string StrokeColor { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 2;
    
    public string SteamIdDisplay => SteamId.ToString();
    public string PingDisplay => $"{Ping:N0}ms";
    public string QualityDisplay => $"{ConnectionQuality:P0}";
    
    public WpfBrush PingBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(PingColor));
            }
            catch
            {
                return WpfBrushes.White;
            }
        }
    }
    
    public System.Windows.Media.FontFamily FontFamilyObj => new(FontFamily);
    
    public WpfBrush TextBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(TextColor));
            }
            catch
            {
                return WpfBrushes.White;
            }
        }
    }
    
    public WpfBrush StrokeBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(StrokeColor));
            }
            catch
            {
                return WpfBrushes.Black;
            }
        }
    }
}
