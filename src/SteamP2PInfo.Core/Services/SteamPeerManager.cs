using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Steamworks;
using SteamP2PInfo.Core.Models;
using SteamP2PInfo.Core.Config;

namespace SteamP2PInfo.Core.Services;

/// <summary>
/// Steam P2P 玩家管理器（完全复制原版逻辑）
/// </summary>
public static class SteamPeerManager
{
    private static FileStream? fs;
    private static StreamReader? sr;
    private static FileSystemWatcher? fsWatcher;
    private static bool mustReopenLog = true;
    private static long? lastPosInLog = null;
    private static readonly Stopwatch sw = new();

    private static readonly Regex STEAMID3_REGEX = new(@"\[U:1:(?<id>\d+)\]", RegexOptions.Compiled);
    private const long STEAMID64_BASE = 0x0110_0001_0000_0000;
    private const long PEER_TIMEOUT_MS = 5000;

    // 调试模式：设为 true 可在不联机情况下测试 overlay
    public static bool DebugMode { get; set; } = false;

    // 调试日志
    private static readonly string DebugLogPath = Path.Combine(
        AppPaths.AppRootDir, "debug.log");

    private static void Log(string message)
    {
        try
        {
            var logDir = Path.GetDirectoryName(DebugLogPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    // 与原版一致：使用反射获取所有 SteamPeerBase 的子类工厂
    private static readonly Func<CSteamID, SteamPeerBase>[] PEER_FACTORIES =
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SteamPeerBase)))
            .Select(t => new Func<CSteamID, SteamPeerBase>((CSteamID sid) => 
                (Activator.CreateInstance(t, sid) as SteamPeerBase)!))
            .ToArray();

    /// <summary>
    /// 玩家字典
    /// </summary>
    private static readonly Dictionary<CSteamID, SteamPeerInfo> mPeers = new();

    private static bool isInitialized = false;
    private static string? steamLogPath;

    public static bool IsInitialized => isInitialized;

    /// <summary>
    /// 初始化（与原版 Init 方法一致）
    /// </summary>
    public static bool Initialize()
    {
        if (isInitialized) return true;
        
        try
        {
            var config = AppConfig.Load();
            steamLogPath = config.SteamLogPath;
            Log($"[Initialize] Steam log path: {steamLogPath}");

            if (string.IsNullOrEmpty(steamLogPath))
            {
                Log("[Initialize] Steam log path is empty!");
                return false;
            }

            var logDir = Path.GetDirectoryName(steamLogPath);
            var logFileName = Path.GetFileName(steamLogPath);

            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
            {
                Log($"[Initialize] Log directory does not exist: {logDir}");
                return false;
            }

            // 与原版一致：设置 FileSystemWatcher
            fsWatcher = new FileSystemWatcher(logDir);
            fsWatcher.Filter = logFileName;
            fsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            fsWatcher.Changed += (e, s) => mustReopenLog = true;
            fsWatcher.EnableRaisingEvents = true;

            sw.Start();
            isInitialized = true;
            Log("[Initialize] Initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log($"[Initialize] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 关闭
    /// </summary>
    public static void Shutdown()
    {
        foreach (var pInfo in mPeers.Values)
        {
            pInfo.peer?.Dispose();
        }
        mPeers.Clear();

        fsWatcher?.Dispose();
        fsWatcher = null;
        sr?.Dispose();
        fs?.Close();
        fs?.Dispose();
        sr = null;
        fs = null;
        isInitialized = false;
        mustReopenLog = true;
        lastPosInLog = null;
        steamLogPath = null;
        sw.Stop();
    }

    /// <summary>
    /// 从日志行提取 Steam ID（与原版一致）
    /// </summary>
    private static CSteamID ExtractUser(string str)
    {
        Match m = STEAMID3_REGEX.Match(str);
        if (m.Success)
        {
            return new CSteamID(ulong.Parse(m.Groups["id"].Value) + STEAMID64_BASE);
        }
        return new CSteamID(0);
    }

    /// <summary>
    /// 获取 P2P 连接的玩家（与原版一致：遍历工厂尝试连接）
    /// </summary>
    private static SteamPeerBase? GetPeer(CSteamID player)
    {
        SteamPeerBase? peer = null;
        foreach (var factory in PEER_FACTORIES)
        {
            try
            {
                peer = factory(player);
                if (peer.UpdatePeerInfo())
                {
                    Log($"[PEER CONNECT] \"{peer.Name}\" (https://steamcommunity.com/profiles/{(ulong)peer.SteamID}) has connected via {peer.ConnectionTypeName}");
                    Logger.LogPeerConnected((ulong)player, peer.Name);
                    
                    if (GameConfig.Current?.SetPlayedWith == true)
                        SteamFriends.SetPlayedWith(player);

                    return peer;
                }
            }
            catch (Exception ex)
            {
                Log($"[GetPeer] Factory failed: {ex.Message}");
                peer?.Dispose();
            }
        }
        return null;
    }

    /// <summary>
    /// 更新玩家列表（完全复制原版逻辑）
    /// </summary>
    public static void UpdatePeerList()
    {
        if (!isInitialized || string.IsNullOrEmpty(steamLogPath)) return;

        try
        {
            // 与原版一致：强制 Steam 刷新日志
            try { SteamFriends.SendClanChatMessage(new CSteamID(0), ""); } catch { }

            if (mustReopenLog)
            {
                sr?.Dispose();
                fs?.Close();
                fs?.Dispose();

                try
                {
                    fs = new FileStream(steamLogPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
                    sr = new StreamReader(fs);
                    
                    // 改进：首次打开扫描最后 1MB，检测已连接的玩家；后续从上次位置继续
                    if (lastPosInLog is null)
                    {
                        // 只扫描最后 1MB，避免大文件（100MB+）导致启动卡顿
                        const long MAX_INITIAL_SCAN_BYTES = 1024 * 1024; // 1MB
                        long startPos = Math.Max(0, fs.Length - MAX_INITIAL_SCAN_BYTES);
                        fs.Seek(startPos, SeekOrigin.Begin);
                        
                        // 如果从中间开始，跳过可能不完整的第一行
                        if (startPos > 0)
                        {
                            sr.ReadLine(); // 丢弃可能被截断的行
                            Log($"[UpdatePeerList] First open, scanning last {MAX_INITIAL_SCAN_BYTES / 1024}KB (skipped partial line)");
                        }
                        else
                        {
                            Log("[UpdatePeerList] First open, file small enough, scanning from beginning");
                        }
                    }
                    else
                        fs.Seek((long)lastPosInLog, SeekOrigin.Begin);
                    
                    mustReopenLog = false;
                    Log($"[UpdatePeerList] Opened log file, pos={fs.Position}");
                }
                catch (Exception ex)
                {
                    Log($"[UpdatePeerList] Error opening log: {ex.Message}");
                    return;
                }
            }

            if (sr == null || fs == null) return;

            // 记录断开日志的委托
            Action<SteamPeerBase?, CSteamID, string> logDisconnect = (p, sid, reason) =>
            {
                string name = p?.Name ?? sid.m_SteamID.ToString();
                Log($"[PEER DISCONNECT] \"{name}\" (https://steamcommunity.com/profiles/{(ulong)sid}): {reason}");
                Logger.LogPeerDisconnected((ulong)sid, name);
            };

            // 与原版一致：读取日志行
            while (!mustReopenLog)
            {
                string? line = sr.ReadLine();
                if (line == null)
                {
                    lastPosInLog = fs.Position;
                    break;
                }

                // 只处理当前游戏进程的日志
                var processName = GameConfig.Current?.ProcessName;
                if (!string.IsNullOrEmpty(processName) && !line.Contains(processName))
                    continue;

                bool begin;
                if (line.Contains("BeginAuthSession"))
                {
                    begin = true;
                }
                else if (line.Contains("EndAuthSession"))
                {
                    begin = false;
                }
                else if (line.Contains("LeaveLobby"))
                {
                    foreach (var sid in mPeers.Keys)
                    {
                        logDisconnect(mPeers[sid].peer, sid, "Player left Steam lobby");
                    }
                    mPeers.Clear();
                    continue;
                }
                else continue;

                CSteamID steamID = ExtractUser(line);

                if (steamID.m_SteamID != 0)
                {
                    if (steamID.BIndividualAccount())
                    {
                        if (begin)
                        {
                            if (!mPeers.TryGetValue(steamID, out SteamPeerInfo? peerInfo))
                            {
                                var newPeerInfo = new SteamPeerInfo(GetPeer(steamID));
                                if (newPeerInfo.peer is null)
                                {
                                    Log($"[PEER CONNECT] Player \"{steamID}\" was detected, but we don't have a P2P connection to them yet");
                                    newPeerInfo.lastDisconnectTimeMS = sw.ElapsedMilliseconds;
                                }
                                mPeers.Add(steamID, newPeerInfo);
                            }
                        }
                        else
                        {
                            // 玩家断开
                            if (mPeers.TryGetValue(steamID, out SteamPeerInfo? pInfo))
                            {
                                mPeers.Remove(steamID);
                                logDisconnect(pInfo.peer, steamID, "Auth session with peer ended");
                            }
                        }
                    }
                    else
                    {
                        Log($"[PARSE ERROR] \"{steamID}\" was not a valid steam user");
                    }
                }
            }

            // 与原版一致：清理超时的玩家
            foreach (var sid in mPeers.Keys.ToArray())
            {
                var pInfo = mPeers[sid];
                bool isP2PConnected = false;
                
                if (pInfo.peer is null)
                    isP2PConnected = (pInfo.peer = GetPeer(sid)) != null;
                else
                    isP2PConnected = pInfo.peer.UpdatePeerInfo();

                if (pInfo.isConnected && !isP2PConnected)
                    pInfo.lastDisconnectTimeMS = sw.ElapsedMilliseconds;
                pInfo.isConnected = isP2PConnected;

                if (!isP2PConnected && sw.ElapsedMilliseconds - pInfo.lastDisconnectTimeMS > PEER_TIMEOUT_MS)
                {
                    mPeers.Remove(sid);
                    logDisconnect(pInfo.peer, sid, pInfo.peer is null ? "P2P connection was not established" : "Peer disconnected from P2P session");
                    pInfo.peer?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[UpdatePeerList] Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有已连接的玩家（返回 UI 使用的 SteamPeer 模型）
    /// </summary>
    public static List<SteamPeer> GetPeers()
    {
        // 调试模式
        if (DebugMode)
        {
            return GetDebugPeers();
        }

        var result = new List<SteamPeer>();
        
        // 与原版一致：只返回有 peer 的玩家
        foreach (var pInfo in mPeers.Values.Where(info => info.peer != null))
        {
            var peer = pInfo.peer!;
            result.Add(new SteamPeer
            {
                SteamId64 = (ulong)peer.SteamID,
                Name = peer.Name,
                Ping = peer.Ping,
                ConnectionQuality = peer.ConnectionQuality,
                ConnectionType = peer.ConnectionTypeName,
                PingColor = peer.PingColor
            });
        }
        
        return result;
    }

    /// <summary>
    /// 获取原始的 SteamPeerBase 列表（与原版接口一致）
    /// </summary>
    public static IEnumerable<SteamPeerBase> GetPeerBases()
    {
        return mPeers.Values.Where(info => info.peer != null).Select(info => info.peer!);
    }

    /// <summary>
    /// 调试用假数据
    /// </summary>
    private static List<SteamPeer> GetDebugPeers()
    {
        return new List<SteamPeer>
        {
            new() { SteamId64 = 76561198012345678, Name = "TestPlayer1", Ping = 45, ConnectionQuality = 0.95, ConnectionType = "SteamNetworkingSockets", PingColor = "#7CFC00" },
            new() { SteamId64 = 76561198087654321, Name = "TestPlayer2", Ping = 75, ConnectionQuality = 0.80, ConnectionType = "SteamNetworking", PingColor = "#00BFFF" },
            new() { SteamId64 = 76561198011111111, Name = "TestPlayer3", Ping = 250, ConnectionQuality = 0.60, ConnectionType = "SteamNetworkingSockets", PingColor = "#CD5C5C" }
        };
    }
}
