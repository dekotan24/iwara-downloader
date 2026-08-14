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
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _database = DatabaseService.Instance;
        // Phase8カットオーバーまではWinForms版MainFormも別途DownloadManagerを持つ (二重インスタンス)。
        // アプリ全体で1個に統合するのはカットオーバー時の作業とする。
        private readonly DownloadManager _downloadManager = new();

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

            // 旧WinForms版StartFreeSpaceMonitorに対応(1分間隔)。DL件数/進捗のライブ更新は
            // Phase7でDownloadManagerイベント購読に置き換える(ここでは初期表示のみ)。
            _freeSpaceTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _freeSpaceTimer.Tick += (_, _) => RefreshFreeSpace();
            _freeSpaceTimer.Start();
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
            var downloading = _database.GetVideosByStatus(DownloadStatus.Downloading).Count;
            var pending = _database.GetVideosByStatus(DownloadStatus.Pending).Count;
            var allVideos = _database.GetAllVideos();
            var completed = allVideos.Count(v => v.Status == DownloadStatus.Completed);
            var totalSize = allVideos.Where(v => v.Status == DownloadStatus.Completed).Sum(v => v.FileSize);
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

        #region 動画コンテキストメニュー (Phase4f)
        // 現時点は単一選択(SelectedVideo)のみ対応。複数選択の一括操作はPhase7以降で拡張検討。

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
            // TODO: dev側のCancelTask修正(feature/wpf-migration分岐後にdevへ入った)がマージされたら
            // 4引数版(downloadManagerを渡す)に合わせること。
            using var form = new VideoDetailsForm(video, _database, _downloadManager.IwaraApi);
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

        #endregion

        #region チャンネルコンテキストメニュー (Phase4f)

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
