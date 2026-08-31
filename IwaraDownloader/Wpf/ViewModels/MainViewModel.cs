using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IwaraDownloader.Forms;
using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using IwaraDownloader.Wpf.Models;
using IwaraDownloader.Wpf.Theme;
using Brush = System.Windows.Media.Brush;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// MainWindow用ViewModel。Phase4bでチャンネルツリーの読み込みを実装。
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private const int TreeRefreshDebounceMs = 200;
        private const int VideoListRefreshDebounceMs = 200;
        private const int DownloadCountDebounceMs = 500;

        private readonly DatabaseService _database = DatabaseService.Instance;
        // Phase8b: DownloadManagerはコンストラクタ引数で受け取る単一インスタンス。
        // 生成元はコンストラクタ側(--wpf-main起動時はProgram.cs)。MainViewModelが独自にnew()する
        // ことは二重インスタンス(WebServerServiceとの不整合含む)の温床になるため禁止。
        private readonly DownloadManager _downloadManager;
        private readonly WebServerService _webServer;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private DispatcherTimer? _treeRefreshTimer;
        private DispatcherTimer? _videoListRefreshTimer;
        private DispatcherTimer? _downloadCountTimer;

        /// <summary>
        /// ファイル選択ダイアログ(LocalFileMapHelper.MapAsync)の親ウィンドウ指定用。
        /// MainWindow.xaml.csのコンストラクタから設定される(MVVM上は妥協だが、
        /// IWin32Windowを要求する既存WinForms UtilsをWPFから呼ぶ以上ハンドルが必要)。
        /// </summary>
        public System.Windows.Window? OwnerWindow { get; set; }

        private sealed class Win32WindowWrapper : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }
            public Win32WindowWrapper(IntPtr handle) => Handle = handle;
        }

        public BulkObservableCollection<ChannelTreeNodeViewModel> TreeNodes { get; } = new();
        public BulkObservableCollection<VideoListItemViewModel> Videos { get; } = new();

        #region 表示モード切替(詳細リスト/タイル、旧WinForms版btnViewMode相当)

        // VirtualizingWrapPanel(NuGet: WpfToolkit.Controls)がコンテナ自体を仮想化するため、
        // 旧WinForms版のTileModeMaxItems(=500件キャップ)は不要。Videosをそのままタイル表示にも
        // 束縛でき、可視範囲のコンテナ生成時(DataContextChanged)にサムネ読み込みをトリガーする。
        [ObservableProperty]
        private bool _isTileMode;

        public bool IsListMode => !IsTileMode;

        public string ViewModeToggleText => IsTileMode ? L.T("MainForm_D161") : L.T("MainForm_D162");

        partial void OnIsTileModeChanged(bool value)
        {
            SettingsManager.Instance.Settings.VideoListViewMode = value ? 1 : 0;
            SettingsManager.Instance.Save();
            OnPropertyChanged(nameof(IsListMode));
            OnPropertyChanged(nameof(ViewModeToggleText));
        }

        private void OnThumbnailReady(object? sender, string videoId) => PostToUi(() =>
        {
            var item = Videos.FirstOrDefault(v => v.Video.VideoId == videoId);
            if (item == null) return;
            var bytes = ThumbnailCacheService.Instance.TryGetMemoryCachedBytes(videoId);
            if (bytes != null) item.ApplyThumbnailBytes(bytes);
        });

        #endregion

        [ObservableProperty]
        private ChannelTreeNodeViewModel? _selectedTreeNode;

        [ObservableProperty]
        private VideoListItemViewModel? _selectedVideo;

        [ObservableProperty]
        private string _urlInput = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FooterStatusText))]
        private string _statusMessage = "";

        /// <summary>ツールバー項目にマウスホバー/キーボードフォーカスした時の説明文(空なら非表示)。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FooterStatusText))]
        private string _hoverHintText = "";

        /// <summary>
        /// ステータスバー左側の実際の表示テキスト。HoverHintTextが設定されている間は
        /// それを優先表示し(ツールバー項目の説明)、無ければ通常のStatusMessageを表示する。
        /// </summary>
        public string FooterStatusText => string.IsNullOrEmpty(HoverHintText) ? StatusMessage : HoverHintText;

        [ObservableProperty]
        private string _freeSpaceText = "";

        [ObservableProperty]
        private Brush _freeSpaceForeground = ThemeManager.GetBrush("Brush.TextSecondary");

        [ObservableProperty]
        private string _downloadCountText = "";

        [ObservableProperty]
        private int _progressBarValue;

        private readonly DispatcherTimer _freeSpaceTimer;

        public MainViewModel(DownloadManager downloadManager)
        {
            _downloadManager = downloadManager;
            _webServer = new WebServerService();
            _webServer.SetDownloadManager(_downloadManager);
            WebServerServiceHolder.Instance = _webServer;

            RefreshTree();
            RefreshFreeSpace();
            RefreshDownloadCount();

            // 旧WinForms版StartFreeSpaceMonitorに対応(1分間隔)。
            _freeSpaceTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _freeSpaceTimer.Tick += (_, _) => RefreshFreeSpace();
            _freeSpaceTimer.Start();

            // Phase7: DownloadManagerのイベントをUIスレッドへ結線(旧WinForms版PostToUiパターンに対応)。
            _downloadManager.TaskProgressChanged += OnTaskProgressChanged;
            _downloadManager.TaskStatusChanged += OnTaskStatusChanged;
            _downloadManager.NewVideosFound += OnNewVideosFound;
            _downloadManager.AutoCheckCompleted += OnAutoCheckCompleted;
            _downloadManager.BackgroundTaskProgress += OnBackgroundTaskProgress;
            _downloadManager.BackgroundTaskCompleted += OnBackgroundTaskCompleted;
            _downloadManager.UserAddStatusChanged += (_, msg) => PostToUi(() => StatusMessage = msg);
            _downloadManager.UserAdded += (_, _) => PostToUi(ScheduleTreeRefresh);
            _downloadManager.DownloadQueueSuspended += (_, count) => PostToUi(() => StatusMessage = L.T("MainForm_D004", count));
            _downloadManager.DownloadQueueSuspendedForDiskSpace += (_, count) => PostToUi(() => StatusMessage = L.T("MainForm_QueueSuspendedDiskSpace", count));
            _downloadManager.DownloadQueueResumedForDiskSpace += (_, _) => PostToUi(() => StatusMessage = L.T("MainForm_QueueResumedDiskSpace"));
            ThumbnailCacheService.Instance.ThumbnailReady += OnThumbnailReady;

            IsTileMode = SettingsManager.Instance.Settings.VideoListViewMode == 1;

            Current = this;

            InitializeLifecycle();
        }

        /// <summary>
        /// Phase8c: 唯一存在するMainViewModelインスタンスへの参照。旧WinForms版の
        /// Application.OpenForms経由でMainFormを探すパターン(ImportFromFolderWizard等の
        /// ブリッジダイアログからの通知用)をWPF側で置き換えるためのホルダー。
        /// </summary>
        public static MainViewModel? Current { get; private set; }

        /// <summary>
        /// 外部 (ImportFromFolderWizard 等のブリッジダイアログ) からインポート完了通知を受けたときに
        /// チャンネル一覧 + 動画リストの両方を更新するフック。旧WinForms版MainForm.RefreshAfterImportに対応。
        /// </summary>
        public void RefreshAfterImport()
        {
            PostToUi(() =>
            {
                RefreshTree();
                LoadVideos();
            });
        }

        /// <summary>
        /// 旧WinForms版MainForm_Load/MainForm_Shownに対応するアプリ起動処理。
        /// DL再開(DownloadManager.Start/ResumeIncompleteDownloads)は環境セットアップ済みならここで
        /// 即座に、未セットアップならShowSetupWizardの成功分岐まで遅延させる(キャンセル時は呼ばない)。
        /// </summary>
        private void InitializeLifecycle()
        {
            var settings = SettingsManager.Instance.Settings;

            var recoveryMessage = FileMoveJournal.RecoverIfNeeded(_database);

            RefreshEnvironmentAndLoginStatus();

            if (recoveryMessage != null) StatusMessage = recoveryMessage;

            if (settings.WebServerAutoStart)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webServer.StartAsync(settings.WebServerPort, settings.WebServerBindAll);
                        LoggingService.Instance.Info($"Web media server auto-started on port {settings.WebServerPort}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Error("Web media server auto-start failed", ex);
                    }
                });
            }

            // 環境未セットアップの間はDownloadManagerを起動しない(無駄な失敗試行・リトライを防ぐ)。
            // DL開始はShowSetupWizardの成功分岐に寄せてあるので、キャンセル時はここでは起動しない。
            // WPFウィンドウの初回描画後に呼びたいのでDispatcherへ回す。
            if (IsSetupNeeded)
            {
                _dispatcher.BeginInvoke(new Action(() => ShowSetupWizard(autoTriggered: true)));
            }
            else
            {
                _downloadManager.Start();
                _downloadManager.ResumeIncompleteDownloads();
            }

            if (settings.CheckUpdateOnStartup)
            {
                _ = CheckForUpdatesOnStartupAsync();
            }
        }

        #region 環境チェック/ログイン状態/セットアップウィザード/アップデートチェック (Phase8b)

        [ObservableProperty] private bool _isSetupNeeded;
        [ObservableProperty] private string _loginStatusText = "";
        [ObservableProperty] private Brush _loginStatusBrush = ThemeManager.GetBrush("Brush.TextSecondary");

        /// <summary>
        /// 環境チェック+ログイン状態表示の更新。旧WinForms版CheckEnvironment/UpdateLoginStatusに対応。
        /// ログインアクション自体はSettingsForm(ブリッジ)側のログインUIに集約する方針とし、
        /// ここでは状態表示のみを持つ(WPF側にメール/パスワード入力ダイアログを新規に作らない判断、
        /// 2026-08-15)。
        /// </summary>
        private void RefreshEnvironmentAndLoginStatus()
        {
            var (pythonReady, scriptReady) = _downloadManager.CheckEnvironment();
            bool setupComplete = pythonReady && scriptReady;
            IsSetupNeeded = !setupComplete;

            if (!setupComplete)
            {
                StatusMessage = L.T("MainForm_D017");
            }
            else if (!_downloadManager.IsLoggedIn)
            {
                StatusMessage = L.T("MainForm_D018");
            }
            else
            {
                StatusMessage = L.T("MainForm_D019");
                _ = VerifyLoginInBackgroundAsync();
            }
            UpdateLoginStatusDisplay();
        }

        private void UpdateLoginStatusDisplay()
        {
            if (_downloadManager.IsLoggedIn)
            {
                LoginStatusText = L.T("MainForm_D022");
                LoginStatusBrush = ThemeManager.GetBrush("Brush.Success");
            }
            else
            {
                LoginStatusText = L.T("MainForm_D024");
                LoginStatusBrush = ThemeManager.GetBrush("Brush.TextSecondary");
            }
        }

        private async Task VerifyLoginInBackgroundAsync()
        {
            try
            {
                var (valid, error) = await _downloadManager.VerifyTokenAsync();
                if (!valid)
                {
                    PostToUi(() =>
                    {
                        UpdateLoginStatusDisplay();
                        StatusMessage = L.T("MainForm_D020", error ?? L.T("Common_Unknown"));
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VerifyToken error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenSetupWizard() => ShowSetupWizard();

        private void ShowSetupWizard(bool autoTriggered = false)
        {
            using var wiz = new SetupWizardForm();
            var result = wiz.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D013"), L.T("MainForm_D014"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                // Start/ResumeIncompleteDownloadsは冪等(稼働中なら何もしない/重複投入しない)なので、
                // 初回起動と手動再セットアップの両経路からここを呼んでも安全。
                _downloadManager.Start();
                _downloadManager.ResumeIncompleteDownloads();
            }
            else if (autoTriggered)
            {
                StatusMessage = L.T("MainForm_D015");
            }
            RefreshEnvironmentAndLoginStatus();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                await Task.Delay(3000);
                var result = await UpdateService.CheckForUpdateAsync();
                if (result.HasUpdate)
                {
                    var dialogResult = System.Windows.MessageBox.Show(
                        L.T("MainForm_D128") + L.T("MainForm_D129", UpdateService.CurrentVersionString) +
                        L.T("MainForm_D130", result.LatestVersion) + L.T("MainForm_D131"),
                        L.T("MainForm_D132"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);
                    if (dialogResult == System.Windows.MessageBoxResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新チェック失敗: {ex.Message}");
            }
        }

        #endregion

        #region クリップボード監視 (Phase8b)
        // Win32フック(AddClipboardFormatListener/WM_CLIPBOARDUPDATE)自体はウィンドウハンドルが要るため
        // MainWindow.xaml.cs側が持つ。ここではON/OFF状態とURL検知後の処理(旧WinForms版
        // OnClipboardChangedに対応)のみを持ち、フック登録要求はイベントで橋渡しする。

        [ObservableProperty] private bool _clipboardMonitorEnabled = SettingsManager.Instance.Settings.EnableClipboardMonitor;
        private string? _lastProcessedClipboardText;

        public string ClipboardMonitorToggleText => ClipboardMonitorEnabled ? L.T("MainForm_D155") : L.T("MainForm_D156");

        /// <summary>MainWindow.xaml.csがAddClipboardFormatListener/RemoveClipboardFormatListenerを呼ぶためのフック。</summary>
        public event Action<bool>? ClipboardMonitorToggled;

        partial void OnClipboardMonitorEnabledChanged(bool value)
        {
            SettingsManager.Instance.Settings.EnableClipboardMonitor = value;
            SettingsManager.Instance.Save();
            OnPropertyChanged(nameof(ClipboardMonitorToggleText));
            ClipboardMonitorToggled?.Invoke(value);
        }

        /// <summary>MainWindow.xaml.csのWM_CLIPBOARDUPDATEフックから呼ぶ。旧WinForms版OnClipboardChangedに対応。</summary>
        public async void OnClipboardChanged()
        {
            try
            {
                if (!ClipboardMonitorEnabled) return;

                string text;
                try
                {
                    if (!System.Windows.Clipboard.ContainsText()) return;
                    text = System.Windows.Clipboard.GetText() ?? "";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clipboard read failed: {ex.Message}");
                    return;
                }

                text = text.Trim();
                if (string.IsNullOrEmpty(text)) return;
                if (text == _lastProcessedClipboardText) return;

                bool isVideo = Helpers.IsVideoUrl(text);
                bool isUser = Helpers.IsUserProfileUrl(text);
                if (!isVideo && !isUser) return;

                _lastProcessedClipboardText = text;

                if (isVideo)
                {
                    var vid = Helpers.ExtractVideoIdFromUrl(text);
                    if (!string.IsNullOrEmpty(vid) && _database.GetVideoByVideoId(vid) != null)
                    {
                        StatusMessage = L.T("MainForm_D159", vid);
                        return;
                    }
                    StatusMessage = L.T("MainForm_D160");
                    await AddVideoAsync(text);
                }
                else
                {
                    _downloadManager.EnqueueSubscribedUser(text);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("OnClipboardChanged で予期せぬ例外", ex);
            }
        }

        #endregion

        /// <summary>
        /// 終了処理に時間がかかりそうか(旧WinForms版FormClosingのslowClose判定に対応)。
        /// trueならMainWindow側でトレイバルーンを出してから閉じる。
        /// </summary>
        public bool IsSlowCloseExpected =>
            _downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0
            || _downloadManager.PendingTaskCount > 0 || MetadataService.WritesInProgress > 0;

        /// <summary>
        /// アプリ終了処理。旧WinForms版MainForm_FormClosing(トレイ最小化ではなく実際に閉じる経路)に対応。
        /// DL停止→Webサーバー停止→mp4タグ書き込み完了待ち→DownloadManager破棄の順で行う
        /// (moov atom破損防止のため書き込み完了待ちを挟む)。
        /// </summary>
        public void Dispose()
        {
            if (Current == this) Current = null;

            _freeSpaceTimer.Stop();
            _treeRefreshTimer?.Stop();
            _videoListRefreshTimer?.Stop();
            _downloadCountTimer?.Stop();

            _downloadManager.TaskProgressChanged -= OnTaskProgressChanged;
            _downloadManager.TaskStatusChanged -= OnTaskStatusChanged;
            _downloadManager.NewVideosFound -= OnNewVideosFound;
            _downloadManager.AutoCheckCompleted -= OnAutoCheckCompleted;
            _downloadManager.BackgroundTaskProgress -= OnBackgroundTaskProgress;
            _downloadManager.BackgroundTaskCompleted -= OnBackgroundTaskCompleted;
            ThumbnailCacheService.Instance.ThumbnailReady -= OnThumbnailReady;

            _downloadManager.Stop();
            try { _webServer.StopAsync().Wait(5000); } catch { }
            _webServer.Dispose();
            MetadataService.WaitForWritesToComplete(10000);
            _downloadManager.Dispose();
        }

        /// <summary>
        /// DownloadManagerイベント(バックグラウンドスレッド発火)をUIスレッドへ橋渡しする。
        /// 旧WinForms版PostToUi(InvokeRequired + BeginInvoke)に相当。
        /// </summary>
        private void PostToUi(Action action)
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
            if (_dispatcher.CheckAccess()) action();
            else _dispatcher.BeginInvoke(action);
        }

        private void OnTaskProgressChanged(object? sender, DownloadTask task) => PostToUi(() =>
        {
            UpdateVideoItem(task);
            ScheduleDownloadCountRefresh();
        });

        private void OnTaskStatusChanged(object? sender, DownloadTask task) => PostToUi(() =>
        {
            UpdateVideoItem(task);
            ScheduleDownloadCountRefresh();
            // どのステータス遷移でもキューノード内訳が変わるのでツリーもRefresh(debounce経由)。
            ScheduleTreeRefresh();
        });

        private void OnNewVideosFound(object? sender, (SubscribedUser User, List<VideoInfo> Videos) e) => PostToUi(() =>
        {
            ScheduleTreeRefresh();
            ScheduleVideoListRefresh();
        });

        private void OnAutoCheckCompleted(object? sender, EventArgs e) => PostToUi(ScheduleTreeRefresh);

        /// <summary>バックグラウンドタスクの進捗をステータスバーに表示</summary>
        private void OnBackgroundTaskProgress(object? sender, (string TaskName, string Message) e) => PostToUi(() =>
        {
            StatusMessage = $"[{e.TaskName}] {e.Message}";
        });

        /// <summary>バックグラウンドタスク完了時の通知</summary>
        private void OnBackgroundTaskCompleted(object? sender, (string TaskName, string Summary, bool Success) e) => PostToUi(() =>
        {
            StatusMessage = $"[{e.TaskName}] {e.Summary}";
            try
            {
                NotificationService.Instance.ShowNotification(
                    e.Success ? L.T("MainForm_D175", e.TaskName) : L.T("MainForm_D176", e.TaskName),
                    e.Summary);
            }
            catch { }
            // 動画リスト/ツリーを更新 (サムネ補完で URL が増えたら再描画したい)
            ScheduleTreeRefresh();
            ScheduleVideoListRefresh();
        });

        /// <summary>DownloadManagerイベントで更新された1件だけをVideosコレクション内で差し替える(全件再読込を避ける)</summary>
        private void UpdateVideoItem(DownloadTask task)
        {
            var item = Videos.FirstOrDefault(v => v.Video.VideoId == task.Video.VideoId);
            item?.ApplyTaskUpdate(task);
        }

        /// <summary>短時間に複数回呼ばれてもRefreshTree()は1回だけ実行される(旧WinForms版と同じdebounce方式)</summary>
        private void ScheduleTreeRefresh()
        {
            _treeRefreshTimer ??= CreateDebounceTimer(TreeRefreshDebounceMs, RefreshTree);
            _treeRefreshTimer.Stop();
            _treeRefreshTimer.Start();
        }

        /// <summary>
        /// 動画の右クリックメニューが開いている間かどうか。開いている間にLoadVideos()
        /// (Videos.ReplaceAll、単発Resetイベント)が走ると、WPFのSelector標準動作で
        /// SelectedItem(=SelectedVideo)がnullにリセットされ、メニュー項目のコマンドが
        /// 対象を失って何も起きなくなる(メニュー自体は開いたまま反応しないように見える)。
        /// そのため開いている間はLoadVideosを保留し、閉じた瞬間にまとめて反映する。
        /// </summary>
        public bool IsVideoContextMenuOpen { get; set; }
        private bool _videoListRefreshPending;

        /// <summary>短時間に複数回呼ばれてもLoadVideos()は1回だけ実行される(旧WinForms版RefreshVideoListに相当)</summary>
        private void ScheduleVideoListRefresh()
        {
            _videoListRefreshTimer ??= CreateDebounceTimer(VideoListRefreshDebounceMs, RunScheduledVideoListRefresh);
            _videoListRefreshTimer.Stop();
            _videoListRefreshTimer.Start();
        }

        private void RunScheduledVideoListRefresh()
        {
            if (IsVideoContextMenuOpen)
            {
                _videoListRefreshPending = true;
                return;
            }
            LoadVideos();
        }

        /// <summary>右クリックメニューが閉じた直後に呼ぶ。開いている間に保留していた更新があれば反映する。</summary>
        public void FlushPendingVideoListRefresh()
        {
            if (!_videoListRefreshPending) return;
            _videoListRefreshPending = false;
            LoadVideos();
        }

        private void ScheduleDownloadCountRefresh()
        {
            _downloadCountTimer ??= CreateDebounceTimer(DownloadCountDebounceMs, RefreshDownloadCount);
            _downloadCountTimer.Stop();
            _downloadCountTimer.Start();
        }

        private static DispatcherTimer CreateDebounceTimer(int intervalMs, Action onTick)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
            timer.Tick += (sender, _) =>
            {
                ((DispatcherTimer)sender!).Stop();
                onTick();
            };
            return timer;
        }

        /// <summary>DL先ドライブの空き容量をステータスバー用に更新する</summary>
        private void RefreshFreeSpace()
        {
            try
            {
                var settings = SettingsManager.Instance.Settings;
                var root = Path.GetPathRoot(Path.GetFullPath(settings.DownloadFolder));
                if (string.IsNullOrEmpty(root)) return;

                var free = new DriveInfo(root).AvailableFreeSpace;
                var text = L.T("MainForm_FreeSpace", FileMoveHelper.FormatSize(free), root.TrimEnd('\\'));
                var isLow = settings.MinFreeSpaceGb > 0 && free < settings.MinFreeSpaceGb * 1024L * 1024 * 1024;
                if (isLow) text += " ⚠";

                FreeSpaceText = text;
                FreeSpaceForeground = isLow ? ThemeManager.GetBrush("Brush.Danger") : ThemeManager.GetBrush("Brush.TextSecondary");
            }
            catch { /* フォルダ未作成・ドライブ情報取得不可の場合は非表示のまま */ }
        }

        /// <summary>DL中/待機中/完了件数とキュー進捗をステータスバー用に更新する</summary>
        private void RefreshDownloadCount()
        {
            var (downloading, pending, completed, totalSize) = _database.GetDownloadCountSummary();
            var totalSizeStr = FileMoveHelper.FormatSize(totalSize);

            var activeTasks = _downloadManager.GetActiveTasks();
            var dlTasks = activeTasks.Where(t => t.Status == DownloadStatus.Downloading).ToList();
            int progressValue = 0;
            string queueText = "";

            if (dlTasks.Count > 0)
            {
                double avgInProgress = dlTasks.Average(t => t.Progress);
                progressValue = Math.Clamp((int)avgInProgress, 0, 100);
                // 件数(DL中/待機)は前半の DL:/待機: と重複するため、ここでは進捗の平均%だけ足す
                queueText = L.T("MainForm_QueueSummaryFull", avgInProgress.ToString("F0"));
            }

            DownloadCountText = L.T("MainForm_D127", downloading, pending, completed, totalSizeStr, queueText);
            ProgressBarValue = progressValue;
        }

        [RelayCommand]
        private void CheckNow() => _downloadManager.EnqueueAllUsersForCheck();

        [RelayCommand]
        private void StartAll()
        {
            _downloadManager.Start();
            StatusMessage = L.T("MainForm_D044");
        }

        [RelayCommand]
        private void StopAll()
        {
            _downloadManager.CancelAllTasks();
            StatusMessage = L.T("MainForm_D045");
            RefreshTree();
            LoadVideos();
            RefreshDownloadCount();
        }

        /// <summary>
        /// URL入力行の「貼り付けて追加」相当。旧WinForms版ProcessUrlInput/AddVideoAsyncに対応する
        /// 分岐(動画URL/プロフィールURL/ユーザー名/不正入力)を再現する。
        /// </summary>
        [RelayCommand]
        private async Task AddFromUrlAsync()
        {
            var input = UrlInput.Trim();
            if (string.IsNullOrEmpty(input)) return;
            UrlInput = "";

            var isUrl = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (Helpers.IsVideoUrl(input))
            {
                await AddVideoAsync(input);
            }
            else if (Helpers.IsUserProfileUrl(input))
            {
                _downloadManager.EnqueueSubscribedUser(input);
                RefreshTree();
            }
            else if (isUrl)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D046"), L.T("MainForm_D040"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else if (!Helpers.IsValidUsername(input))
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D041"), L.T("MainForm_D042"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else
            {
                _downloadManager.EnqueueSubscribedUser(input);
                RefreshTree();
            }
        }

        private async Task AddVideoAsync(string url)
        {
            StatusMessage = L.T("MainForm_D047");
            try
            {
                var videoId = Helpers.ExtractVideoIdFromUrl(url);
                if (!string.IsNullOrEmpty(videoId))
                {
                    var existingVideo = _database.GetVideoByVideoId(videoId);
                    if (existingVideo != null)
                    {
                        var statusText = VideoListItemViewModel.GetStatusText(existingVideo.Status);
                        var result = System.Windows.MessageBox.Show(
                            L.T("MainForm_D048") + L.T("MainForm_D049", existingVideo.Title) +
                            L.T("MainForm_D050", statusText) + L.T("MainForm_D051"),
                            L.T("MainForm_D052"),
                            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            SubscribedUser? user = existingVideo.SubscribedUserId.HasValue
                                ? _database.GetSubscribedUserById(existingVideo.SubscribedUserId.Value)
                                : null;
                            _downloadManager.EnqueueDownload(existingVideo, existingVideo.SubscribedUserId.HasValue, user);
                            RefreshTree();
                            LoadVideos();
                            StatusMessage = L.T("MainForm_D053", existingVideo.Title);
                        }
                        else
                        {
                            StatusMessage = L.T("MainForm_D054");
                        }
                        return;
                    }
                }

                var progress = new Progress<string>(msg => StatusMessage = msg);
                var addedVideo = await _downloadManager.AddSingleVideoAsync(url, progress);

                if (addedVideo != null)
                {
                    RefreshTree();
                    LoadVideos();
                    var statusKey = SettingsManager.Instance.Settings.ImmediateDownloadOnAdd
                        ? "MainForm_D055" : "MainForm_D188";
                    StatusMessage = L.T(statusKey, addedVideo.Title);
                }
                else
                {
                    System.Windows.MessageBox.Show(L.T("MainForm_D056"), L.T("MainForm_D029"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    StatusMessage = L.T("MainForm_D057");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D058", ex.Message), L.T("MainForm_D029"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusMessage = L.T("MainForm_D029");
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            using var form = new SettingsForm(_downloadManager);
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _downloadManager.UpdateAutoCheckTimer();
                _downloadManager.NotifyConcurrentLimitChanged();
                IsAdvancedDbToolEnabled = SettingsManager.Instance.Settings.EnableAdvancedDbTool;
                LoadVideos();
            }
        }

        /// <summary>DB操作ツール(SQLエディタ/テーブルブラウザ)をツールメニューに表示するか。設定画面のトグルに連動。</summary>
        [ObservableProperty]
        private bool _isAdvancedDbToolEnabled = SettingsManager.Instance.Settings.EnableAdvancedDbTool;

        [RelayCommand]
        private void OpenDatabaseTool()
        {
            // downloader起動中のままDBを直接いじると裏のDL処理と競合しうるため、
            // 別プロセスの独立ツール(DBMaintenanceTool.exe、プロジェクトはIwaraDownloader.DbTool)
            // として切り出している。本体からは起動するだけで、DownloadManagerとの連携は持たない
            // (DbTool側で起動時に本体プロセスの有無を確認・警告する設計、詳細はDbTool側Program.cs参照)。
            var exePath = System.IO.Path.Combine(AppContext.BaseDirectory, "DBMaintenanceTool.exe");
            if (!File.Exists(exePath))
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D200"), L.T("MainForm_D201"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D202", ex.Message), L.T("MainForm_D201"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // 以下、Phase6でWPF版に置き換えるまでの間、既存WinFormsダイアログをそのまま起動するブリッジ。
        // WinForms/WPFはstrangler-fig方式で共存させる方針(memory: project_iwara_downloader_wpf_migration)。

        [RelayCommand]
        private void OpenAbout() => new Wpf.Views.AboutWindow().ShowDialog();

        [RelayCommand]
        private void OpenBulkImport()
        {
            using var form = new BulkImportForm(_downloadManager);
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                RefreshTree();
                LoadVideos();
            }
        }

        [RelayCommand]
        private void OpenSearchImport()
        {
            using var form = new SearchImportForm(_downloadManager);
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                RefreshTree();
                LoadVideos();
            }
        }

        [RelayCommand]
        private void OpenImportFromFolder() => ImportFromFolderWizard.ShowOrActivate(null, _downloadManager);

        [RelayCommand]
        private void OpenDuplicateCheck()
        {
            using var form = new DuplicateCheckForm();
            form.ShowDialog();
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void OpenStatistics()
        {
            using var form = new StatisticsForm();
            form.ShowDialog();
        }

        #region ツールバートグル+ツール/ヘルプメニュー (Phase8a-3でパリティ閉じ)
        // クリップボード監視トグル(WM_CLIPBOARDUPDATEフック必須)とタイル表示モード切替
        // (サムネグリッドUI自体が未実装)はPhase8b/将来フェーズへ持ち越し。

        [ObservableProperty]
        private bool _immediateDownloadOnAdd = SettingsManager.Instance.Settings.ImmediateDownloadOnAdd;

        public string ImmediateDownloadToggleText => ImmediateDownloadOnAdd ? L.T("MainForm_D189") : L.T("MainForm_D190");

        partial void OnImmediateDownloadOnAddChanged(bool value)
        {
            SettingsManager.Instance.Settings.ImmediateDownloadOnAdd = value;
            SettingsManager.Instance.Save();
            OnPropertyChanged(nameof(ImmediateDownloadToggleText));
        }

        [RelayCommand]
        private void OpenLogFolder()
        {
            var logFolder = LoggingService.Instance.LogDirectory;
            if (Directory.Exists(logFolder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logFolder,
                    UseShellExecute = true,
                });
            }
            else
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D133"), L.T("MainForm_D090"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void RelocateFiles()
        {
            if (_downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D134") + L.T("MainForm_D135"), L.T("MainForm_D136"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var downloadFolder = SettingsManager.Instance.Settings.DownloadFolder;
            var plan = FileMoveHelper.BuildRelocationPlan(_database.GetAllVideos(), _database.GetAllSubscribedUsers(), downloadFolder);
            if (plan.Count == 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D137"), L.T("MainForm_D136"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            long totalBytes = 0;
            foreach (var (video, _) in plan)
            {
                try { totalBytes += new FileInfo(video.LocalFilePath).Length; } catch { }
            }
            var spaceSummary = FileMoveHelper.BuildDriveSpaceSummary(plan, out bool insufficient);
            var spaceLines = string.IsNullOrEmpty(spaceSummary) ? "" : "\n\n" + spaceSummary;
            var warnLine = insufficient ? L.T("MainForm_LowSpaceWarn") : "";

            var confirm = System.Windows.MessageBox.Show(
                L.T("MainForm_D138", plan.Count) + $" ({FileMoveHelper.FormatSize(totalBytes)})。\n" +
                L.T("MainForm_D139") + L.T("MainForm_D140") + spaceLines + warnLine,
                L.T("MainForm_D136"), System.Windows.MessageBoxButton.YesNo,
                insufficient ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Question);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            if (_downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D141") + L.T("MainForm_D135"), L.T("MainForm_D136"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var oldDirs = plan.Select(p => Path.GetDirectoryName(p.Video.LocalFilePath))
                .Where(d => !string.IsNullOrEmpty(d)).Select(d => d!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var newDirs = plan.Select(p => Path.GetDirectoryName(p.NewPath))
                .Where(d => !string.IsNullOrEmpty(d)).Select(d => d!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using var progressForm = new FileMoveProgressForm(plan, _database);
            progressForm.ShowDialog(GetOwnerWin32Window());

            foreach (var dir in oldDirs)
            {
                IndexCacheService.Invalidate(dir);
                FileMoveHelper.CleanupEmptyDirectories(dir);
                FileMoveHelper.TryDeleteDirectoryIfEmpty(dir);
            }
            foreach (var dir in newDirs) IndexCacheService.Invalidate(dir);
            RefreshTree();
            LoadVideos();

            StatusMessage = L.T("MainForm_D142", progressForm.MovedCount, progressForm.FailedCount);
            if (progressForm.FailedCount > 0)
            {
                System.Windows.MessageBox.Show(
                    L.T("MainForm_D143", progressForm.FailedCount) + L.T("MainForm_D098") + L.T("MainForm_D144"),
                    L.T("MainForm_D136"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task RelinkFiles()
        {
            if (_downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D134") + L.T("MainForm_D135"), L.T("MainForm_D145"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var downloadFolder = SettingsManager.Instance.Settings.DownloadFolder;
            var videos = _database.GetAllVideos();
            var users = _database.GetAllSubscribedUsers();

            StatusMessage = L.T("MainForm_D146");
            var result = await Task.Run(() => FileMoveHelper.BuildRelinkPlan(videos, users, downloadFolder));

            var notes = new List<string>();
            if (result.NotMovedCount > 0) notes.Add(L.T("MainForm_RelinkNotMoved", result.NotMovedCount));
            if (result.UnverifiedCount > 0) notes.Add(L.T("MainForm_RelinkUnverified", result.UnverifiedCount));
            if (result.MissingCount > 0) notes.Add(L.T("MainForm_RelinkMissing", result.MissingCount));
            var notesText = notes.Count > 0 ? "\n\n" + string.Join("\n", notes) : "";

            if (result.Items.Count == 0)
            {
                StatusMessage = L.T("MainForm_D147");
                var hint = (result.NotMovedCount + result.MissingCount + result.UnverifiedCount) > 0 ? L.T("MainForm_RelinkHint") : "";
                System.Windows.MessageBox.Show(L.T("MainForm_D148") + notesText + hint, L.T("MainForm_D145"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            int copiedCount = result.Items.Count(i => i.OldFileStillExists);
            var copiedLine = copiedCount > 0 ? L.T("MainForm_RelinkCopied", copiedCount) : "";

            var confirm = System.Windows.MessageBox.Show(
                L.T("MainForm_D149", result.Items.Count) + L.T("MainForm_D150") + L.T("MainForm_D151") + copiedLine + notesText,
                L.T("MainForm_D145"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            if (_downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D152") + L.T("MainForm_D135"), L.T("MainForm_D145"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int relinked = 0;
            await Task.Run(() =>
            {
                foreach (var (video, newPath, _) in result.Items)
                {
                    var oldPath = video.LocalFilePath;
                    video.LocalFilePath = newPath;
                    _database.UpdateVideo(video);
                    relinked++;

                    try
                    {
                        var newJson = Path.ChangeExtension(newPath, ".json");
                        var oldJson = Path.ChangeExtension(oldPath, ".json");
                        if (!File.Exists(newJson) && File.Exists(oldJson)) File.Move(oldJson, newJson);
                    }
                    catch { }
                }
            });

            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D153", relinked);
        }

        #endregion

        #region 動画コンテキストメニュー (Phase4f, Phase8a-1/8a-5でパリティ閉じ)

        /// <summary>MainWindow.xaml.csのSelectionChangedから設定される、動画一覧の複数選択スナップショット。</summary>
        public List<VideoListItemViewModel> SelectedVideos { get; set; } = new();

        /// <summary>
        /// 選択中の動画(複数可)をVideoInfoのリストで返す。旧WinForms版GetSelectedVideosに相当。
        /// SelectedVideosが空でもSelectedVideoがあればそれ1件を返す(単一クリックのみでSelectionChangedが
        /// 発火しない経路への保険)。
        /// </summary>
        private List<VideoInfo> GetSelectedVideoInfos()
        {
            if (SelectedVideos.Count > 0) return SelectedVideos.Select(v => v.Video).ToList();
            return SelectedVideo != null ? new List<VideoInfo> { SelectedVideo.Video } : new List<VideoInfo>();
        }

        [ObservableProperty] private bool _isExcludedNodeSelected;
        [ObservableProperty] private bool _isNormalNodeSelected = true;
        [ObservableProperty] private bool _isSingleVideoSelected;
        [ObservableProperty] private bool _canDownloadSelectedVideo;
        [ObservableProperty] private string _downloadMenuText = "";
        [ObservableProperty] private bool _canCancelSelectedVideo;
        [ObservableProperty] private bool _canRetryFailedSelectedVideo;
        [ObservableProperty] private bool _canReDownloadSelectedVideo;
        [ObservableProperty] private bool _canRefreshInfoSelectedVideo;
        [ObservableProperty] private bool _canCheckFileExistsSelectedVideo;
        [ObservableProperty] private bool _canMapLocalFileSelectedVideo;
        [ObservableProperty] private bool _canOpenAuthorSelectedVideo;
        [ObservableProperty] private bool _canPlaySelectedVideo;
        [ObservableProperty] private bool _canOpenFolderSelectedVideo;
        [ObservableProperty] private bool _canOpenPageSelectedVideo;
        [ObservableProperty] private bool _canShowVideoDetails;
        [ObservableProperty] private string _favoriteMenuText = "";
        [ObservableProperty] private bool _canChangeVideoPriority;
        [ObservableProperty] private bool _priorityHighestChecked;
        [ObservableProperty] private bool _priorityHighChecked;
        [ObservableProperty] private bool _priorityNormalChecked;
        [ObservableProperty] private bool _priorityLowChecked;

        /// <summary>
        /// 動画コンテキストメニューを開く直前に呼ぶ。旧WinForms版menuVideoContext_Openingに相当
        /// (表示直前に選択中動画(複数可)のステータスから各項目のVisibleを再計算する方式を踏襲)。
        /// </summary>
        public void RefreshVideoContextMenuState()
        {
            IsExcludedNodeSelected = SelectedTreeNode?.Kind == TreeNodeKind.Excluded;
            IsNormalNodeSelected = !IsExcludedNodeSelected;

            var selected = GetSelectedVideoInfos();
            IsSingleVideoSelected = selected.Count == 1;
            if (selected.Count == 0 || IsExcludedNodeSelected)
            {
                // 除外(ゴミ箱)ノードでは通常の操作項目を全て隠す(旧WinForms版と同じ)
                CanDownloadSelectedVideo = false;
                CanCancelSelectedVideo = false;
                CanRetryFailedSelectedVideo = false;
                CanReDownloadSelectedVideo = false;
                CanRefreshInfoSelectedVideo = false;
                CanCheckFileExistsSelectedVideo = false;
                CanMapLocalFileSelectedVideo = false;
                CanOpenAuthorSelectedVideo = false;
                CanPlaySelectedVideo = false;
                CanOpenFolderSelectedVideo = false;
                CanChangeVideoPriority = false;
                PriorityHighestChecked = PriorityHighChecked = PriorityNormalChecked = PriorityLowChecked = false;
                return;
            }

            bool hasPending = selected.Any(v => v.Status == DownloadStatus.Pending);
            bool hasDownloading = selected.Any(v => v.Status == DownloadStatus.Downloading);
            bool hasCompleted = selected.Any(v => v.Status == DownloadStatus.Completed);
            bool hasFailed = selected.Any(v => v.Status == DownloadStatus.Failed);
            bool hasPaused = selected.Any(v => v.Status == DownloadStatus.Paused);

            CanDownloadSelectedVideo = selected.Any(v =>
                v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading
                && (v.Status != DownloadStatus.Pending || _downloadManager.GetTask(v.VideoId) == null));
            DownloadMenuText = hasFailed ? L.T("MainForm_D104") : L.T("MainForm_D105");
            CanCancelSelectedVideo = hasPending || hasDownloading || hasPaused;
            CanRetryFailedSelectedVideo = hasFailed;
            CanReDownloadSelectedVideo = hasCompleted;
            CanRefreshInfoSelectedVideo = selected.Any(v => string.IsNullOrEmpty(v.Title) || v.Title.StartsWith("Video "));
            CanCheckFileExistsSelectedVideo = hasCompleted;

            var single = IsSingleVideoSelected ? selected[0] : null;
            CanPlaySelectedVideo = single != null && single.Status == DownloadStatus.Completed && single.LocalFileExists;
            CanOpenFolderSelectedVideo = single != null && single.LocalFileExists;
            CanOpenPageSelectedVideo = single != null && !string.IsNullOrEmpty(single.Url);
            CanOpenAuthorSelectedVideo = single != null && !string.IsNullOrEmpty(single.AuthorUsername);
            CanMapLocalFileSelectedVideo = single != null
                && single.Status != DownloadStatus.Downloading && single.Status != DownloadStatus.WritingTags
                && (!single.LocalFileExists || single.Status == DownloadStatus.Failed || single.Status == DownloadStatus.Pending);
            CanShowVideoDetails = IsNormalNodeSelected && IsSingleVideoSelected;

            bool allFav = selected.All(v => v.IsFavorite);
            FavoriteMenuText = allFav ? L.T("MainForm_D106") : L.T("MainForm_D107");

            // 優先度は未DL(Pending)にのみ意味を持つ。チェックマークは選択中Pending全件の実効優先度
            // (手動設定 ?? 所属チャンネルの既定 ?? Normal) が一致する場合のみ点灯、バラバラなら消灯。
            // 選択件数分の個別DB問い合わせを避けるため、チャンネルは1回だけ一括取得する
            // (大量選択(数千件)で右クリックメニューを開くたびにUIスレッドが固まるのを防ぐため)。
            CanChangeVideoPriority = hasPending;
            if (hasPending)
            {
                var userMap = _database.GetAllSubscribedUsers().ToDictionary(u => u.Id);
                var resolved = selected.Where(v => v.Status == DownloadStatus.Pending)
                    .Select(v => v.Priority ?? (v.SubscribedUserId.HasValue && userMap.TryGetValue(v.SubscribedUserId.Value, out var u)
                        ? u.DefaultPriority
                        : null) ?? DownloadPriority.Normal)
                    .Distinct().ToList();
                var uniform = resolved.Count == 1 ? resolved[0] : (DownloadPriority?)null;
                PriorityHighestChecked = uniform == DownloadPriority.Highest;
                PriorityHighChecked = uniform == DownloadPriority.High;
                PriorityNormalChecked = uniform == DownloadPriority.Normal;
                PriorityLowChecked = uniform == DownloadPriority.Low;
            }
            else
            {
                PriorityHighestChecked = PriorityHighChecked = PriorityNormalChecked = PriorityLowChecked = false;
            }
        }

        [RelayCommand]
        private void PlayVideo()
        {
            var video = SelectedVideo?.Video;
            if (video != null && !string.IsNullOrEmpty(video.LocalFilePath) && System.IO.File.Exists(video.LocalFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = video.LocalFilePath,
                    UseShellExecute = true,
                });
            }
        }

        /// <summary>
        /// 動画一覧のダブルクリック用(旧WinForms版listViewVideos_MouseDoubleClickに対応)。
        /// DL済み+ローカルファイルが実在すればそれを既定アプリで再生、そうでなければ元動画ページを開く。
        /// </summary>
        [RelayCommand]
        private void PlayOrOpenSelectedVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;

            if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath) && System.IO.File.Exists(video.LocalFilePath))
            {
                PlayVideo();
            }
            else
            {
                Helpers.OpenUrl(video.Url);
            }
        }

        [RelayCommand]
        private void OpenVideoFolder()
        {
            var video = SelectedVideo?.Video;
            if (video == null || string.IsNullOrEmpty(video.LocalFilePath)) return;
            var folder = Path.GetDirectoryName(video.LocalFilePath);
            if (folder == null || !Directory.Exists(folder)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{video.LocalFilePath}\"",
                UseShellExecute = true,
            });
        }

        [RelayCommand]
        private void OpenVideoPage()
        {
            var video = SelectedVideo?.Video;
            if (video != null) Helpers.OpenUrl(video.Url);
        }

        [RelayCommand]
        private void CopyVideoUrl()
        {
            var urls = GetSelectedVideoInfos().Where(v => !string.IsNullOrEmpty(v.Url)).Select(v => v.Url).ToList();
            if (urls.Count == 0) return;
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, urls));
            StatusMessage = L.T("MainForm_D111", urls.Count);
        }

        [RelayCommand]
        private void CopyVideoTitle()
        {
            var titles = GetSelectedVideoInfos().Where(v => !string.IsNullOrEmpty(v.Title)).Select(v => v.Title).ToList();
            if (titles.Count == 0) return;
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, titles));
            StatusMessage = L.T("MainForm_D112", titles.Count);
        }

        /// <summary>
        /// 投稿者名をそのままコピーする。複数選択時も重複を除いて1行ずつに留める
        /// (同一動画一覧内で同じ投稿者が複数選ばれるケースが多いため)。
        /// </summary>
        [RelayCommand]
        private void CopyVideoAuthor()
        {
            var authors = GetSelectedVideoInfos()
                .Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                .Select(v => v.AuthorUsername)
                .Distinct()
                .ToList();
            if (authors.Count == 0) return;
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, authors));
            StatusMessage = L.T("MainForm_D193", authors.Count);
        }

        [RelayCommand]
        private void ToggleFavorite()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;
            bool allFav = videos.All(v => v.IsFavorite);
            foreach (var video in videos)
            {
                video.IsFavorite = !allFav;
                _database.UpdateVideo(video);
            }
            foreach (var item in Videos.Where(item => videos.Contains(item.Video)))
                item.Refresh(_downloadManager.GetTask(item.Video.VideoId));
            RefreshTree();
        }

        [RelayCommand]
        private void OpenVideoDetails()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;
            using var form = new VideoDetailsForm(video, _database, _downloadManager.IwaraApi, _downloadManager);
            form.ShowDialog();
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void DeleteVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;

            var message = videos.Count == 1
                ? L.T("MainForm_ConfirmDeleteOne", videos[0].Title)
                : L.T("MainForm_ConfirmDeleteMany", videos.Count);
            var result = System.Windows.MessageBox.Show(
                message + L.T("MainForm_D120"), L.T("MainForm_D103"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            var deletedCount = _downloadManager.ExcludeVideos(videos);
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D121", deletedCount);
        }

        [RelayCommand]
        private void RestoreVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;
            var restored = _downloadManager.RestoreExcludedVideos(videos.Select(v => v.VideoId));
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_RestoredStatus", restored);
        }

        /// <summary>
        /// 選択中の未DL(Pending)動画すべての優先度を一括変更する。DownloadingやCompleted等は
        /// ChangeVideoPriority側で自動的に除外される(優先度はキュー待ち順にしか意味がないため)。
        /// </summary>
        private void SetVideoPriority(DownloadPriority priority)
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;
            var changed = _downloadManager.ChangeVideoPriority(videos, priority);
            if (changed == 0) return;
            foreach (var item in Videos.Where(item => videos.Contains(item.Video)))
                item.Refresh(_downloadManager.GetTask(item.Video.VideoId));
            StatusMessage = L.T("MainForm_D191", changed);
        }

        [RelayCommand] private void SetVideoPriorityHighest() => SetVideoPriority(DownloadPriority.Highest);
        [RelayCommand] private void SetVideoPriorityHigh() => SetVideoPriority(DownloadPriority.High);
        [RelayCommand] private void SetVideoPriorityNormal() => SetVideoPriority(DownloadPriority.Normal);
        [RelayCommand] private void SetVideoPriorityLow() => SetVideoPriority(DownloadPriority.Low);

        [RelayCommand]
        private void DownloadVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;

            foreach (var video in videos)
            {
                bool isOrphanPending = video.Status == DownloadStatus.Pending && _downloadManager.GetTask(video.VideoId) == null;
                if (video.Status == DownloadStatus.Downloading || video.Status == DownloadStatus.Completed
                    || (video.Status == DownloadStatus.Pending && !isOrphanPending))
                    continue;

                if (video.Status == DownloadStatus.Failed)
                {
                    video.RetryCount = 0;
                    video.LastErrorMessage = null;
                    _database.UpdateVideo(video);
                }
                var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
                _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            }
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void CancelVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;
            foreach (var video in videos) _downloadManager.CancelTask(video.VideoId);
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void RetryFailedVideo()
        {
            var videos = GetSelectedVideoInfos().Where(v => v.Status == DownloadStatus.Failed).ToList();
            if (videos.Count == 0) return;

            foreach (var video in videos)
            {
                video.RetryCount = 0;
                video.LastErrorMessage = null;
                _database.UpdateVideo(video);
                var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
                _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            }
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D108", videos.Count);
        }

        [RelayCommand]
        private async Task RefreshVideoInfo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;

            var progress = new Progress<string>(msg => StatusMessage = msg);
            int refreshCount = 0;
            foreach (var video in videos)
            {
                if (!(string.IsNullOrEmpty(video.Title) || video.Title.StartsWith("Video "))) continue;
                if (await _downloadManager.RefreshVideoInfoAsync(video, progress)) refreshCount++;
            }
            LoadVideos();
            StatusMessage = L.T("MainForm_D110", refreshCount);
        }

        [RelayCommand]
        private async Task CheckFileExistsForSelectedVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;
            await CheckFilesExistenceAsync(videos, L.T("MainForm_NoDownloadedSelected"));
        }

        [RelayCommand]
        private async Task MapLocalFile()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;

            var result = await Utils.LocalFileMapHelper.MapAsync(GetOwnerWin32Window(), video, _downloadManager.IwaraApi, _database, _downloadManager);
            if (result != Utils.LocalFileMapHelper.MapResult.Mapped) return;

            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D187", video.Title);
        }

        [RelayCommand]
        private void OpenVideoAuthorPage()
        {
            var video = SelectedVideo?.Video;
            if (video == null || string.IsNullOrEmpty(video.AuthorUsername)) return;
            Helpers.OpenUrl($"https://www.iwara.tv/profile/{video.AuthorUsername}");
        }

        [RelayCommand]
        private void ReDownloadVideo()
        {
            var videos = GetSelectedVideoInfos().Where(v => v.Status == DownloadStatus.Completed).ToList();
            if (videos.Count == 0) return;

            var totalSize = videos.Sum(v => v.FileSize);
            var message = videos.Count == 1
                ? L.T("MainForm_ConfirmRedlOne", videos[0].Title)
                : L.T("MainForm_ConfirmRedlMany", videos.Count, FileMoveHelper.FormatSize(totalSize));
            var result = System.Windows.MessageBox.Show(message, L.T("MainForm_D122"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            int requeuedCount = 0;
            foreach (var video in videos)
            {
                if (!string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
                {
                    try { File.Delete(video.LocalFilePath); } catch { /* 削除失敗してもDBリセットは続行 */ }
                }
                if (!string.IsNullOrEmpty(video.LocalFilePath))
                {
                    var metaPath = Path.ChangeExtension(video.LocalFilePath, ".json");
                    if (File.Exists(metaPath))
                    {
                        try { File.Delete(metaPath); } catch { }
                    }
                    var dir = Path.GetDirectoryName(video.LocalFilePath);
                    if (!string.IsNullOrEmpty(dir)) IndexCacheService.Invalidate(dir);
                }

                video.LocalFilePath = string.Empty;
                video.FileSize = 0;
                video.Status = DownloadStatus.Pending;
                video.DownloadedAt = null;
                video.RetryCount = 0;
                video.LastErrorMessage = null;
                _database.UpdateVideo(video);

                var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
                _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                requeuedCount++;
            }

            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D123", requeuedCount);
        }

        [RelayCommand]
        private void PermanentDeleteVideo()
        {
            var videos = GetSelectedVideoInfos();
            if (videos.Count == 0) return;

            var message = videos.Count == 1
                ? L.T("MainForm_ConfirmPurgeOne", videos[0].Title)
                : L.T("MainForm_ConfirmPurgeMany", videos.Count);
            var result = System.Windows.MessageBox.Show(message, L.T("MainForm_D103"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            int deleted = _database.DeleteExcludedPermanent(videos.Select(v => v.VideoId));
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_PurgedStatus", deleted);
        }

        #endregion

        #region チャンネルコンテキストメニュー (Phase4f, Phase8a-2でパリティ閉じ)

        [ObservableProperty] private bool _isUserNodeSelected;
        [ObservableProperty] private bool _canEnableChannel;
        [ObservableProperty] private bool _canDisableChannel;
        [ObservableProperty] private bool _checkChannelNowEnabled;
        [ObservableProperty] private string _checkChannelNowText = "";
        [ObservableProperty] private bool _canDownloadAllChannel;
        [ObservableProperty] private bool _canCheckChannelFiles;
        [ObservableProperty] private bool _canDeleteNotFoundChannel;
        [ObservableProperty] private string _channelSavePathText = "";
        [ObservableProperty] private bool _channelExternalDLInheritChecked;
        [ObservableProperty] private bool _channelExternalDLOnChecked;
        [ObservableProperty] private bool _channelExternalDLOffChecked;
        [ObservableProperty] private string _channelExternalDLInheritText = "";
        [ObservableProperty] private bool _channelPriorityHighestChecked;
        [ObservableProperty] private bool _channelPriorityHighChecked;
        [ObservableProperty] private bool _channelPriorityNormalChecked;
        [ObservableProperty] private bool _channelPriorityLowChecked;

        /// <summary>
        /// チャンネルコンテキストメニューを開く直前に呼ぶ。旧WinForms版contextMenuChannel_Openingに相当。
        /// </summary>
        public void RefreshChannelContextMenuState()
        {
            var node = SelectedTreeNode;
            IsUserNodeSelected = node?.Kind == TreeNodeKind.Channel;
            CanCheckChannelFiles = IsUserNodeSelected || node?.Kind == TreeNodeKind.Downloaded;
            CanDownloadAllChannel = IsUserNodeSelected
                || node?.Kind is TreeNodeKind.NotDownloaded or TreeNodeKind.FailedVideos;
            CanDeleteNotFoundChannel = node?.Kind == TreeNodeKind.FailedVideos;

            var user = node?.Channel;
            if (user == null)
            {
                CanEnableChannel = false;
                CanDisableChannel = false;
                CheckChannelNowEnabled = false;
                ChannelPriorityHighestChecked = ChannelPriorityHighChecked =
                    ChannelPriorityNormalChecked = ChannelPriorityLowChecked = false;
                return;
            }

            CanEnableChannel = !user.IsEnabled;
            CanDisableChannel = user.IsEnabled;
            CheckChannelNowEnabled = user.IsEnabled;
            CheckChannelNowText = user.IsEnabled ? L.T("MainForm_D082") : L.T("MainForm_D083");
            ChannelSavePathText = string.IsNullOrEmpty(user.CustomSavePath) ? L.T("MainForm_D085") : L.T("MainForm_D086");
            ChannelExternalDLInheritChecked = !user.DownloadExternalVideosOverride.HasValue;
            ChannelExternalDLOnChecked = user.DownloadExternalVideosOverride == true;
            ChannelExternalDLOffChecked = user.DownloadExternalVideosOverride == false;
            var globalDefault = SettingsManager.Instance.Settings.DownloadExternalVideosDefault;
            ChannelExternalDLInheritText = L.T("MainForm_D084", globalDefault ? "ON" : "OFF");

            var channelPriority = user.DefaultPriority ?? DownloadPriority.Normal;
            ChannelPriorityHighestChecked = channelPriority == DownloadPriority.Highest;
            ChannelPriorityHighChecked = channelPriority == DownloadPriority.High;
            ChannelPriorityNormalChecked = channelPriority == DownloadPriority.Normal;
            ChannelPriorityLowChecked = channelPriority == DownloadPriority.Low;
        }

        [RelayCommand]
        private void EnableChannel()
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;
            user.IsEnabled = true;
            _database.UpdateSubscribedUser(user);
            RefreshTree();
        }

        [RelayCommand]
        private void DisableChannel()
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;
            user.IsEnabled = false;
            _database.UpdateSubscribedUser(user);
            RefreshTree();
        }

        [RelayCommand]
        private void CheckChannelNow()
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;
            _downloadManager.EnqueueUserForCheck(user, priority: true);
            StatusMessage = L.T("MainForm_D087", user.Username);
        }

        [RelayCommand]
        private void OpenChannelPage()
        {
            var user = SelectedTreeNode?.Channel;
            if (user != null) Helpers.OpenUrl(user.ProfileUrl);
        }

        [RelayCommand]
        private void DownloadAllChannel()
        {
            var node = SelectedTreeNode;
            List<VideoInfo> videos;

            if (node?.Channel is SubscribedUser user)
            {
                videos = _database.GetVideosBySubscribedUser(user.Id)
                    .Where(v => v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading
                             && (v.Status != DownloadStatus.Pending || _downloadManager.GetTask(v.VideoId) == null))
                    .ToList();
                foreach (var video in videos) _downloadManager.EnqueueDownload(video, true, user);
            }
            else if (node?.Kind == TreeNodeKind.NotDownloaded)
            {
                videos = _database.GetAllVideos()
                    .Where(v => v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading
                             && (v.Status != DownloadStatus.Pending || _downloadManager.GetTask(v.VideoId) == null))
                    .ToList();
                EnqueueEach(videos);
            }
            else if (node?.Kind == TreeNodeKind.FailedVideos)
            {
                videos = _database.GetVideosByStatus(DownloadStatus.Failed).ToList();
                foreach (var video in videos)
                {
                    video.RetryCount = 0;
                    video.LastErrorMessage = null;
                    _database.UpdateVideo(video);
                }
                EnqueueEach(videos);
            }
            else
            {
                return;
            }

            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D088", videos.Count);

            void EnqueueEach(List<VideoInfo> list)
            {
                foreach (var video in list)
                {
                    var videoUser = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
                    _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, videoUser);
                }
            }
        }

        [RelayCommand]
        private async Task CheckChannelFiles()
        {
            var node = SelectedTreeNode;
            List<VideoInfo> videos;
            string noTargetMessage;

            if (node?.Channel is SubscribedUser user)
            {
                videos = _database.GetVideosBySubscribedUser(user.Id);
                if (videos.Count == 0) { StatusMessage = L.T("MainForm_D118", user.Username); return; }
                noTargetMessage = L.T("MainForm_NoDownloadedInChannel", user.Username);
            }
            else if (node?.Kind == TreeNodeKind.Downloaded)
            {
                videos = _database.GetVideosByStatus(DownloadStatus.Completed);
                if (videos.Count == 0) { StatusMessage = L.T("MainForm_D119"); return; }
                noTargetMessage = L.T("MainForm_D119");
            }
            else
            {
                return;
            }

            await CheckFilesExistenceAsync(videos, noTargetMessage);
        }

        /// <summary>
        /// 完了扱いのファイルが実在するか確認し、消えていればPendingへ戻して再キューする。
        /// 旧WinForms版CheckFilesExistenceに相当(動画コンテキストメニュー/チャンネルメニュー共用)。
        /// </summary>
        private async Task CheckFilesExistenceAsync(IList<VideoInfo> videos, string noTargetMessage)
        {
            StatusMessage = L.T("MainForm_D114", videos.Count);
            var (checkedCount, missing) = await Task.Run(() =>
            {
                int cnt = 0;
                var miss = new List<VideoInfo>();
                foreach (var video in videos)
                {
                    if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath))
                    {
                        cnt++;
                        if (!File.Exists(video.LocalFilePath))
                        {
                            video.Status = DownloadStatus.Pending;
                            video.LocalFilePath = string.Empty;
                            video.DownloadedAt = null;
                            video.RetryCount = 0;
                            video.LastErrorMessage = null;
                            _database.UpdateVideo(video);
                            miss.Add(video);
                        }
                    }
                }
                return (cnt, miss);
            });

            foreach (var video in missing)
            {
                var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
                _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            }

            RefreshTree();
            LoadVideos();

            StatusMessage = checkedCount == 0 ? noTargetMessage
                : missing.Count == 0 ? L.T("MainForm_D115", checkedCount)
                : L.T("MainForm_D116", checkedCount, missing.Count);
        }

        [RelayCommand]
        private void DeleteNotFoundChannel()
        {
            var errors = _database.GetVideosByStatus(DownloadStatus.Failed);
            var notFound = errors.Where(v =>
                v.LastErrorMessage != null &&
                (v.LastErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 v.LastErrorMessage.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                 v.LastErrorMessage.Contains("deleted", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (notFound.Count == 0)
            {
                System.Windows.MessageBox.Show(L.T("MainForm_D089"), L.T("MainForm_D090"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                L.T("MainForm_D091", notFound.Count), L.T("MainForm_D092"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            int deleted = _downloadManager.ExcludeVideos(notFound);
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D093", deleted);
        }

        [RelayCommand]
        private void SetChannelSavePath()
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;

            var owner = GetOwnerWin32Window();
            var defaultDownloadFolder = SettingsManager.Instance.Settings.DownloadFolder;
            var oldSavePath = user.GetSavePath(defaultDownloadFolder);

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = L.T("MainForm_D094", user.Username),
                UseDescriptionForTitle = true,
                SelectedPath = oldSavePath,
            };
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return;

            var newSavePath = dialog.SelectedPath;
            if (string.Equals(
                    Path.GetFullPath(newSavePath).TrimEnd('\\'),
                    Path.GetFullPath(oldSavePath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var allUserVideos = _database.GetVideosBySubscribedUser(user.Id);
            var movableFiles = FileMoveHelper.GetMovableFiles(allUserVideos, oldSavePath);
            var decision = FileMoveHelper.ConfirmMove(owner, L.T("MainForm_MoveTitle", user.Username), movableFiles, oldSavePath, newSavePath);
            if (decision == FileMoveHelper.MoveDecision.Cancel) return;

            user.CustomSavePath = newSavePath;
            _database.UpdateSubscribedUser(user);
            StatusMessage = L.T("MainForm_D095", newSavePath);

            if (decision == FileMoveHelper.MoveDecision.Move && movableFiles.Count > 0)
            {
                var items = FileMoveHelper.BuildMovePlan(movableFiles, oldSavePath, newSavePath);
                using var progressForm = new FileMoveProgressForm(items, _database);
                progressForm.ShowDialog(owner);

                IndexCacheService.Invalidate(oldSavePath);
                IndexCacheService.Invalidate(newSavePath);
                FileMoveHelper.CleanupEmptyDirectories(oldSavePath);
                RefreshTree();
                LoadVideos();

                StatusMessage = L.T("MainForm_D096", progressForm.MovedCount, progressForm.FailedCount);
                if (progressForm.FailedCount > 0)
                {
                    System.Windows.MessageBox.Show(
                        L.T("MainForm_D097", progressForm.FailedCount) + L.T("MainForm_D098") + L.T("MainForm_D099"),
                        L.T("MainForm_D100"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        private void SetChannelExternalDLInherit() => SetChannelExternalOverride(null);

        [RelayCommand]
        private void SetChannelExternalDLOn() => SetChannelExternalOverride(true);

        [RelayCommand]
        private void SetChannelExternalDLOff() => SetChannelExternalOverride(false);

        private void SetChannelExternalOverride(bool? value)
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;
            user.DownloadExternalVideosOverride = value;
            _database.UpdateSubscribedUser(user);
            var label = value switch
            {
                true => L.T("MainForm_menuChExternalDLOn"),
                false => L.T("MainForm_menuChExternalDLOff"),
                null => L.T("MainForm_menuChExternalDLInherit"),
            };
            StatusMessage = L.T("MainForm_D101", user.Username, label);
        }

        [RelayCommand] private void SetChannelPriorityHighest() => SetChannelDefaultPriority(DownloadPriority.Highest);
        [RelayCommand] private void SetChannelPriorityHigh() => SetChannelDefaultPriority(DownloadPriority.High);
        [RelayCommand] private void SetChannelPriorityNormal() => SetChannelDefaultPriority(DownloadPriority.Normal);
        [RelayCommand] private void SetChannelPriorityLow() => SetChannelDefaultPriority(DownloadPriority.Low);

        /// <summary>
        /// チャンネルの既定優先度を変更する。この変更は投入時解決(EnqueueDownload/ResolvePriority)
        /// のため、既にキューに入っている動画には遡及しない — 今後そのチャンネルの動画がキューに
        /// 入る時に適用される。
        /// </summary>
        private void SetChannelDefaultPriority(DownloadPriority priority)
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;
            user.DefaultPriority = priority;
            _database.UpdateSubscribedUser(user);
            StatusMessage = L.T("MainForm_D192", user.Username);
        }

        [RelayCommand]
        private void DeleteChannel()
        {
            var user = SelectedTreeNode?.Channel;
            if (user == null) return;

            var result = System.Windows.MessageBox.Show(
                L.T("MainForm_D102", user.Username), L.T("MainForm_D103"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            _database.DeleteSubscribedUser(user.Id);
            RefreshTree();
        }

        private System.Windows.Forms.IWin32Window GetOwnerWin32Window() => OwnerWindow != null
            ? new Win32WindowWrapper(new System.Windows.Interop.WindowInteropHelper(OwnerWindow).Handle)
            : new Win32WindowWrapper(IntPtr.Zero);

        #endregion

        // RefreshTree()はTreeNodesを毎回作り直すため、選択中ノードが論理的に同じでも
        // (同じKind/ChannelId)、選択復元時に新しいインスタンスへの再代入が起きて
        // OnSelectedTreeNodeChangedが発火してしまう。新着チェック等でRefreshTreeが
        // 頻繁に走るたびにLoadVideos()(Videos.ReplaceAll)が無条件に走ると、動画一覧の
        // 選択状態(SelectedVideo)が巻き添えでリセットされ、開いている右クリックメニューの
        // コマンドが対象を失う。実質同じノードへの再代入では再読込しないようにする。
        private TreeNodeKind? _lastLoadedTreeNodeKind;
        private int? _lastLoadedTreeNodeChannelId;

        partial void OnSelectedTreeNodeChanged(ChannelTreeNodeViewModel? value)
        {
            // TreeNodes.ReplaceAll(単発Resetイベント)が起きると、WPFのSelector標準動作で
            // バインド中のSelectedItem(=SelectedTreeNode)が一瞬nullを経由してから、
            // RefreshTree()側の選択復元処理で改めて実ノードへ代入される。この中間のnull遷移で
            // ガード変数がリセットされると、直後の実ノードへの再代入が「値が変わった」と
            // 誤判定されてしまうため、nullへの変化自体は無視する(直後に必ず本代入が来る)。
            if (value == null) return;

            var kind = value.Kind;
            var channelId = value.Channel?.Id;
            if (kind == _lastLoadedTreeNodeKind && channelId == _lastLoadedTreeNodeChannelId) return;
            _lastLoadedTreeNodeKind = kind;
            _lastLoadedTreeNodeChannelId = channelId;
            LoadVideos();
        }

        private List<VideoInfo> _allLoadedVideos = new();

        /// <summary>
        /// 選択中ノードに応じた動画一覧を読み込む。旧WinForms版RefreshVideoListCoreAsyncに対応。
        /// ツリー選択が変わった時にのみ実行する(状態変化のたびに毎回全件再取得はしない)。
        /// 個別動画の進捗/状態のライブ更新はPhase7でDownloadManagerイベント経由の差分更新にする。
        /// </summary>
        public void LoadVideos()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var node = SelectedTreeNode;
            _allLoadedVideos = node?.Kind switch
            {
                TreeNodeKind.Channel when node.Channel != null => _database.GetVideosBySubscribedUser(node.Channel.Id),
                TreeNodeKind.AllVideos => _database.GetAllVideos(),
                TreeNodeKind.AllDownloads => _database.GetVideosByStatus(DownloadStatus.Downloading)
                    .Concat(_database.GetVideosByStatus(DownloadStatus.Pending)).ToList(),
                TreeNodeKind.NotDownloaded => _database.GetNotDownloadedVideos(),
                TreeNodeKind.Downloaded => _database.GetVideosByStatus(DownloadStatus.Completed),
                TreeNodeKind.Skipped => _database.GetVideosByStatus(DownloadStatus.Skipped),
                TreeNodeKind.FailedVideos => _database.GetVideosByStatus(DownloadStatus.Failed),
                TreeNodeKind.Favorites => _database.GetFavoriteVideos(),
                TreeNodeKind.Excluded => _database.GetExcludedVideos(),
                _ => new List<VideoInfo>(),
            };
            var fetchMs = sw.ElapsedMilliseconds;

            ApplyVideoFilter();
            sw.Stop();

            // LoadVideos()はDispatcherTimer(UIスレッド)から同期的に呼ばれるため、ここが遅いと
            // そのままウィンドウが無応答に見える。全件ノードでの巨大な再取得/再構築を切り分けられるよう、
            // 閾値超過時だけ内訳付きでログに残す。
            if (sw.ElapsedMilliseconds > 150)
            {
                LoggingService.Instance.Warn(
                    $"LoadVideos が遅延: 合計{sw.ElapsedMilliseconds}ms " +
                    $"(DB取得{fetchMs}ms / フィルタ適用{sw.ElapsedMilliseconds - fetchMs}ms), " +
                    $"node={node?.Kind}, 読込件数={_allLoadedVideos.Count}, 表示件数={Videos.Count}");
            }
        }

        /// <summary>
        /// 検索テキスト/NSFWフィルタ/お気に入りのみ/タグ絞り込みを_allLoadedVideosへ適用し、
        /// ソートしてVideosへ反映する。旧WinForms版ApplyVideoFilterに対応(DBへは再クエリしない)。
        /// </summary>
        private void ApplyVideoFilter()
        {
            IEnumerable<VideoInfo> source = _allLoadedVideos;
            if (NsfwFilterMode == 1)
                source = source.Where(v => string.IsNullOrEmpty(v.Rating) || v.Rating == "general");
            else if (NsfwFilterMode == 2)
                source = source.Where(v => v.Rating == "ecchi" || v.Rating == "nsfw");

            if (FavoriteOnlyFilter)
                source = source.Where(v => v.IsFavorite);

            string[] tagTerms = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(TagFilterText))
            {
                tagTerms = TagFilterText
                    .Split(new[] { ',', ' ', '　' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.ToLowerInvariant())
                    .ToArray();
                if (tagTerms.Length > 0)
                    source = source.Where(v => tagTerms.All(tt => (v.Tags ?? "").ToLowerInvariant().Contains(tt)));
            }

            var query = SearchQuery.Parse(VideoFilterText);
            query.IncludeAuthorInFreeText = SelectedTreeNode?.Kind != TreeNodeKind.Channel;
            var filtered = query.IsEmpty ? source.ToList() : source.Where(query.Match).ToList();

            SortVideoList(filtered);

            // 優先度表示の解決(Video.Priority ?? 所属チャンネルのDefaultPriority ?? Normal)用に
            // チャンネルを1回だけ一括取得してDictionary化(動画1件ごとのDB問い合わせを避ける)。
            var userMap = _database.GetAllSubscribedUsers().ToDictionary(u => u.Id);

            var items = new List<VideoListItemViewModel>(filtered.Count);
            foreach (var video in filtered)
            {
                var task = _downloadManager.GetTask(video.VideoId);
                var owner = video.SubscribedUserId.HasValue && userMap.TryGetValue(video.SubscribedUserId.Value, out var u) ? u : null;
                var item = new VideoListItemViewModel(video);
                item.Refresh(task, owner);
                items.Add(item);
            }
            // Add()を件数分呼ぶとCollectionChangedが都度発火しUIスレッドが固まるため、
            // 単発のResetイベントで一括差し替える。
            Videos.ReplaceAll(items);

            if (!query.IsEmpty || NsfwFilterMode != 0 || FavoriteOnlyFilter || tagTerms.Length > 0)
            {
                StatusMessage = L.T("MainForm_D069", filtered.Count, _allLoadedVideos.Count);
            }
        }

        #region 動画一覧フィルタ/ソート (Phase8a-4でパリティ閉じ)

        [ObservableProperty] private string _videoFilterText = "";
        [ObservableProperty] private bool _isAdvancedFilterVisible;
        [ObservableProperty] private bool _favoriteOnlyFilter;
        [ObservableProperty] private string _tagFilterText = "";
        [ObservableProperty] private int _nsfwFilterMode = SettingsManager.Instance.Settings.NsfwFilterMode;

        public string AdvancedSearchToggleText => IsAdvancedFilterVisible ? L.T("MainForm_D166") : L.T("MainForm_D167");

        partial void OnVideoFilterTextChanged(string value) => ApplyVideoFilter();
        partial void OnFavoriteOnlyFilterChanged(bool value) => ApplyVideoFilter();
        partial void OnTagFilterTextChanged(string value) => ApplyVideoFilter();

        partial void OnNsfwFilterModeChanged(int value)
        {
            SettingsManager.Instance.Settings.NsfwFilterMode = value;
            SettingsManager.Instance.Save();
            ApplyVideoFilter();
        }

        [RelayCommand]
        private void ClearVideoFilter() => VideoFilterText = "";

        [RelayCommand]
        private void ToggleAdvancedSearch()
        {
            IsAdvancedFilterVisible = !IsAdvancedFilterVisible;
            OnPropertyChanged(nameof(AdvancedSearchToggleText));
        }

        // ソート状態(旧WinForms版_sortColumn/_sortOrder相当)。デフォルトは公開日時の降順(新しい順)。
        // 列インデックスはMainWindow.xamlのGridViewColumn順(タイトル/ソース/状態/優先度/進捗/サイズ/公開日時)と一致させること。
        private int _sortColumn = 6;
        private bool _sortDescending = true;

        [ObservableProperty] private string _titleColumnHeader = "";
        [ObservableProperty] private string _sourceColumnHeader = "";
        [ObservableProperty] private string _statusColumnHeader = "";
        [ObservableProperty] private string _priorityColumnHeader = "";
        [ObservableProperty] private string _progressColumnHeader = "";
        [ObservableProperty] private string _sizeColumnHeader = "";
        [ObservableProperty] private string _dateColumnHeader = "";

        /// <summary>
        /// 動画一覧のGridViewColumnHeaderクリックで呼ぶ。旧WinForms版listViewVideos_ColumnClickに対応。
        /// </summary>
        public void SortVideosByColumn(int columnIndex)
        {
            if (columnIndex == _sortColumn) _sortDescending = !_sortDescending;
            else
            {
                _sortColumn = columnIndex;
                // 優先度列(3)だけ降順スタート: Pending以外は同値(-1)の塊になるため、昇順だと
                // その塊が先頭に来てキュー本体(Highest〜Low)が埋もれる。
                _sortDescending = columnIndex == 3;
            }
            ApplyVideoFilter();
        }

        private void SortVideoList(List<VideoInfo> list)
        {
            Comparison<VideoInfo> comparison = _sortColumn switch
            {
                0 => (a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture),
                1 => (a, b) => string.Compare(VideoListItemViewModel.GetSourceLabel(a), VideoListItemViewModel.GetSourceLabel(b), StringComparison.Ordinal),
                2 => (a, b) => a.Status.CompareTo(b.Status),
                3 => (a, b) => GetPrioritySortValue(a).CompareTo(GetPrioritySortValue(b)),
                4 => (a, b) => GetProgressSortValue(a).CompareTo(GetProgressSortValue(b)),
                5 => (a, b) => a.FileSize.CompareTo(b.FileSize),
                6 => (a, b) => (a.PostedAt ?? a.CreatedAt).CompareTo(b.PostedAt ?? b.CreatedAt),
                _ => (a, b) => 0,
            };
            list.Sort(comparison);
            if (_sortDescending) list.Reverse();

            UpdateColumnHeaderTexts();
        }

        private double GetProgressSortValue(VideoInfo video)
        {
            var task = _downloadManager.GetTask(video.VideoId);
            if (task != null && task.Status == DownloadStatus.Downloading) return task.Progress;
            if (video.Status == DownloadStatus.Completed) return 100;
            if (video.Status == DownloadStatus.Pending) return -1;
            return -2;
        }

        /// <summary>
        /// 優先度はキュー待ち(Pending)にしか意味を持たない(VideoListItemViewModel.Refreshの表示ルールと同じ)。
        /// それ以外の状態は全て最下位扱いにして一覧の末尾/先頭にまとめる。
        /// </summary>
        private double GetPrioritySortValue(VideoInfo video)
        {
            if (video.Status != DownloadStatus.Pending) return -1;
            var task = _downloadManager.GetTask(video.VideoId);
            var resolved = video.Priority ?? task?.SubscribedUser?.DefaultPriority ?? DownloadPriority.Normal;
            return (double)resolved;
        }

        private void UpdateColumnHeaderTexts()
        {
            var baseTexts = new[]
            {
                L.T("MainForm_colVideoTitle"), L.T("MainForm_colVideoSource"), L.T("MainForm_colVideoStatus"),
                L.T("MainForm_colVideoPriority"), L.T("MainForm_colVideoProgress"), L.T("MainForm_colVideoSize"), L.T("MainForm_colVideoDate"),
            };
            var arrow = _sortDescending ? " ▼" : " ▲";
            TitleColumnHeader = baseTexts[0] + (_sortColumn == 0 ? arrow : "");
            SourceColumnHeader = baseTexts[1] + (_sortColumn == 1 ? arrow : "");
            StatusColumnHeader = baseTexts[2] + (_sortColumn == 2 ? arrow : "");
            PriorityColumnHeader = baseTexts[3] + (_sortColumn == 3 ? arrow : "");
            ProgressColumnHeader = baseTexts[4] + (_sortColumn == 4 ? arrow : "");
            SizeColumnHeader = baseTexts[5] + (_sortColumn == 5 ? arrow : "");
            DateColumnHeader = baseTexts[6] + (_sortColumn == 6 ? arrow : "");
        }

        #endregion

        /// <summary>
        /// チャンネルツリーを再構築する。旧WinForms版RefreshChannelTreeCoreAsyncに対応。
        /// SQL集計(GetVideoTreeCounts)を使うため動画数万件規模でも軽い(Phase3以前の教訓を踏襲)。
        /// </summary>
        public void RefreshTree()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var counts = _database.GetVideoTreeCounts();
            var users = _database.GetAllSubscribedUsers();
            var excludedCount = _database.GetExcludedCount();

            var selectedKind = SelectedTreeNode?.Kind;
            var selectedChannelId = SelectedTreeNode?.Channel?.Id;

            // TreeNodes.Add()を件数分呼ぶとCollectionChangedが都度発火するため、
            // ローカルリストに組み立ててから最後にまとめて差し替える(Videosと同じ理由)。
            var nodes = new List<ChannelTreeNodeViewModel>(users.Count + 8);

            nodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.AllVideos,
                Text = L.T("MainForm_D177", counts.Completed, counts.Total),
                IsBold = true,
            });

            nodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.Favorites,
                Text = L.T("MainForm_D178", counts.Favorite),
                Foreground = ThemeManager.GetBrush("Brush.Favorite"),
            });

            nodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.AllDownloads,
                Text = L.T("MainForm_D179"),
            });

            if (counts.NotDownloaded > 0)
            {
                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.NotDownloaded,
                    Text = L.T("MainForm_D180", counts.NotDownloaded),
                    Foreground = ThemeManager.GetBrush("Brush.Warning"),
                });
            }

            if (counts.Completed > 0)
            {
                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Downloaded,
                    Text = L.T("MainForm_D181", counts.Completed),
                    Foreground = ThemeManager.GetBrush("Brush.Success"),
                });
            }

            if (counts.Skipped > 0)
            {
                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Skipped,
                    Text = L.T("MainForm_D182", counts.Skipped),
                    Foreground = ThemeManager.GetBrush("Brush.TextSecondary"),
                });
            }

            if (counts.Failed > 0)
            {
                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.FailedVideos,
                    Text = L.T("MainForm_D183", counts.Failed),
                    Foreground = ThemeManager.GetBrush("Brush.Danger"),
                });
            }

            if (excludedCount > 0)
            {
                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Excluded,
                    Text = L.T("MainForm_ExcludedNode", excludedCount),
                    Foreground = ThemeManager.GetBrush("Brush.TextSecondary"),
                });
            }

            foreach (var user in users)
            {
                counts.ByChannel.TryGetValue(user.Id, out var ch);
                var chTotal = ch?.Total ?? 0;
                var chCompleted = ch?.Completed ?? 0;
                var chDownloading = ch?.Downloading ?? 0;
                var chPending = ch?.Pending ?? 0;
                var chPaused = ch?.Paused ?? 0;

                var statusText = "";
                if (chDownloading > 0) statusText = $" 🔄{chDownloading}";
                else if (chPending > 0) statusText = $" ⏳{chPending}";
                if (chPaused > 0) statusText += $" ⏸️{chPaused}";

                // アカウント消滅(iwara側で404)は無効化(⬜)より優先して表示する。
                // 消滅済みでも無効化されていても、DL済みファイルや動画情報自体は消えない。
                var channelEmoji = user.IsAccountDeleted ? "❌" : (user.IsEnabled ? "📺" : "⬜");
                var channelForeground = user.IsAccountDeleted
                    ? ThemeManager.GetBrush("Brush.Danger")
                    : (user.IsEnabled ? ThemeManager.GetBrush("Brush.Text") : ThemeManager.GetBrush("Brush.TextDisabled"));

                nodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Channel,
                    Channel = user,
                    Text = $"{channelEmoji} {user.Username} [{chCompleted}/{chTotal}]{statusText}",
                    Foreground = channelForeground,
                });
            }

            TreeNodes.ReplaceAll(nodes);

            // 選択状態を復元
            if (selectedKind != null)
            {
                SelectedTreeNode = TreeNodes.FirstOrDefault(n =>
                    n.Kind == selectedKind &&
                    (n.Kind != TreeNodeKind.Channel || n.Channel?.Id == selectedChannelId));
            }
            SelectedTreeNode ??= TreeNodes.FirstOrDefault();

            sw.Stop();
            if (sw.ElapsedMilliseconds > 150)
            {
                LoggingService.Instance.Warn(
                    $"RefreshTree が遅延: {sw.ElapsedMilliseconds}ms, チャンネル数={users.Count}");
            }
        }
    }
}
