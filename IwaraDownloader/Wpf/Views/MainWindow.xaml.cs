using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using IwaraDownloader.Wpf.Theme;
using IwaraDownloader.Wpf.ViewModels;

namespace IwaraDownloader.Wpf.Views
{
    /// <summary>
    /// WPF版メインウィンドウ。Phase8bでアプリライフサイクル(トレイ/クリップボード監視)を追加。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll")]
        private static extern bool AddClipboardFormatListener(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool RemoveClipboardFormatListener(nint hWnd);

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isClosing;
        private bool _clipboardListenerRegistered;

        public MainWindow(DownloadManager downloadManager)
        {
            InitializeComponent();
            ThemeManager.Apply(this);

            var viewModel = new MainViewModel(downloadManager) { OwnerWindow = this };
            DataContext = viewModel;
            viewModel.ClipboardMonitorToggled += OnClipboardMonitorToggled;

            var settings = SettingsManager.Instance.Settings;
            if (settings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
            }

            SourceInitialized += MainWindow_SourceInitialized;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Closed += (_, _) =>
            {
                viewModel.ClipboardMonitorToggled -= OnClipboardMonitorToggled;
                _notifyIcon?.Dispose();
            };
        }

        /// <summary>
        /// ウィンドウハンドルが確定した直後(旧WinForms版MainForm_Loadのトレイアイコン初期化相当)。
        /// クリップボードフック登録とNotifyIcon生成はハンドルが要るためここで行う。
        /// </summary>
        private void MainWindow_SourceInitialized(object? sender, System.EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }

            SetupNotifyIcon();

            if (DataContext is MainViewModel vm && vm.ClipboardMonitorEnabled)
            {
                RegisterClipboardListener();
            }
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                (DataContext as MainViewModel)?.OnClipboardChanged();
            }
            return nint.Zero;
        }

        private void OnClipboardMonitorToggled(bool enabled)
        {
            if (enabled) RegisterClipboardListener();
            else UnregisterClipboardListener();
        }

        private void RegisterClipboardListener()
        {
            if (_clipboardListenerRegistered) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == nint.Zero) return;
            if (AddClipboardFormatListener(hwnd)) _clipboardListenerRegistered = true;
        }

        private void UnregisterClipboardListener()
        {
            if (!_clipboardListenerRegistered) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != nint.Zero) RemoveClipboardFormatListener(hwnd);
            _clipboardListenerRegistered = false;
        }

        /// <summary>旧WinForms版MainForm独自のnotifyIcon(Designerコンポーネント)+menuShow/menuExitに対応。</summary>
        private void SetupNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "IwaraDownloader",
                Visible = true,
            };
            try
            {
                // Assembly.Location は IwaraDownloader.dll を指す(アイコン未埋め込み)ため、
                // icon.ico が埋め込まれた実行ファイル自体のパスを取る
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add(L.T("MainForm_menuShow"), null, (_, _) => RestoreFromTray());
            menu.Items.Add(L.T("MainForm_menuExit"), null, (_, _) =>
            {
                _isClosing = true;
                Close();
            });
            _notifyIcon.ContextMenuStrip = menu;

            NotificationService.Instance.SetNotifyIcon(_notifyIcon);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        /// <summary>手動最小化(タスクバー等)でもタスクバーから消す。旧WinForms版MainForm_Resizeに対応。</summary>
        private void MainWindow_StateChanged(object? sender, System.EventArgs e)
        {
            if (WindowState == WindowState.Minimized && SettingsManager.Instance.Settings.MinimizeToTray)
            {
                ShowInTaskbar = false;
            }
        }

        /// <summary>
        /// 旧WinForms版MainForm_FormClosingに対応。MinimizeToTray設定時はXボタンでの閉じるを
        /// トレイ最小化に読み替える。実際に終了する経路(トレイメニューの「終了」)では
        /// DL中/タグ書き込み中ならバルーン通知を出してからMainViewModel.Dispose()で後始末する。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            var settings = SettingsManager.Instance.Settings;
            if (!_isClosing && settings.MinimizeToTray)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                return;
            }

            if (DataContext is not MainViewModel vm) return;

            try
            {
                if (vm.IsSlowCloseExpected)
                {
                    _notifyIcon?.ShowBalloonTip(10000, "IwaraDownloader",
                        L.T("MainForm_D016"), System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            catch { }

            vm.Dispose();
            if (_notifyIcon != null) _notifyIcon.Visible = false;
        }

        private void VideoList_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            (DataContext as MainViewModel)?.RefreshVideoContextMenuState();
        }

        private void VideoList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedVideos = ((System.Windows.Controls.ListView)sender).SelectedItems
                    .Cast<VideoListItemViewModel>().ToList();
            }
        }

        private void ChannelTree_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            (DataContext as MainViewModel)?.RefreshChannelContextMenuState();
        }

        private void ToolbarDropdown_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var button = (System.Windows.Controls.Button)sender;
            if (button.ContextMenu == null) return;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        /// <summary>
        /// ツールバー項目にマウスホバー/キーボードフォーカスした時、その説明(ToolTipに設定した文字列)を
        /// ステータスバーに表示する。ToolTipプロパティを唯一の説明文ソースとして再利用することで、
        /// 通常の吹き出しツールチップとステータスバー表示の両方を1箇所の記述でまかなう。
        /// </summary>
        private void ToolbarItem_ShowHint(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            vm.HoverHintText = (sender as System.Windows.FrameworkElement)?.ToolTip as string ?? "";
        }

        private void ToolbarItem_HideHint(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.HoverHintText = "";
        }

        /// <summary>
        /// タイル表示の1枠分。VirtualizingWrapPanelはコンテナをリサイクルするため、
        /// Loadedではなくスクロールでコンテナが別の動画に再利用されるたびに発火する
        /// DataContextChangedでサムネ読み込みをトリガーする。
        /// </summary>
        private void TileItem_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            (e.NewValue as VideoListItemViewModel)?.EnsureThumbnailLoaded();
        }

        /// <summary>
        /// タイル表示のダブルクリック。VirtualizingWrapPanel経由だとListBox.InputBindingsの
        /// MouseBinding(LeftDoubleClick)が発火しなかった(実機確認、パネル側でイベントを吸収している
        /// 可能性)ため、PreviewMouseLeftButtonDown(Tunnel、パネルより先に届く)でClickCountを見て判定する。
        /// </summary>
        private void TileList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            (DataContext as MainViewModel)?.PlayOrOpenSelectedVideoCommand.Execute(null);
        }

        private void TileItem_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (((System.Windows.FrameworkElement)sender).DataContext is VideoListItemViewModel vm)
                vm.EnsureThumbnailLoaded();
        }

        private void VideoColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e.OriginalSource is not System.Windows.Controls.GridViewColumnHeader header || header.Column == null) return;
            var listView = (System.Windows.Controls.ListView)sender;
            var gridView = (System.Windows.Controls.GridView)listView.View;
            int index = gridView.Columns.IndexOf(header.Column);
            if (index >= 0)
            {
                (DataContext as MainViewModel)?.SortVideosByColumn(index);
            }
        }
    }
}
