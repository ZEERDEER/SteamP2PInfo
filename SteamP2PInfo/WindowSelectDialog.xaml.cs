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
