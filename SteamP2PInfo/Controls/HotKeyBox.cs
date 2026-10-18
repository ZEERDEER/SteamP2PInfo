using System;
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
            Text = HotkeyUtils.Format(Value);
            GotFocus += (s, e) => Text = "Press keys…";
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
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

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
