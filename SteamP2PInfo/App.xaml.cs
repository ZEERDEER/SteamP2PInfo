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
