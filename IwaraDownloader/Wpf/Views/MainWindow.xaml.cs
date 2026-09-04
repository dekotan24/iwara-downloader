using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

        private readonly string _normalTitle = "IwaraDownloader";
        private readonly Random _partyRandom = new();
        private DispatcherTimer? _confettiTimer;

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
            PartyModeService.Changed += OnPartyModeChanged;
            if (PartyModeService.IsEnabled) StartPartyMode();
            Closed += (_, _) =>
            {
                viewModel.ClipboardMonitorToggled -= OnClipboardMonitorToggled;
                PartyModeService.Changed -= OnPartyModeChanged;
                StopPartyMode();
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
            if (DataContext is not MainViewModel vm) return;
            // 直前のClosedが何らかの理由で発火しなかった場合の自己修復。開く直前に必ず
            // falseへ戻し、この直後に発火するOpenedで正しくtrueへ戻る(通常経路では無害)。
            vm.IsVideoContextMenuOpen = false;
            vm.RefreshVideoContextMenuState();
        }

        private void VideoContextMenu_Opened(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.IsVideoContextMenuOpen = true;
        }

        private void VideoContextMenu_Closed(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            vm.IsVideoContextMenuOpen = false;
            vm.FlushPendingVideoListRefresh();
        }

        private void VideoList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // ListView(詳細リスト)/ListBox(タイル表示)の両方から呼ばれる共通ハンドラ。
            // ListViewはListBoxのサブクラスなので、共通基底型でキャストしてSelectedItemsを読む。
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedVideos = ((System.Windows.Controls.ListBox)sender).SelectedItems
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

        /// <summary>
        /// 詳細リスト表示のダブルクリック。ListView.InputBindings の MouseBinding(LeftDoubleClick)
        /// は GridViewRowPresenter 経由だと発火しなかった(タイル表示と同じ症状、実機確認)ため、
        /// PreviewMouseLeftButtonDown(Tunnel)でClickCountを見て判定する。列ヘッダーのダブルクリックは
        /// ソート操作なのでコマンドを実行しない。
        /// </summary>
        private void VideoList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (IsInHeader(e.OriginalSource as System.Windows.DependencyObject)) return;
            (DataContext as MainViewModel)?.PlayOrOpenSelectedVideoCommand.Execute(null);
        }

        private static bool IsInHeader(System.Windows.DependencyObject? source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.GridViewColumnHeader) return true;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        #region パーティーモード(隠しイースターエッグ、機能には無関係)

        private static readonly System.Windows.Media.Color[] PartyConfettiColors =
        {
            System.Windows.Media.Color.FromRgb(0xFF, 0x4D, 0x4D), System.Windows.Media.Color.FromRgb(0xFF, 0xB8, 0x4D), System.Windows.Media.Color.FromRgb(0xFF, 0xF2, 0x4D),
            System.Windows.Media.Color.FromRgb(0x4D, 0xFF, 0x88), System.Windows.Media.Color.FromRgb(0x4D, 0xC8, 0xFF), System.Windows.Media.Color.FromRgb(0x9B, 0x4D, 0xFF),
            System.Windows.Media.Color.FromRgb(0xFF, 0x4D, 0xD2),
        };
        private static readonly string[] PartyConfettiEmoji = { "🎉", "🎊", "✨", "🥳", "🎈", "⭐" };

        // ディスコ化するテーマブラシ。キーごとにアニメ開始をずらして「色が波状に流れる」ようにする。
        // 見た目のチャンネル(Accent系/枠線/ホバー背景)だけを対象にし、Text/Background/Success等の
        // 状態色は読みやすさのため素のままにする。
        private static readonly (string Key, double PhaseSeconds)[] PartyDiscoTargets =
        {
            ("Brush.Accent", 0.0), ("Brush.AccentHover", 0.5), ("Brush.Border", 1.0),
            ("Brush.BackgroundHover", 1.5), ("Brush.Favorite", 2.0),
        };
        private readonly List<SolidColorBrush> _partyDiscoBrushes = new();
        private readonly List<(ButtonBase Button, Transform OriginalTransform)> _partyWobbleButtons = new();
        private DispatcherTimer? _titleCycleTimer;
        private int _titleCycleIndex;
        private static readonly string[] PartyTitles =
        {
            "🎉 IwaraDownloader 🎉", "🎊 IwaraDownloader 🎊", "🥳 IwaraDownloader 🥳",
            "✨ IwaraDownloader ✨", "🎈 IwaraDownloader 🎈",
        };

        private void OnPartyModeChanged(bool enabled)
        {
            if (enabled) StartPartyMode();
            else StopPartyMode();
        }

        private void StartPartyMode()
        {
            _titleCycleIndex = 0;
            Title = PartyTitles[0];
            _titleCycleTimer?.Stop();
            _titleCycleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _titleCycleTimer.Tick += (_, _) =>
            {
                _titleCycleIndex = (_titleCycleIndex + 1) % PartyTitles.Length;
                Title = PartyTitles[_titleCycleIndex];
            };
            _titleCycleTimer.Start();

            PartyRainbowBorder.Visibility = Visibility.Visible;
            PartyRainbowRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
                RepeatBehavior = RepeatBehavior.Forever,
            });

            ConfettiCanvas.Visibility = Visibility.Visible;
            ConfettiCanvas.Children.Clear();
            _confettiTimer?.Stop();
            _confettiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
            _confettiTimer.Tick += SpawnConfettiPiece;
            _confettiTimer.Start();

            // テーマブラシ(DynamicResourceでウィンドウ全体のボタン枠・ホバー背景・お気に入り色等に
            // 使われている)を直接アニメーションさせ、ウィンドウ全体をディスコ調に染める。
            // XAML(pack URI)から読み込んだ元のSolidColorBrushはシール(フリーズ)済みで
            // 直接アニメーションできないため、必ずClone()してからウィンドウのResourcesへ
            // 上書きする(Cloneは常にフリーズ解除された状態で返る)。あくまで見た目だけの
            // おまけなので、万一失敗しても紙吹雪/虹枠は道連れにしない。
            _partyDiscoBrushes.Clear();
            foreach (var (key, phase) in PartyDiscoTargets)
            {
                try
                {
                    if (TryFindResource(key) is not SolidColorBrush baseBrush) continue;
                    var animatableBrush = baseBrush.Clone();
                    Resources[key] = animatableBrush;
                    _partyDiscoBrushes.Add(animatableBrush);

                    var hueCycle = new ColorAnimationUsingKeyFrames
                    {
                        Duration = TimeSpan.FromSeconds(3),
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(phase),
                    };
                    var cycleColors = PartyConfettiColors.Append(PartyConfettiColors[0]).ToArray();
                    for (int i = 0; i < cycleColors.Length; i++)
                    {
                        hueCycle.KeyFrames.Add(new LinearColorKeyFrame(cycleColors[i],
                            KeyTime.FromPercent((double)i / (cycleColors.Length - 1))));
                    }
                    animatableBrush.BeginAnimation(SolidColorBrush.ColorProperty, hueCycle);
                }
                catch (InvalidOperationException)
                {
                    // フリーズ済みリソースのクローンに失敗した等、環境依存の想定外ケース。
                    // このキーのディスコ演出だけ諦めて他は続行する。
                }
            }

            // ツールバー等、今画面に出ている全ボタンをランダムな周期・位相でぷるぷる揺らす。
            // 同期させず個々にバラバラのタイミングにすることで「お祭り騒ぎ」感を出す。
            _partyWobbleButtons.Clear();
            foreach (var button in FindVisualChildren<ButtonBase>(this))
            {
                _partyWobbleButtons.Add((button, button.RenderTransform));
                button.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

                var group = new TransformGroup();
                var rotate = new RotateTransform();
                var scale = new ScaleTransform();
                group.Children.Add(rotate);
                group.Children.Add(scale);
                button.RenderTransform = group;

                var period = TimeSpan.FromMilliseconds(_partyRandom.Next(350, 750));
                var beginOffset = TimeSpan.FromMilliseconds(_partyRandom.Next(0, 600));
                var maxAngle = _partyRandom.Next(4, 9);

                rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-maxAngle, maxAngle, period)
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = beginOffset,
                    EasingFunction = new SineEase(),
                });
                var scaleAnim = new DoubleAnimation(0.92, 1.08, period)
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = beginOffset,
                    EasingFunction = new SineEase(),
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        private void StopPartyMode()
        {
            Title = _normalTitle;
            _titleCycleTimer?.Stop();
            _titleCycleTimer = null;

            _confettiTimer?.Stop();
            _confettiTimer = null;
            ConfettiCanvas.Children.Clear();
            ConfettiCanvas.Visibility = Visibility.Collapsed;

            PartyRainbowRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            PartyRainbowBorder.Visibility = Visibility.Collapsed;

            foreach (var brush in _partyDiscoBrushes)
            {
                // Start側と同じ理由(環境依存でシール済みになるケースがある)で1件ずつガードする。
                // 止め損ねても見た目のおまけが1色回り続けるだけで実害はない。
                try { brush.BeginAnimation(SolidColorBrush.ColorProperty, null); }
                catch (InvalidOperationException) { }
            }
            _partyDiscoBrushes.Clear();

            foreach (var (button, originalTransform) in _partyWobbleButtons)
            {
                button.RenderTransform = originalTransform;
            }
            _partyWobbleButtons.Clear();
        }

        /// <summary>ビジュアルツリーを再帰的に辿って指定型の子要素を全て集める。</summary>
        private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var result = new List<T>();
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) result.Add(typed);
                result.AddRange(FindVisualChildren<T>(child));
            }
            return result;
        }

        /// <summary>紙吹雪を1つ生成し、上から下へ回転させながら降らせてフェードアウトさせる。</summary>
        private void SpawnConfettiPiece(object? sender, EventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            var text = new TextBlock
            {
                Text = PartyConfettiEmoji[_partyRandom.Next(PartyConfettiEmoji.Length)],
                FontSize = _partyRandom.Next(14, 28),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new RotateTransform(),
            };
            if (_partyRandom.Next(2) == 0)
            {
                text.Foreground = new SolidColorBrush(PartyConfettiColors[_partyRandom.Next(PartyConfettiColors.Length)]);
            }

            var startX = _partyRandom.NextDouble() * ActualWidth;
            Canvas.SetLeft(text, startX);
            Canvas.SetTop(text, -30);
            ConfettiCanvas.Children.Add(text);

            var fallDuration = TimeSpan.FromSeconds(_partyRandom.Next(4, 7));
            var fall = new DoubleAnimation(-30, ActualHeight + 30, fallDuration);
            var sway = new DoubleAnimation(startX, startX + _partyRandom.Next(-60, 60), fallDuration)
            {
                AutoReverse = false,
            };
            var spin = new DoubleAnimation(0, _partyRandom.Next(-720, 720), fallDuration);
            fall.Completed += (_, _) => ConfettiCanvas.Children.Remove(text);

            text.BeginAnimation(Canvas.TopProperty, fall);
            text.BeginAnimation(Canvas.LeftProperty, sway);
            ((RotateTransform)text.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, spin);
        }

        #endregion
    }
}
