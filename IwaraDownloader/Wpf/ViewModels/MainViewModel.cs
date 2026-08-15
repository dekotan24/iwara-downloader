using System.Collections.ObjectModel;
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
        // Phase8カットオーバーまではWinForms版MainFormも別途DownloadManagerを持つ (二重インスタンス)。
        // アプリ全体で1個に統合するのはカットオーバー時の作業とする。
        private readonly DownloadManager _downloadManager = new();
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

        public ObservableCollection<ChannelTreeNodeViewModel> TreeNodes { get; } = new();
        public ObservableCollection<VideoListItemViewModel> Videos { get; } = new();

        [ObservableProperty]
        private ChannelTreeNodeViewModel? _selectedTreeNode;

        [ObservableProperty]
        private VideoListItemViewModel? _selectedVideo;

        [ObservableProperty]
        private string _urlInput = "";

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private string _freeSpaceText = "";

        [ObservableProperty]
        private Brush _freeSpaceForeground = ThemeManager.GetBrush("Brush.TextSecondary");

        [ObservableProperty]
        private string _downloadCountText = "";

        [ObservableProperty]
        private int _progressBarValue;

        private readonly DispatcherTimer _freeSpaceTimer;

        public MainViewModel()
        {
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
        }

        public void Dispose()
        {
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

        /// <summary>短時間に複数回呼ばれてもLoadVideos()は1回だけ実行される(旧WinForms版RefreshVideoListに相当)</summary>
        private void ScheduleVideoListRefresh()
        {
            _videoListRefreshTimer ??= CreateDebounceTimer(VideoListRefreshDebounceMs, LoadVideos);
            _videoListRefreshTimer.Stop();
            _videoListRefreshTimer.Start();
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
                queueText = L.T("MainForm_QueueSummaryFull", dlTasks.Count, avgInProgress.ToString("F0"), pending);
            }
            else if (pending > 0)
            {
                queueText = L.T("MainForm_QueueSummaryPending", pending);
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
                LoadVideos();
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

        #region 動画コンテキストメニュー (Phase4f, Phase8a-1でパリティ閉じ)
        // 現時点は単一選択(SelectedVideo)のみ対応。複数選択の一括操作はPhase8a-5で対応。

        [ObservableProperty] private bool _isExcludedNodeSelected;
        [ObservableProperty] private bool _isNormalNodeSelected = true;
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
        [ObservableProperty] private string _favoriteMenuText = "";

        /// <summary>
        /// 動画コンテキストメニューを開く直前に呼ぶ。旧WinForms版menuVideoContext_Openingに相当
        /// (表示直前に選択中動画のステータスから各項目のVisibleを再計算する方式を踏襲)。
        /// </summary>
        public void RefreshVideoContextMenuState()
        {
            IsExcludedNodeSelected = SelectedTreeNode?.Kind == TreeNodeKind.Excluded;
            IsNormalNodeSelected = !IsExcludedNodeSelected;
            var video = SelectedVideo?.Video;
            if (video == null || IsExcludedNodeSelected)
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
                return;
            }

            bool isOrphanPending = video.Status == DownloadStatus.Pending && _downloadManager.GetTask(video.VideoId) == null;
            CanDownloadSelectedVideo = video.Status != DownloadStatus.Downloading && video.Status != DownloadStatus.Completed
                && (video.Status != DownloadStatus.Pending || isOrphanPending);
            DownloadMenuText = video.Status == DownloadStatus.Failed ? L.T("MainForm_D104") : L.T("MainForm_D105");
            CanCancelSelectedVideo = video.Status == DownloadStatus.Pending || video.Status == DownloadStatus.Downloading || video.Status == DownloadStatus.Paused;
            CanRetryFailedSelectedVideo = video.Status == DownloadStatus.Failed;
            CanReDownloadSelectedVideo = video.Status == DownloadStatus.Completed;
            CanRefreshInfoSelectedVideo = string.IsNullOrEmpty(video.Title) || video.Title.StartsWith("Video ");
            CanCheckFileExistsSelectedVideo = video.Status == DownloadStatus.Completed;
            CanPlaySelectedVideo = video.Status == DownloadStatus.Completed && video.LocalFileExists;
            CanOpenFolderSelectedVideo = video.LocalFileExists;
            CanMapLocalFileSelectedVideo = video.Status != DownloadStatus.Downloading && video.Status != DownloadStatus.WritingTags
                && (!video.LocalFileExists || video.Status == DownloadStatus.Failed || video.Status == DownloadStatus.Pending);
            CanOpenAuthorSelectedVideo = !string.IsNullOrEmpty(video.AuthorUsername);
            FavoriteMenuText = video.IsFavorite ? L.T("MainForm_D106") : L.T("MainForm_D107");
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
            var video = SelectedVideo?.Video;
            if (video != null && !string.IsNullOrEmpty(video.Url))
            {
                System.Windows.Clipboard.SetText(video.Url);
                StatusMessage = L.T("MainForm_D111", 1);
            }
        }

        [RelayCommand]
        private void CopyVideoTitle()
        {
            var video = SelectedVideo?.Video;
            if (video != null && !string.IsNullOrEmpty(video.Title))
            {
                System.Windows.Clipboard.SetText(video.Title);
                StatusMessage = L.T("MainForm_D112", 1);
            }
        }

        [RelayCommand]
        private void ToggleFavorite()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;
            video.IsFavorite = !video.IsFavorite;
            _database.UpdateVideo(video);
            SelectedVideo!.Refresh(_downloadManager.GetTask(video.VideoId));
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
            var video = SelectedVideo?.Video;
            if (video == null) return;

            var result = System.Windows.MessageBox.Show(
                L.T("MainForm_ConfirmDeleteOne", video.Title) + L.T("MainForm_D120"),
                L.T("MainForm_D103"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            var deletedCount = _downloadManager.ExcludeVideos(new[] { video });
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D121", deletedCount);
        }

        [RelayCommand]
        private void RestoreVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;
            var restored = _downloadManager.RestoreExcludedVideos(new[] { video.VideoId });
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_RestoredStatus", restored);
        }

        [RelayCommand]
        private void DownloadVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;

            if (video.Status == DownloadStatus.Failed)
            {
                video.RetryCount = 0;
                video.LastErrorMessage = null;
                _database.UpdateVideo(video);
            }

            var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
            _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void CancelVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;
            _downloadManager.CancelTask(video.VideoId);
            RefreshTree();
            LoadVideos();
        }

        [RelayCommand]
        private void RetryFailedVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null || video.Status != DownloadStatus.Failed) return;

            video.RetryCount = 0;
            video.LastErrorMessage = null;
            _database.UpdateVideo(video);

            var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
            _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D108", 1);
        }

        [RelayCommand]
        private async Task RefreshVideoInfo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;
            if (!(string.IsNullOrEmpty(video.Title) || video.Title.StartsWith("Video "))) return;

            var progress = new Progress<string>(msg => StatusMessage = msg);
            var success = await _downloadManager.RefreshVideoInfoAsync(video, progress);
            LoadVideos();
            StatusMessage = L.T("MainForm_D110", success ? 1 : 0);
        }

        [RelayCommand]
        private void CheckFileExistsForSelectedVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null || video.Status != DownloadStatus.Completed || string.IsNullOrEmpty(video.LocalFilePath)) return;

            if (File.Exists(video.LocalFilePath))
            {
                StatusMessage = L.T("MainForm_D115", 1);
                return;
            }

            video.Status = DownloadStatus.Pending;
            video.LocalFilePath = string.Empty;
            video.DownloadedAt = null;
            video.RetryCount = 0;
            video.LastErrorMessage = null;
            _database.UpdateVideo(video);

            var user = video.SubscribedUserId.HasValue ? _database.GetSubscribedUserById(video.SubscribedUserId.Value) : null;
            _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D116", 1, 1);
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
            var video = SelectedVideo?.Video;
            if (video == null || video.Status != DownloadStatus.Completed) return;

            var result = System.Windows.MessageBox.Show(
                L.T("MainForm_ConfirmRedlOne", video.Title),
                L.T("MainForm_D122"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

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

            RefreshTree();
            LoadVideos();
            StatusMessage = L.T("MainForm_D123", 1);
        }

        [RelayCommand]
        private void PermanentDeleteVideo()
        {
            var video = SelectedVideo?.Video;
            if (video == null) return;

            var result = System.Windows.MessageBox.Show(
                L.T("MainForm_ConfirmPurgeOne", video.Title), L.T("MainForm_D103"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            int deleted = _database.DeleteExcludedPermanent(new[] { video.VideoId });
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

        /// <summary>
        /// チャンネルコンテキストメニューを開く直前に呼ぶ。旧WinForms版contextMenuChannel_Openingに相当。
        /// </summary>
        public void RefreshChannelContextMenuState()
        {
            var node = SelectedTreeNode;
            IsUserNodeSelected = node?.Kind == TreeNodeKind.Channel;
            CanCheckChannelFiles = IsUserNodeSelected || node?.Kind == TreeNodeKind.Downloaded;
            CanDownloadAllChannel = IsUserNodeSelected
                || node?.Kind is TreeNodeKind.NotDownloaded or TreeNodeKind.FailedVideos or TreeNodeKind.SingleVideos;
            CanDeleteNotFoundChannel = node?.Kind == TreeNodeKind.FailedVideos;

            var user = node?.Channel;
            if (user == null)
            {
                CanEnableChannel = false;
                CanDisableChannel = false;
                CheckChannelNowEnabled = false;
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
            else if (node?.Kind == TreeNodeKind.SingleVideos)
            {
                videos = _database.GetAllVideos()
                    .Where(v => !v.SubscribedUserId.HasValue && v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading
                             && (v.Status != DownloadStatus.Pending || _downloadManager.GetTask(v.VideoId) == null))
                    .ToList();
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

        partial void OnSelectedTreeNodeChanged(ChannelTreeNodeViewModel? value) => LoadVideos();

        /// <summary>
        /// 選択中ノードに応じた動画一覧を読み込む。旧WinForms版RefreshVideoListCoreAsyncに対応。
        /// ツリー選択が変わった時にのみ実行する(状態変化のたびに毎回全件再取得はしない)。
        /// 個別動画の進捗/状態のライブ更新はPhase7でDownloadManagerイベント経由の差分更新にする。
        /// </summary>
        public void LoadVideos()
        {
            Videos.Clear();
            var node = SelectedTreeNode;
            if (node == null) return;

            List<VideoInfo> videos = node.Kind switch
            {
                TreeNodeKind.Channel when node.Channel != null => _database.GetVideosBySubscribedUser(node.Channel.Id),
                TreeNodeKind.AllVideos => _database.GetAllVideos(),
                TreeNodeKind.AllDownloads => _database.GetVideosByStatus(DownloadStatus.Downloading)
                    .Concat(_database.GetVideosByStatus(DownloadStatus.Pending)).ToList(),
                TreeNodeKind.NotDownloaded => _database.GetNotDownloadedVideos(),
                TreeNodeKind.Downloaded => _database.GetVideosByStatus(DownloadStatus.Completed),
                TreeNodeKind.Skipped => _database.GetVideosByStatus(DownloadStatus.Skipped),
                TreeNodeKind.FailedVideos => _database.GetVideosByStatus(DownloadStatus.Failed),
                TreeNodeKind.SingleVideos => _database.GetSingleVideos(),
                TreeNodeKind.Favorites => _database.GetFavoriteVideos(),
                TreeNodeKind.Excluded => _database.GetExcludedVideos(),
                _ => new List<VideoInfo>(),
            };

            // 各DatabaseServiceメソッドが既にSQL側でCreatedAt DESC順に返す(GetVideosByStatusのConcatは
            // 旧WinForms版と同じ挙動: 個々にソート済みだが連結後の全体ソートはしない)ため、ここでは
            // 追加のソートをしない(4万件規模の再ソートを避ける)。
            foreach (var video in videos)
            {
                var task = _downloadManager.GetTask(video.VideoId);
                var item = new VideoListItemViewModel(video);
                item.Refresh(task);
                Videos.Add(item);
            }
        }

        /// <summary>
        /// チャンネルツリーを再構築する。旧WinForms版RefreshChannelTreeCoreAsyncに対応。
        /// SQL集計(GetVideoTreeCounts)を使うため動画数万件規模でも軽い(Phase3以前の教訓を踏襲)。
        /// </summary>
        public void RefreshTree()
        {
            var counts = _database.GetVideoTreeCounts();
            var users = _database.GetAllSubscribedUsers();
            var excludedCount = _database.GetExcludedCount();

            var selectedKind = SelectedTreeNode?.Kind;
            var selectedChannelId = SelectedTreeNode?.Channel?.Id;

            TreeNodes.Clear();

            TreeNodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.AllVideos,
                Text = L.T("MainForm_D177", counts.Completed, counts.Total),
                IsBold = true,
            });

            TreeNodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.Favorites,
                Text = L.T("MainForm_D178", counts.Favorite),
                Foreground = ThemeManager.GetBrush("Brush.Favorite"),
            });

            TreeNodes.Add(new ChannelTreeNodeViewModel
            {
                Kind = TreeNodeKind.AllDownloads,
                Text = L.T("MainForm_D179"),
            });

            if (counts.NotDownloaded > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.NotDownloaded,
                    Text = L.T("MainForm_D180", counts.NotDownloaded),
                    Foreground = ThemeManager.GetBrush("Brush.Warning"),
                });
            }

            if (counts.Completed > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Downloaded,
                    Text = L.T("MainForm_D181", counts.Completed),
                    Foreground = ThemeManager.GetBrush("Brush.Success"),
                });
            }

            if (counts.Skipped > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Skipped,
                    Text = L.T("MainForm_D182", counts.Skipped),
                    Foreground = ThemeManager.GetBrush("Brush.TextSecondary"),
                });
            }

            if (counts.Failed > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.FailedVideos,
                    Text = L.T("MainForm_D183", counts.Failed),
                    Foreground = ThemeManager.GetBrush("Brush.Danger"),
                });
            }

            if (counts.SingleVideos > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.SingleVideos,
                    Text = L.T("MainForm_D184", counts.SingleVideos),
                });
            }

            if (excludedCount > 0)
            {
                TreeNodes.Add(new ChannelTreeNodeViewModel
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

                TreeNodes.Add(new ChannelTreeNodeViewModel
                {
                    Kind = TreeNodeKind.Channel,
                    Channel = user,
                    Text = $"{(user.IsEnabled ? "📺" : "⬜")} {user.Username} [{chCompleted}/{chTotal}]{statusText}",
                    Foreground = user.IsEnabled ? ThemeManager.GetBrush("Brush.Text") : ThemeManager.GetBrush("Brush.TextDisabled"),
                });
            }

            // 選択状態を復元
            if (selectedKind != null)
            {
                SelectedTreeNode = TreeNodes.FirstOrDefault(n =>
                    n.Kind == selectedKind &&
                    (n.Kind != TreeNodeKind.Channel || n.Channel?.Id == selectedChannelId));
            }
            SelectedTreeNode ??= TreeNodes.FirstOrDefault();
        }
    }
}
