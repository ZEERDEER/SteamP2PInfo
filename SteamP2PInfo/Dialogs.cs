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

            if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
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
