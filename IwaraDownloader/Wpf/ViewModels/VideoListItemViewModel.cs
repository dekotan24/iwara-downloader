using CommunityToolkit.Mvvm.ComponentModel;
using IwaraDownloader.Models;
using IwaraDownloader.Utils;
using IwaraDownloader.Wpf.Theme;
using Brush = System.Windows.Media.Brush;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// 動画一覧の1行分。旧WinForms版CreateVideoListItemに対応する6列
    /// (タイトル/ソース/状態/進捗/サイズ/追加日時)を持つ。
    /// </summary>
    public partial class VideoListItemViewModel : ObservableObject
    {
        public VideoInfo Video { get; }

        public VideoListItemViewModel(VideoInfo video)
        {
            Video = video;
            Refresh();
        }

        /// <summary>
        /// Video(DBから読んだ値)を元に表示用プロパティを再計算する。
        /// DownloadTaskの進捗/状態が別途あればそちらを優先する場合はoverrideを渡す。
        /// </summary>
        public void Refresh(DownloadTask? task = null)
        {
            Title = Video.Title;
            Source = GetSourceLabel(Video);
            FileSize = Video.FileSizeFormatted;
            CreatedAt = Video.CreatedAt.ToString("yyyy/MM/dd HH:mm");
            IsFavorite = Video.IsFavorite;

            var effectiveStatus = task?.Status ?? Video.Status;
            StatusText = GetStatusText(effectiveStatus);
            StatusForeground = GetStatusBrush(effectiveStatus);

            if (task != null && task.Status == DownloadStatus.Downloading)
            {
                ProgressText = task.Progress > 0
                    ? (task.DownloadSpeed > 0 ? $"{task.Progress:F0}% ({task.SpeedFormatted})" : $"{task.Progress:F0}%")
                    : L.T("MainForm_ProgressDownloading");
            }
            else if (task != null && task.Status == DownloadStatus.WritingTags)
            {
                ProgressText = L.T("MainForm_ProgressWritingTags");
            }
            else if (Video.Status == DownloadStatus.Completed)
            {
                ProgressText = "100%";
            }
            else if (Video.Status == DownloadStatus.Pending)
            {
                ProgressText = L.T("MainForm_D074");
            }
            else
            {
                ProgressText = "-";
            }
        }

        private static string GetSourceLabel(VideoInfo video)
        {
            if (!video.IsExternal)
                return string.Equals(video.Site, Helpers.SiteAi, StringComparison.OrdinalIgnoreCase) ? "iwara.ai" : "iwara.tv";

            var url = video.EmbedUrl?.ToLowerInvariant() ?? string.Empty;
            if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
            if (url.Contains("vimeo.com")) return "Vimeo";
            if (url.Contains("twitter.com") || url.Contains("x.com")) return "X/Twitter";
            if (url.Contains("nicovideo.jp")) return L.T("MainForm_SourceNico");
            if (url.Contains("bilibili.com")) return "Bilibili";
            return L.T("MainForm_SourceExternal");
        }

        internal static string GetStatusText(DownloadStatus status) => status switch
        {
            DownloadStatus.Pending => L.T("MainForm_D074"),
            DownloadStatus.Downloading => L.T("MainForm_D075"),
            DownloadStatus.WritingTags => L.T("MainForm_D076"),
            DownloadStatus.Completed => L.T("MainForm_D014"),
            DownloadStatus.Failed => L.T("MainForm_D077"),
            DownloadStatus.Skipped => L.T("MainForm_D078"),
            DownloadStatus.Paused => L.T("MainForm_D079"),
            _ => L.T("MainForm_D080"),
        };

        private static Brush GetStatusBrush(DownloadStatus status) => status switch
        {
            DownloadStatus.Completed => ThemeManager.GetBrush("Brush.Success"),
            DownloadStatus.Failed => ThemeManager.GetBrush("Brush.Danger"),
            DownloadStatus.Downloading => ThemeManager.GetBrush("Brush.Accent"),
            DownloadStatus.WritingTags => ThemeManager.GetBrush("Brush.Warning"),
            DownloadStatus.Skipped => ThemeManager.GetBrush("Brush.TextDisabled"),
            _ => ThemeManager.GetBrush("Brush.TextSecondary"),
        };

        [ObservableProperty]
        private string _title = "";

        [ObservableProperty]
        private string _source = "";

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private Brush _statusForeground = System.Windows.Media.Brushes.Gray;

        [ObservableProperty]
        private string _progressText = "-";

        [ObservableProperty]
        private string _fileSize = "";

        [ObservableProperty]
        private string _createdAt = "";

        [ObservableProperty]
        private bool _isFavorite;
    }
}
