using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using IwaraDownloader.Wpf.Theme;
using Brush = System.Windows.Media.Brush;
using ImageSource = System.Windows.Media.ImageSource;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// 動画一覧の1行分。旧WinForms版CreateVideoListItemに対応する6列
    /// (タイトル/ソース/状態/進捗/サイズ/公開日時)を持つ。
    /// </summary>
    public partial class VideoListItemViewModel : ObservableObject
    {
        public VideoInfo Video { get; private set; }

        public VideoListItemViewModel(VideoInfo video)
        {
            Video = video;
            Refresh();
        }

        /// <summary>
        /// DownloadManagerのTaskProgressChanged/TaskStatusChanged経由の更新用。
        /// task.VideoはDownloadManagerが実際に書き換え続けている参照そのものなので、
        /// 表示側のVideoもそれに差し替えてから再計算する(旧WinForms版UpdateVideoItemの
        /// `_displayVideoList[i] = task.Video` に相当)。
        /// </summary>
        public void ApplyTaskUpdate(DownloadTask task)
        {
            Video = task.Video;
            Refresh(task);
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
            DisplayDate = (Video.PostedAt ?? Video.CreatedAt).ToString("yyyy/MM/dd HH:mm");
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

        internal static string GetSourceLabel(VideoInfo video)
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
        private string _displayDate = "";

        [ObservableProperty]
        private bool _isFavorite;

        [ObservableProperty]
        private ImageSource? _thumbnail;

        /// <summary>
        /// タイル表示に切り替わった時に呼ぶ。メモリキャッシュにあれば即座に反映、無ければ
        /// バックグラウンドロードを開始するだけ(結果はMainViewModel.OnThumbnailReady経由で反映)。
        /// UIスレッド上でI/Oは発生しない(ThumbnailCacheService.TryGetMemoryCachedBytesはメモリのみ参照)。
        /// </summary>
        public void EnsureThumbnailLoaded()
        {
            if (Thumbnail != null) return;
            var bytes = ThumbnailCacheService.Instance.TryGetMemoryCachedBytes(Video.VideoId);
            if (bytes != null)
            {
                ApplyThumbnailBytes(bytes);
            }
            else
            {
                ThumbnailCacheService.Instance.EnsureLoadedAsync(Video.VideoId, Video.ThumbnailUrl);
            }
        }

        public void ApplyThumbnailBytes(byte[] bytes)
        {
            try
            {
                var bmp = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                Thumbnail = bmp;
            }
            catch
            {
                // 破損データ等はプレースホルダーのまま(Thumbnail=null)にしておく
            }
        }
    }
}
