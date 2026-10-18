using System.Windows.Controls;
using SteamP2PInfo.Config;

namespace SteamP2PInfo.Views
{
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();
        }

        /// <summary>Rebuilds the generated settings cards for GameConfig.Current (call after attach).</summary>
        public void RebuildEditor(string gameTitle)
        {
            generatedEditor.Children.Clear();

            if (GameConfig.Current == null)
                return;

            subtitleText.Text = $"Settings for {gameTitle} · saved to config\\{GameConfig.Current.ProcessName}.json";
            generatedEditor.Children.Add(ConfigUIBuilder.CreateConfigEditor(GameConfig.Current));
        }
    }
}
