import { Settings, Monitor, Volume2, Keyboard, FolderOpen, FileText, Type, X } from 'lucide-react';
import { useState, useEffect, useRef } from 'react';
import type { GameConfig, AppConfig } from '../types';

interface ConfigPanelProps {
  config: GameConfig | null;
  appConfig: AppConfig | null;
  onUpdate: (config: Partial<GameConfig>) => void;
  onUpdateAppConfig: (config: Partial<AppConfig>) => void;
  onBrowseFile?: (options?: { filter?: string; title?: string; initialDir?: string }) => Promise<string | null>;
  onShowFontDialog?: (currentFont?: string, currentSize?: number) => Promise<{ fontFamily: string; fontSize: number; fontString: string } | null>;
}

interface ToggleProps {
  label: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

function Toggle({ label, description, checked, onChange }: ToggleProps) {
  return (
    <div className="flex items-center justify-between py-2">
      <div>
        <div className="text-white font-medium text-sm">{label}</div>
        {description && <div className="text-xs text-gray-500">{description}</div>}
      </div>
      <button
        onClick={() => onChange(!checked)}
        className={`relative w-11 h-6 rounded-full transition-colors ${
          checked ? 'bg-accent' : 'bg-gray-600'
        }`}
      >
        <div
          className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${
            checked ? 'left-6' : 'left-1'
          }`}
        />
      </button>
    </div>
  );
}

interface SectionTitleProps {
  icon: React.ReactNode;
  title: string;
}

function SectionTitle({ icon, title }: SectionTitleProps) {
  return (
    <div className="flex items-center gap-2 py-3 border-b border-white/10 mb-2">
      <div className="w-6 h-6 rounded-md bg-accent/20 flex items-center justify-center text-accent">
        {icon}
      </div>
      <span className="text-white font-semibold text-sm">{title}</span>
    </div>
  );
}

interface PathInputProps {
  label: string;
  description?: string;
  value: string;
  onChange: (value: string) => void;
  onBrowse?: () => void;
}

function PathInput({ label, description, value, onChange, onBrowse }: PathInputProps) {
  return (
    <div className="py-2">
      <div className="flex items-center justify-between mb-1">
        <div>
          <div className="text-white font-medium text-sm">{label}</div>
          {description && <div className="text-xs text-gray-500">{description}</div>}
        </div>
      </div>
      <div className="flex gap-2">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          autoComplete="off"
          spellCheck={false}
          className="flex-1 px-3 py-1.5 bg-white/5 border border-white/10 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-accent/50 transition-colors text-xs font-mono"
        />
        <button
          onClick={onBrowse}
          className="px-3 py-1.5 bg-accent/20 text-accent rounded-lg hover:bg-accent/30 transition-colors"
          title="浏览"
        >
          <FolderOpen size={16} />
        </button>
      </div>
    </div>
  );
}

interface TextInputProps {
  label: string;
  description?: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

function TextInput({ label, description, value, onChange, placeholder }: TextInputProps) {
  return (
    <div className="py-2">
      <div className="mb-1">
        <div className="text-white font-medium text-sm">{label}</div>
        {description && <div className="text-xs text-gray-500">{description}</div>}
      </div>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete="off"
        spellCheck={false}
        className="w-full px-3 py-1.5 bg-white/5 border border-white/10 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-accent/50 transition-colors text-sm"
      />
    </div>
  );
}

interface NumberInputProps {
  label: string;
  description?: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  step?: number;
}

function NumberInput({ label, description, value, onChange, min = 0, max = 1, step = 0.001 }: NumberInputProps) {
  return (
    <div className="py-2">
      <div className="flex items-center justify-between mb-1">
        <div>
          <div className="text-white font-medium text-sm">{label}</div>
          {description && <div className="text-xs text-gray-500">{description}</div>}
        </div>
        <span className="text-accent font-mono text-xs">{value.toFixed(3)}</span>
      </div>
      <input
        type="range"
        value={value}
        onChange={(e) => onChange(parseFloat(e.target.value))}
        min={min}
        max={max}
        step={step}
        className="w-full h-1.5 bg-white/10 rounded-lg appearance-none cursor-pointer accent-accent"
      />
    </div>
  );
}

interface SelectInputProps {
  label: string;
  description?: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}

function SelectInput({ label, description, value, onChange, options }: SelectInputProps) {
  return (
    <div className="py-2">
      <div className="mb-1">
        <div className="text-white font-medium text-sm">{label}</div>
        {description && <div className="text-xs text-gray-500">{description}</div>}
      </div>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full px-3 py-1.5 bg-white/5 border border-white/10 rounded-lg text-white focus:outline-none focus:border-accent/50 transition-colors text-sm"
      >
        {options.map((opt) => (
          <option key={opt.value} value={opt.value} className="bg-gray-900">
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
}

interface ColorInputProps {
  label: string;
  description?: string;
  value: string;
  onChange: (value: string) => void;
}

function ColorInput({ label, description, value, onChange }: ColorInputProps) {
  const displayColor = value.length === 9 ? '#' + value.slice(3) : value;
  
  return (
    <div className="py-2">
      <div className="flex items-center justify-between">
        <div>
          <div className="text-white font-medium text-sm">{label}</div>
          {description && <div className="text-xs text-gray-500">{description}</div>}
        </div>
        <div className="flex items-center gap-2">
          <input
            type="color"
            value={displayColor}
            onChange={(e) => {
              const alpha = value.length === 9 ? value.slice(1, 3) : 'FF';
              onChange('#' + alpha + e.target.value.slice(1));
            }}
            className="w-8 h-8 rounded border border-white/10 cursor-pointer"
          />
          <span className="text-gray-400 text-xs font-mono">{value}</span>
        </div>
      </div>
    </div>
  );
}

// 虚拟键码到名称的映射
const VK_NAMES: Record<number, string> = {
  0: '无',
  8: 'Backspace', 9: 'Tab', 13: 'Enter', 16: 'Shift', 17: 'Ctrl', 18: 'Alt',
  19: 'Pause', 20: 'CapsLock', 27: 'Esc', 32: 'Space',
  33: 'PageUp', 34: 'PageDown', 35: 'End', 36: 'Home',
  37: '←', 38: '↑', 39: '→', 40: '↓',
  45: 'Insert', 46: 'Delete',
  48: '0', 49: '1', 50: '2', 51: '3', 52: '4', 53: '5', 54: '6', 55: '7', 56: '8', 57: '9',
  65: 'A', 66: 'B', 67: 'C', 68: 'D', 69: 'E', 70: 'F', 71: 'G', 72: 'H', 73: 'I', 74: 'J',
  75: 'K', 76: 'L', 77: 'M', 78: 'N', 79: 'O', 80: 'P', 81: 'Q', 82: 'R', 83: 'S', 84: 'T',
  85: 'U', 86: 'V', 87: 'W', 88: 'X', 89: 'Y', 90: 'Z',
  112: 'F1', 113: 'F2', 114: 'F3', 115: 'F4', 116: 'F5', 117: 'F6',
  118: 'F7', 119: 'F8', 120: 'F9', 121: 'F10', 122: 'F11', 123: 'F12',
  144: 'NumLock', 145: 'ScrollLock',
  186: ';', 187: '=', 188: ',', 189: '-', 190: '.', 191: '/', 192: '`',
  219: '[', 220: '\\', 221: ']', 222: "'",
};

// e.code (DOM 标准) 到 Windows 虚拟键码的映射
const CODE_TO_VK: Record<string, number> = {
  Backspace: 8, Tab: 9, Enter: 13, ShiftLeft: 16, ShiftRight: 16,
  ControlLeft: 17, ControlRight: 17, AltLeft: 18, AltRight: 18,
  Pause: 19, CapsLock: 20, Escape: 27, Space: 32,
  PageUp: 33, PageDown: 34, End: 35, Home: 36,
  ArrowLeft: 37, ArrowUp: 38, ArrowRight: 39, ArrowDown: 40,
  Insert: 45, Delete: 46,
  Digit0: 48, Digit1: 49, Digit2: 50, Digit3: 51, Digit4: 52,
  Digit5: 53, Digit6: 54, Digit7: 55, Digit8: 56, Digit9: 57,
  KeyA: 65, KeyB: 66, KeyC: 67, KeyD: 68, KeyE: 69, KeyF: 70, KeyG: 71,
  KeyH: 72, KeyI: 73, KeyJ: 74, KeyK: 75, KeyL: 76, KeyM: 77, KeyN: 78,
  KeyO: 79, KeyP: 80, KeyQ: 81, KeyR: 82, KeyS: 83, KeyT: 84, KeyU: 85,
  KeyV: 86, KeyW: 87, KeyX: 88, KeyY: 89, KeyZ: 90,
  F1: 112, F2: 113, F3: 114, F4: 115, F5: 116, F6: 117,
  F7: 118, F8: 119, F9: 120, F10: 121, F11: 122, F12: 123,
  NumLock: 144, ScrollLock: 145,
  Semicolon: 186, Equal: 187, Comma: 188, Minus: 189,
  Period: 190, Slash: 191, Backquote: 192,
  BracketLeft: 219, Backslash: 220, BracketRight: 221, Quote: 222,
  Numpad0: 96, Numpad1: 97, Numpad2: 98, Numpad3: 99, Numpad4: 100,
  Numpad5: 101, Numpad6: 102, Numpad7: 103, Numpad8: 104, Numpad9: 105,
  NumpadMultiply: 106, NumpadAdd: 107, NumpadSubtract: 109, NumpadDecimal: 110, NumpadDivide: 111,
};

// 从 e.code 获取 Windows 虚拟键码
function codeToVK(code: string): number {
  return CODE_TO_VK[code] || 0;
}

interface HotkeyInputProps {
  label: string;
  description?: string;
  value: number;
  onChange: (value: number) => void;
}

function HotkeyInput({ label, description, value, onChange }: HotkeyInputProps) {
  const [isListening, setIsListening] = useState(false);
  const inputRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!isListening) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      e.preventDefault();
      e.stopPropagation();
      
      // 忽略修饰键单独按下
      const modifierCodes = ['ShiftLeft', 'ShiftRight', 'ControlLeft', 'ControlRight', 'AltLeft', 'AltRight'];
      if (modifierCodes.includes(e.code)) return;
      
      // 使用 e.code 获取虚拟键码
      const vk = codeToVK(e.code);
      if (vk > 0) {
        onChange(vk);
      }
      setIsListening(false);
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isListening, onChange]);

  const displayName = VK_NAMES[value] || (value > 0 ? `键码 ${value}` : '点击设置');

  return (
    <div className="py-2">
      <div className="flex items-center justify-between">
        <div>
          <div className="text-white font-medium text-sm">{label}</div>
          {description && <div className="text-xs text-gray-500">{description}</div>}
        </div>
        <div className="flex items-center gap-2">
          <button
            ref={inputRef}
            onClick={() => setIsListening(true)}
            onBlur={() => setIsListening(false)}
            className={`px-3 py-1.5 rounded-lg text-sm font-mono transition-colors ${
              isListening
                ? 'bg-accent text-white animate-pulse'
                : 'bg-white/10 text-white hover:bg-white/20'
            }`}
          >
            {isListening ? '按下按键...' : displayName}
          </button>
          {value > 0 && (
            <button
              onClick={() => onChange(0)}
              className="p-1 rounded hover:bg-white/10 text-gray-400 hover:text-white transition-colors"
              title="清除热键"
            >
              <X size={14} />
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export function ConfigPanel({ config, appConfig, onUpdate, onUpdateAppConfig, onBrowseFile, onShowFontDialog }: ConfigPanelProps) {
  const handleBrowseSteamLog = async () => {
    if (!onBrowseFile) return;
    const path = await onBrowseFile({
      title: '选择 Steam IPC 日志文件',
      filter: 'Log files (*.log)|*.log|All files (*.*)|*.*',
      initialDir: 'C:\\Program Files (x86)\\Steam\\logs'
    });
    if (path) {
      onUpdateAppConfig({ steamLogPath: path });
    }
  };

  const handleSelectFont = async () => {
    if (!onShowFontDialog || !config) return;
    // 解析当前字体设置
    const parts = config.overlay.font.split(',');
    const currentFont = parts[0]?.trim() || 'Segoe UI';
    const sizeMatch = parts[1]?.match(/(\d+\.?\d*)/);
    const currentSize = sizeMatch ? parseFloat(sizeMatch[1]) : 14;
    
    const result = await onShowFontDialog(currentFont, currentSize);
    if (result) {
      onUpdate({ overlay: { ...config.overlay, font: result.fontString } });
    }
  };

  return (
    <div className="space-y-4">
      {/* Steam 日志路径设置 */}
      <div>
        <SectionTitle icon={<FileText size={14} />} title="Steam 日志" />
        <PathInput
          label="IPC 日志路径"
          description="Steam 客户端 IPC 日志文件"
          value={appConfig?.steamLogPath || ''}
          onChange={(value) => onUpdateAppConfig({ steamLogPath: value })}
          onBrowse={handleBrowseSteamLog}
        />
      </div>

      {/* 游戏设置 - 仅在连接游戏后显示 */}
      {config ? (
        <>
          {/* 通用设置 */}
          <div>
            <SectionTitle icon={<Settings size={14} />} title="通用设置" />
            <Toggle
              label="添加到最近一起玩"
              description="让队友出现在 Steam 最近玩家列表"
              checked={config.setPlayedWith}
              onChange={(checked) => onUpdate({ setPlayedWith: checked })}
            />
            <Toggle
              label="在 Steam 悬浮层打开资料"
              description="双击玩家名称时在游戏内打开"
              checked={config.openProfileInOverlay}
              onChange={(checked) => onUpdate({ openProfileInOverlay: checked })}
            />
            <Toggle
              label="记录活动日志"
              description="保存玩家连接/断开记录"
              checked={config.logActivity}
              onChange={(checked) => onUpdate({ logActivity: checked })}
            />
          </div>

          {/* 通知设置 */}
          <div>
            <SectionTitle icon={<Volume2 size={14} />} title="通知" />
            <Toggle
              label="新会话提示音"
              description="有新玩家加入时播放声音"
              checked={config.playSoundOnNewSession}
              onChange={(checked) => onUpdate({ playSoundOnNewSession: checked })}
            />
          </div>

          {/* 热键设置 */}
          <div>
            <SectionTitle icon={<Keyboard size={14} />} title="悬浮窗热键" />
            <Toggle
              label="启用热键"
              description="按 Home 键显示/隐藏悬浮窗"
              checked={config.hotkeysEnabled}
              onChange={(checked) => onUpdate({ hotkeysEnabled: checked })}
            />
          </div>

          {/* 悬浮窗设置 */}
          <div>
            <SectionTitle icon={<Monitor size={14} />} title="悬浮窗" />
            <Toggle
              label="启用悬浮窗"
              description="在游戏中显示玩家信息"
              checked={config.overlay.enabled}
              onChange={(checked) => onUpdate({ overlay: { ...config.overlay, enabled: checked } })}
            />
            <Toggle
              label="显示 Steam ID"
              checked={config.overlay.showSteamId}
              onChange={(checked) => onUpdate({ overlay: { ...config.overlay, showSteamId: checked } })}
            />
            <Toggle
              label="显示连接质量"
              checked={config.overlay.showConnectionQuality}
              onChange={(checked) => onUpdate({ overlay: { ...config.overlay, showConnectionQuality: checked } })}
            />
            
            <div className="mt-3 pt-3 border-t border-white/5">
              <div className="text-xs text-gray-500 uppercase tracking-wider mb-2">高级</div>
              
              <TextInput
                label="横幅格式"
                description="支持 {time:HH:mm:ss}"
                value={config.overlay.bannerFormat}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, bannerFormat: value } })}
                placeholder="[{time:HH:mm:ss}] Steam P2P Info Modern"
              />
              
              {/* 字体选择器 */}
              <div className="py-2">
                <div className="mb-1">
                  <div className="text-white font-medium text-sm">字体</div>
                  <div className="text-xs text-gray-500">悬浮窗显示字体</div>
                </div>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={config.overlay.font}
                    onChange={(e) => onUpdate({ overlay: { ...config.overlay, font: e.target.value } })}
                    placeholder="Segoe UI, 14pt"
                    autoComplete="off"
                    spellCheck={false}
                    className="flex-1 px-3 py-1.5 bg-white/5 border border-white/10 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-accent/50 transition-colors text-sm"
                  />
                  <button
                    onClick={handleSelectFont}
                    className="px-3 py-1.5 bg-accent/20 text-accent rounded-lg hover:bg-accent/30 transition-colors"
                    title="选择字体"
                  >
                    <Type size={16} />
                  </button>
                </div>
              </div>
              
              <SelectInput
                label="锚点位置"
                value={config.overlay.anchor}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, anchor: value } })}
                options={[
                  { value: 'TopLeft', label: '左上角' },
                  { value: 'TopRight', label: '右上角' },
                  { value: 'BottomLeft', label: '左下角' },
                  { value: 'BottomRight', label: '右下角' },
                ]}
              />
              
              <NumberInput
                label="X 偏移"
                value={config.overlay.xOffset}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, xOffset: value } })}
              />
              
              <NumberInput
                label="Y 偏移"
                value={config.overlay.yOffset}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, yOffset: value } })}
              />
              
              <ColorInput
                label="文字颜色"
                value={config.overlay.textColor}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, textColor: value } })}
              />
              
              <ColorInput
                label="描边颜色"
                value={config.overlay.strokeColor}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, strokeColor: value } })}
              />
              
              <NumberInput
                label="描边宽度"
                value={config.overlay.strokeWidth}
                onChange={(value) => onUpdate({ overlay: { ...config.overlay, strokeWidth: value } })}
                min={0}
                max={10}
                step={0.5}
              />
            </div>
          </div>
        </>
      ) : (
        <div className="text-center py-8 text-gray-500">
          <p>连接游戏后可配置游戏特定设置</p>
        </div>
      )}
    </div>
  );
}
