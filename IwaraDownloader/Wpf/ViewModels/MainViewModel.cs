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

        public ObservableCollection<ChannelTreeNodeViewModel> TreeNodes { get; } = new();

        [ObservableProperty]
        private ChannelTreeNodeViewModel? _selectedTreeNode;

        public MainViewModel()
        {
            RefreshTree();
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
