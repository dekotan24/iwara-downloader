using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using IwaraDownloader.Wpf.Models;
using IwaraDownloader.Wpf.Theme;

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

        public MainViewModel()
        {
            RefreshTree();
        }

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
