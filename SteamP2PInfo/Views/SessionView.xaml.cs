using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteamP2PInfo.Config;

namespace SteamP2PInfo.Views
{
    public partial class SessionView : UserControl
    {
        public event Action AttachRequested;

        public SessionView()
        {
            InitializeComponent();
        }

        // Internal (not public as written in the plan) because SteamPeerBase is an internal type.
        internal void SetPeerSource(ObservableCollection<SteamPeerBase> peers)
        {
            peerList.ItemsSource = peers;
        }

        public void ShowAttached(string title)
        {
            emptyState.Visibility = Visibility.Collapsed;
            attachedContent.Visibility = Visibility.Visible;
            gameName.Text = title;
        }

        public void UpdateStatus(int peerCount)
        {
            statusText.Text = $"Connected · {peerCount} player{(peerCount == 1 ? "" : "s")}";
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            AttachRequested?.Invoke();
        }

        // Border has no MouseDoubleClick event (it is Control-only), so double clicks are
        // detected the WPF way: MouseLeftButtonDown with ClickCount == 2.
        private void PeerCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            if (!((sender as FrameworkElement)?.DataContext is SteamPeerBase peer))
                return;

            if (GameConfig.Current.OpenProfileInOverlay)
                Steamworks.SteamFriends.ActivateGameOverlayToUser("steamid", peer.SteamID);
            else
                Process.Start($"https://steamcommunity.com/profiles/{peer.SteamID}");
        }
    }
}
