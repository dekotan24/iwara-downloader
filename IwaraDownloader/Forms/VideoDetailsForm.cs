using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 動画の詳細情報ダイアログ。
    /// 表示: タイトル / 投稿者 / ID / UUID / ステータス / サイズ / 日時 / URL / 保存先 / エラー履歴
    /// 編集: タグ / メモ (DB 上はカラムあるが UI からの編集箇所が今まで無かった)
    /// </summary>
    public partial class VideoDetailsForm : Form
    {
        private readonly VideoInfo _video;
        private readonly DatabaseService _database;

        public VideoDetailsForm(VideoInfo video, DatabaseService database)
        {
            _video = video;
            _database = database;
            InitializeComponent();
            PopulateFields();
        }

        private void PopulateFields()
        {
            txtTitle.Text = _video.Title;
            txtSource.Text = GetSourceLabel(_video);
            txtAuthor.Text = _video.AuthorUsername;
            txtVideoId.Text = _video.VideoId;
            txtFileUuid.Text = _video.FileUuid;
            txtStatus.Text = GetStatusText(_video.Status);
            txtDuration.Text = _video.DurationFormatted;
            txtFileSize.Text = _video.FileSizeFormatted;
            txtPostedAt.Text = _video.PostedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            txtDownloadedAt.Text = _video.DownloadedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            txtCreatedAt.Text = _video.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            txtUrl.Text = _video.Url;
            txtLocalFilePath.Text = _video.LocalFilePath;
            txtRetry.Text = _video.RetryCount.ToString();
            txtLastError.Text = _video.LastErrorMessage ?? "";
            txtTags.Text = _video.Tags;
            txtMemo.Text = _video.Memo;

            // 開くボタンの有効/無効
            btnOpenUrl.Enabled = !string.IsNullOrEmpty(_video.Url);
            btnOpenFile.Enabled = !string.IsNullOrEmpty(_video.LocalFilePath)
                                && System.IO.File.Exists(_video.LocalFilePath);
        }

        /// <summary>
        /// ソース表示用 (iwara.tv / iwara.ai / YouTube / niconico 等)。
        /// MainForm.GetVideoSourceLabel と同じロジック。
        /// </summary>
        private static string GetSourceLabel(VideoInfo v)
        {
            if (!v.IsExternal)
            {
                if (string.Equals(v.Site, Helpers.SiteAi, StringComparison.OrdinalIgnoreCase))
                    return "iwara.ai";
                return "iwara.tv";
            }
            var url = v.EmbedUrl?.ToLowerInvariant() ?? string.Empty;
            if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
            if (url.Contains("vimeo.com")) return "Vimeo";
            if (url.Contains("twitter.com") || url.Contains("x.com")) return "X/Twitter";
            if (url.Contains("nicovideo.jp")) return "ニコニコ";
            if (url.Contains("bilibili.com")) return "Bilibili";
            return "外部";
        }

        private static string GetStatusText(DownloadStatus status) => status switch
        {
            DownloadStatus.Pending => "待機中",
            DownloadStatus.Downloading => "DL中",
            DownloadStatus.WritingTags => "タグ書込中",
            DownloadStatus.Completed => "完了",
            DownloadStatus.Failed => "失敗",
            DownloadStatus.Skipped => "スキップ",
            DownloadStatus.Paused => "一時停止",
            _ => status.ToString()
        };

        private void btnOpenUrl_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_video.Url))
                Helpers.OpenUrl(_video.Url);
        }

        private void btnOpenFile_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_video.LocalFilePath) && System.IO.File.Exists(_video.LocalFilePath))
                Helpers.OpenFolderAndSelectFile(_video.LocalFilePath);
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            // タグ・メモのみ DB 反映 (他フィールドは表示専用)
            // タグはカンマ区切り正規化 (前後の空白除去 + 連続空文字削除)
            var rawTags = txtTags.Text ?? "";
            var normalizedTags = string.Join(",", rawTags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            _video.Tags = normalizedTags;
            _video.Memo = txtMemo.Text ?? "";
            _database.UpdateVideo(_video);
        }
    }
}
