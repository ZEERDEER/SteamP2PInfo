using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Steamworks;
using SteamP2PInfo.Config;
using SteamP2PInfo.Views;

namespace SteamP2PInfo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private ObservableCollection<SteamPeerBase> peers;
        private OverlayWindow overlay;
        private Timer timer;
        private int timerTicks = 0;
        private int overlayHotkey = 0;
        private int previousPeersAmount = 0;
        private string updateUrl;

        private const string STEAM_COMMAND = "log_ipc \"BeginAuthSession,EndAuthSession,LeaveLobby,SendClanChatMessage\"";

        private readonly SessionView sessionView = new SessionView();
        private readonly ConfigView configView = new ConfigView();

        private WindowSelectDialog.WindowInfo wInfo;

        public MainWindow()
        {
            if (Process.GetProcessesByName("SteamP2PInfo").Length > 1)
            {
                MessageBox.Show("Cannot run 2 instances of Steam P2P Info at once.", "Program Already Running", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowUnhandledException((Exception)e.ExceptionObject, "CurrentDomain", e.IsTerminating);
            TaskScheduler.UnobservedTaskException += (s, e) => ShowUnhandledException(e.Exception, "TaskScheduler", false);
            Dispatcher.UnhandledException += (s, e) => { if (!Debugger.IsAttached) ShowUnhandledException(e.Exception, "Dispatcher", true); };

            InitializeComponent();
            Closing += MainWindow_Closed;

            peers = new ObservableCollection<SteamPeerBase>();
            sessionView.SetPeerSource(peers);
            sessionView.AttachRequested += AttachGame;

            navSession.Click += (s, e) => SelectPane(navSession);
            navConfig.Click += (s, e) => SelectPane(navConfig);
            SelectPane(navSession);

            Title = "Steam P2P Info " + VersionCheck.CurrentVersion;

            timer = new Timer(Timer_Tick, null, Timeout.Infinite, Timeout.Infinite);
            Settings.Default.PropertyChanged += (s, e) => Settings.Default.Save();

            Task.Run(() =>
            {
                if (VersionCheck.FetchLatest())
                {
                    string v = VersionCheck.LatestRelease["tag_name"].ToString();
                    if (string.Compare(VersionCheck.CurrentVersion, v) < 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            updateUrl = "https://github.com/tremwil/SteamP2PInfo/releases/tag/" + v;
                            updateBadge.Content = $"Update {v} available";
                            updateBadge.Visibility = Visibility.Visible;
                            _ = Dialogs.ShowUpdateAvailable(this, v, new Uri(updateUrl));
                        });
                    }
                }
            });
        }

        private void SelectPane(Wpf.Ui.Controls.NavigationViewItem item)
        {
            // NavigationView.SelectedItem has a non-public setter and only changes through its own
            // page-navigation pipeline. With the pane used standalone, IsActive is the public
            // selection state; the item's inherited Click event drives the switch.
            navSession.IsActive = ReferenceEquals(item, navSession);
            navConfig.IsActive = ReferenceEquals(item, navConfig);
            rootContent.Content = ReferenceEquals(item, navConfig) ? (object)configView : sessionView;
        }

        private void UpdateBadge_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(updateUrl))
                Process.Start(new ProcessStartInfo(updateUrl));
        }

        private void Timer_Tick(object o)
        {
            Dispatcher.Invoke(() =>
            {
                // Necessary to close the program after the game exits, as SteamAPI_Shutdown isn't
                // sufficient to have steam recognize the game is no longer running
                if (!WinAPI.User32.IsWindow(wInfo.Handle))
                    Close();

                if (HotkeyManager.Enabled && !GameConfig.Current.HotkeysEnabled)
                    HotkeyManager.Disable();

                if (!HotkeyManager.Enabled && GameConfig.Current.HotkeysEnabled)
                    HotkeyManager.Enable();

                if ((timerTicks = (timerTicks + 1) % 6) == 0)
                {
                    // Rather not have the settings update on a loop, but
                    // Fody generated OnChange seems to break PropertyChanged
                    // for GameConfig. So do this for now.
                    GameConfig.Current?.Save();
                    SteamPeerManager.UpdatePeerList();
                }

                peers.Clear();
                foreach (SteamPeerBase p in SteamPeerManager.GetPeers())
                    peers.Add(p);
                sessionView.UpdateStatus(peers.Count);

                if (GameConfig.Current.PlaySoundOnNewSession)
                {
                    if (peers.Count > 0 && previousPeersAmount == 0)
                    {
                        System.Media.SystemSounds.Beep.Play();
                    }
                    previousPeersAmount = peers.Count;
                }

                // Update overlay column sizes
                foreach (var col in overlay.dataGrid.Columns)
                {
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.Pixel);
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                }
                overlay.dataGrid.UpdateLayout();

                // Queue position update after the overlay has re-rendered
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(overlay.UpdatePosition));
                overlay.UpdateVisibility();
            });
        }

        private async void ShowUnhandledException(Exception err, string type, bool fatal)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowUnhandledException(err, type, fatal));
                return;
            }

            System.Media.SystemSounds.Exclamation.Play();
            var result = await new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = $"Unhandled Exception: {err.GetType().Name}",
                Content = $"{err.Message}\n{err.StackTrace}",
                PrimaryButtonText = "Copy",
                CloseButtonText = "Close",
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                Clipboard.SetText($"{err.GetType().Name}: {err.Message}\n{err.StackTrace}");

            Close();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (GameConfig.Current != null) GameConfig.Current.Save();
            Settings.Default.Save();
            if (overlay != null) overlay.Close();
            HotkeyManager.Disable();
            ETWPingMonitor.Stop();
        }

        private async void AttachGame()
        {
            if (wInfo != null)
                return;

            WindowSelectDialog dialog = new WindowSelectDialog() { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            GameConfig.LoadOrCreate(dialog.SelectedWindow.ProcessName);

            if (!Directory.Exists(Path.GetDirectoryName(Settings.Default.SteamLogPath)))
            {
                await Dialogs.ShowError(this, "Directory Not Found", "Steam IPC log file directory does not exist. Please modify the config accordingly.");
                return;
            }

            if (GameConfig.Current.SteamAppId == 0)
            {
                int? appId = await Dialogs.PromptAppId(this);
                if (appId == null)
                {
                    await Dialogs.ShowError(this, "Input Error", "Please input a valid number");
                    return;
                }
                GameConfig.Current.SteamAppId = appId.Value;
            }

            Environment.SetEnvironmentVariable("SteamAppId", GameConfig.Current.SteamAppId.ToString());
            if (!SteamAPI.Init())
            {
                await Dialogs.ShowError(this, "Steam API Error", "Could not initialize Steam API. Make sure the provided AppId is valid!");
                GameConfig.Current.SteamAppId = 0;
                GameConfig.Current.Save();
                return;
            }

            wInfo = dialog.SelectedWindow;
            SteamPeerManager.Init();

            if (MustEnterSteamCommand())
                SteamConsoleHelper();

            HotkeyManager.RemoveHotkey(overlayHotkey);
            overlayHotkey = HotkeyManager.AddHotkey(wInfo.Handle, () => GameConfig.Current.OverlayConfig.Hotkey, () => GameConfig.Current.OverlayConfig.Enabled ^= true);

            overlay = new OverlayWindow(wInfo.Handle, wInfo.ProcessId, wInfo.ThreadId);
            overlay.dataGrid.DataContext = peers;
            if (!overlay.InstallMsgHook())
            {
                overlay.Close();
                await Dialogs.ShowError(this, "WINAPI Error", "Failed to setup overlay message hook");
                Close();
                return;
            }

            sessionView.ShowAttached(wInfo.Title);
            configView.RebuildEditor(wInfo.Title);

            ETWPingMonitor.Start();
            timer.Change(0, 1000);
        }

        private bool MustEnterSteamCommand()
        {
            DateTime ipcLogDate;
            String startupDateString = null;

            // Check if the program was recently updated -- We'll want to enter the command again if so
            if (Settings.Default.LastRunVersion != VersionCheck.CurrentVersion)
            {
                Settings.Default.LastRunVersion = VersionCheck.CurrentVersion;
                Settings.Default.Save();
                return true;
            }

            if (!File.Exists(Settings.Default.SteamLogPath))
                return true;

            try
            {
                ipcLogDate = File.GetLastWriteTime(Settings.Default.SteamLogPath);
            }
            catch (Exception)
            {
                return true;
            }

            try
            {
                using (FileStream stream = new FileStream(Settings.Default.SteamBootstrapLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var reader = new ReverseTextReader(stream, Encoding.UTF8);
                    var today = DateTime.Today;
                    int dateCheckCountdown = 20;
                    while (!reader.EndOfStream)
                    {
                        String line = reader.ReadLine();
                        if (line.Trim().Length == 0)
                            continue;
                        else if (line.Contains("Startup - updater built"))
                        {
                            int substringStartIndex = line.IndexOf("[") + 1;
                            startupDateString = line.Substring(substringStartIndex, line.IndexOf("]") - substringStartIndex);
                            break;
                        }
                        else
                        {
                            if (--dateCheckCountdown == 0)
                            {
                                int substringStartIndex = line.IndexOf("[") + 1;
                                DateTime lineDate = DateTime.Parse(line.Substring(substringStartIndex, line.IndexOf("]") - substringStartIndex));
                                if (today.Subtract(lineDate).TotalHours > 24)
                                    return true; // let's assume Steam hasn't been running for 24+ hours
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return true;
            }

            return startupDateString == null || DateTime.Parse(startupDateString) > ipcLogDate;
        }

        private async void SteamConsoleHelper()
        {
            Process.Start(new ProcessStartInfo("steam://open/console"));

            if (await Dialogs.ShowCopyCommand(this, STEAM_COMMAND))
            {
                try
                {
                    Clipboard.SetText(STEAM_COMMAND);
                }
                catch (Exception e)
                {
                    await Dialogs.ShowError(this, "Write to Clipboard Failed!", $"Failed to copy command to clipboard. Please enter '{STEAM_COMMAND}' manually.\n\n {e}");
                }
            }
        }
    }
}
