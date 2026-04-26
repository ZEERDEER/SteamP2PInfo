import { useCallback, useEffect, useState } from 'react';
import type { GameSession, WindowInfo, GameConfig, AppConfig } from '../types';

type MessageHandler = (data: unknown) => void;

const messageHandlers = new Map<string, MessageHandler>();
let messageId = 0;
let setGlobalSession: (session: GameSession | null) => void = () => {};
let setGlobalConfig: (config: GameConfig | null) => void = () => {};

// 初始化 WebView2 消息监听
if (typeof window !== 'undefined' && window.chrome?.webview) {
  window.chrome.webview.addEventListener('message', (e) => {
    try {
      const message = JSON.parse(e.data);
      if (message.type === 'sessionUpdated') {
        setGlobalSession(message.data as GameSession | null);
        if (message.data === null) setGlobalConfig(null);
        return;
      }
      
      const handler = messageHandlers.get(message.id);
      if (handler) {
        handler(message.data);
        messageHandlers.delete(message.id);
      }
    } catch (err) {
      console.error('Failed to parse message:', err);
    }
  });
}

function sendMessage<T>(type: string, payload?: unknown): Promise<T> {
  return new Promise((resolve) => {
    const id = `msg_${++messageId}`;
    
    messageHandlers.set(id, (data) => {
      resolve(data as T);
    });
    
    const message = JSON.stringify({ id, type, payload });
    
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage(message);
    } else {
      // 开发模式下的模拟数据
      setTimeout(() => {
        const handler = messageHandlers.get(id);
        if (handler) {
          handler(getMockData(type));
          messageHandlers.delete(id);
        }
      }, 100);
    }
  });
}

// 开发模式模拟数据
function getMockData(type: string): unknown {
  switch (type) {
    case 'getWindows':
      return [
        { handle: '1', title: 'ELDEN RING', processName: 'eldenring', processId: 1234, threadId: 1 },
        { handle: '2', title: 'Dark Souls III', processName: 'DarkSoulsIII', processId: 5678, threadId: 2 },
      ];
    case 'getSession':
      return {
        isAttached: true,
        gameName: 'ELDEN RING',
        processName: 'eldenring',
        steamAppId: 1245620,
        peers: [
          { steamId: '76561198012345678', name: 'SunBro_420', ping: 45, connectionQuality: 0.95, isOldAPI: false, connectionType: 'SteamNetworkingSockets', pingColor: '#7CFC00' },
          { steamId: '76561198087654321', name: 'DarkMoon_Knight', ping: 75, connectionQuality: 0.78, isOldAPI: false, connectionType: 'SteamNetworkingSockets', pingColor: '#00BFFF' },
          { steamId: '76561198011111111', name: 'Patches_The_Hyena', ping: 230, connectionQuality: 0.45, isOldAPI: true, connectionType: 'SteamNetworking', pingColor: '#CD5C5C' },
        ],
        lastUpdate: new Date().toISOString(),
      };
    case 'getConfig':
      return {
        processName: 'eldenring',
        steamAppId: 1245620,
        setPlayedWith: true,
        openProfileInOverlay: true,
        logActivity: false,
        hotkeysEnabled: true,
        playSoundOnNewSession: false,
        overlay: {
          enabled: true,
          showSteamId: false,
          showConnectionQuality: true,
          hotkey: 0,
          bannerFormat: '[{time:HH:mm:ss}] Steam P2P Info',
          font: 'Segoe UI, 20.25pt',
          xOffset: 0.025,
          yOffset: 0.025,
          anchor: 'TopRight',
          textColor: '#FFFFFFFF',
          strokeColor: '#FF000000',
          strokeWidth: 2.0,
        },
      };
    case 'getAppConfig':
      return {
        steamLogPath: 'C:\\Program Files (x86)\\Steam\\logs\\ipc_SteamClient.log',
        steamBootstrapLogPath: 'C:\\Program Files (x86)\\Steam\\logs\\bootstrap_log.txt',
        theme: 'dark',
        accentColor: '#FF6B35',
      };
    default:
      return null;
  }
}

export function useBridge() {
  const [session, setSession] = useState<GameSession | null>(null);
  const [config, setConfig] = useState<GameConfig | null>(null);
  const [appConfig, setAppConfig] = useState<AppConfig | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    setGlobalSession = setSession;
    setGlobalConfig = setConfig;
    return () => {
      setGlobalSession = () => {};
      setGlobalConfig = () => {};
    };
  }, []);

  const getWindows = useCallback(async (): Promise<WindowInfo[]> => {
    return sendMessage<WindowInfo[]>('getWindows');
  }, []);

  const attachGame = useCallback(async (handle: string, steamAppId?: number, debugMode?: boolean): Promise<boolean> => {
    setIsLoading(true);
    try {
      const result = await sendMessage<boolean>('attachGame', { handle, steamAppId, debugMode });
      if (result) {
        await refreshSession();
        await refreshConfig();
      }
      return result;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const detachGame = useCallback(async (): Promise<void> => {
    await sendMessage<void>('detachGame');
    setSession(null);
    setConfig(null);
  }, []);

  const refreshSession = useCallback(async (): Promise<void> => {
    const data = await sendMessage<GameSession>('getSession');
    setSession(data);
  }, []);

  const refreshConfig = useCallback(async (): Promise<void> => {
    const data = await sendMessage<GameConfig>('getConfig');
    setConfig(data);
  }, []);

  const updateConfig = useCallback(async (newConfig: Partial<GameConfig>): Promise<void> => {
    await sendMessage<void>('updateConfig', newConfig);
    await refreshConfig();
  }, [refreshConfig]);

  const getAppConfig = useCallback(async (): Promise<AppConfig> => {
    const data = await sendMessage<AppConfig>('getAppConfig');
    setAppConfig(data);
    return data;
  }, []);

  const updateAppConfig = useCallback(async (newConfig: Partial<AppConfig>): Promise<void> => {
    await sendMessage<void>('updateAppConfig', newConfig);
    await getAppConfig();
  }, [getAppConfig]);

  const openProfile = useCallback(async (steamId: string): Promise<void> => {
    await sendMessage<void>('openProfile', { steamId });
  }, []);

  const openSteamConsole = useCallback(async (): Promise<void> => {
    await sendMessage<void>('openSteamConsole');
  }, []);

  const copyToClipboard = useCallback(async (text: string): Promise<void> => {
    await sendMessage<void>('copyToClipboard', { text });
  }, []);

  const browseFile = useCallback(async (options?: { filter?: string; title?: string; initialDir?: string }): Promise<string | null> => {
    return sendMessage<string | null>('browseFile', options);
  }, []);

  const showFontDialog = useCallback(async (currentFont?: string, currentSize?: number): Promise<{ fontFamily: string; fontSize: number; fontString: string } | null> => {
    return sendMessage('showFontDialog', { currentFont, currentSize });
  }, []);

  const getSystemFonts = useCallback(async (): Promise<string[]> => {
    return sendMessage<string[]>('getSystemFonts');
  }, []);

  // 初始化加载 AppConfig
  useEffect(() => {
    getAppConfig();
  }, [getAppConfig]);

  // 自动刷新
  useEffect(() => {
    if (!session?.isAttached) return;

    const interval = setInterval(() => {
      refreshSession();
    }, 1000);

    return () => clearInterval(interval);
  }, [session?.isAttached, refreshSession]);

  return {
    session,
    config,
    appConfig,
    isLoading,
    getWindows,
    attachGame,
    detachGame,
    refreshSession,
    updateConfig,
    getAppConfig,
    updateAppConfig,
    openProfile,
    openSteamConsole,
    copyToClipboard,
    browseFile,
    showFontDialog,
    getSystemFonts,
  };
}
