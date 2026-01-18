import { useState, useEffect } from 'react';
import { Gamepad2, Search, X, Loader2 } from 'lucide-react';
import type { WindowInfo } from '../types';

interface WindowSelectorProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (window: WindowInfo, steamAppId?: number, debugMode?: boolean) => void;
  getWindows: () => Promise<WindowInfo[]>;
}

export function WindowSelector({ isOpen, onClose, onSelect, getWindows }: WindowSelectorProps) {
  const [windows, setWindows] = useState<WindowInfo[]>([]);
  const [selected, setSelected] = useState<WindowInfo | null>(null);
  const [steamAppId, setSteamAppId] = useState('');
  const [debugMode, setDebugMode] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (isOpen) {
      loadWindows();
    }
  }, [isOpen]);

  const loadWindows = async () => {
    setIsLoading(true);
    try {
      const list = await getWindows();
      setWindows(list);
    } finally {
      setIsLoading(false);
    }
  };

  const filteredWindows = windows.filter(w =>
    w.title.toLowerCase().includes(search.toLowerCase()) ||
    w.processName.toLowerCase().includes(search.toLowerCase())
  );

  const handleConfirm = () => {
    if (selected) {
      onSelect(selected, steamAppId ? parseInt(steamAppId) : undefined, debugMode);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 animate-fade-in">
      <div className="glass rounded-2xl w-full max-w-md mx-4 overflow-hidden shadow-2xl">
        {/* Header */}
        <div className="px-4 py-3 border-b border-white/10 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-accent/20 flex items-center justify-center">
              <Gamepad2 className="text-accent" size={16} />
            </div>
            <div>
              <h2 className="text-sm font-semibold text-white">选择游戏窗口</h2>
              <p className="text-xs text-gray-400">选择要监控的游戏</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-white/10 text-gray-400 hover:text-white transition-colors"
          >
            <X size={16} />
          </button>
        </div>

        {/* Search */}
        <div className="px-4 py-2 border-b border-white/5">
          <div className="relative">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
            <input
              type="text"
              placeholder="搜索窗口..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-8 pr-3 py-2 bg-white/5 border border-white/10 rounded-lg text-sm text-white placeholder-gray-500 focus:outline-none focus:border-accent/50 transition-colors"
            />
          </div>
        </div>

        {/* Window List */}
        <div className="max-h-48 overflow-y-auto">
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="animate-spin text-accent" size={24} />
            </div>
          ) : filteredWindows.length === 0 ? (
            <div className="text-center py-8 text-gray-500 text-sm">
              没有找到窗口
            </div>
          ) : (
            <div className="p-1.5">
              {filteredWindows.map((w) => (
                <button
                  key={w.handle}
                  onClick={() => setSelected(w)}
                  className={`w-full text-left px-3 py-2 rounded-lg transition-all ${
                    selected?.handle === w.handle
                      ? 'bg-accent/20 border border-accent/50'
                      : 'hover:bg-white/5 border border-transparent'
                  }`}
                >
                  <div className="text-sm font-medium text-white truncate">{w.title}</div>
                  <div className="text-xs text-gray-500">{w.processName}.exe</div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Steam App ID Input */}
        {selected && (
          <div className="px-4 py-2 border-t border-white/5 animate-fade-in">
            <label className="block text-xs text-gray-400 mb-1">
              Steam App ID (可选，首次需要)
            </label>
            <input
              type="text"
              placeholder="例如: 1245620"
              value={steamAppId}
              onChange={(e) => setSteamAppId(e.target.value.replace(/\D/g, ''))}
              className="w-full px-3 py-1.5 bg-white/5 border border-white/10 rounded-lg text-sm text-white placeholder-gray-500 focus:outline-none focus:border-accent/50 transition-colors"
            />
            {/* 调试模式开关 */}
            <div className="flex items-center justify-between mt-2 pt-2 border-t border-white/5">
              <div>
                <div className="text-xs text-white">调试模式</div>
                <div className="text-xs text-gray-500">显示假数据测试 overlay</div>
              </div>
              <button
                onClick={() => setDebugMode(!debugMode)}
                className={`relative w-9 h-5 rounded-full transition-colors ${
                  debugMode ? 'bg-accent' : 'bg-gray-600'
                }`}
              >
                <div
                  className={`absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform ${
                    debugMode ? 'left-4' : 'left-0.5'
                  }`}
                />
              </button>
            </div>
          </div>
        )}

        {/* Footer */}
        <div className="px-4 py-3 border-t border-white/10 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg text-sm text-gray-400 hover:text-white hover:bg-white/10 transition-colors"
          >
            取消
          </button>
          <button
            onClick={handleConfirm}
            disabled={!selected}
            className="px-4 py-1.5 rounded-lg text-sm bg-accent hover:bg-accent-light text-white font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            连接游戏
          </button>
        </div>
      </div>
    </div>
  );
}
