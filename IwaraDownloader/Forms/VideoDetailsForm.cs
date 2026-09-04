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
        private readonly IwaraApiService _api;
        private readonly DownloadManager _downloadManager;

        /// <summary>
        /// ローカルファイルの再マップ/マッピング解除が実行されたか (Save せずに閉じても DB は
        /// 既に更新済みなので、呼び出し側はダイアログの DialogResult とは別にこれを見て
        /// チャンネルツリー等を再描画する)。
        /// </summary>
        public bool Remapped { get; private set; }

        public VideoDetailsForm(VideoInfo video, DatabaseService database, IwaraApiService api, DownloadManager downloadManager)
        {
            _video = video;
            _database = database;
            _api = api;
            _downloadManager = downloadManager;
            InitializeComponent();
            Utils.Localizer.Apply(this);
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
            chkFavorite.Checked = _video.IsFavorite;

            // 開くボタンの有効/無効
            btnOpenUrl.Enabled = !string.IsNullOrEmpty(_video.Url);
            btnOpenFile.Enabled = !string.IsNullOrEmpty(_video.LocalFilePath)
                                && System.IO.File.Exists(_video.LocalFilePath);

            // マッピング解除: DB上でファイルが紐付いている場合のみ (実体の有無は問わない。
            // 実体が消えていて DB のパスだけ残っている状態こそ解除したいケースのため)。
            // DL処理中に横から切り離すと競合するため、その間は無効化。
            btnUnmapFile.Enabled = !string.IsNullOrEmpty(_video.LocalFilePath)
                                 && _video.Status != DownloadStatus.Downloading
                                 && _video.Status != DownloadStatus.WritingTags;
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
            if (url.Contains("nicovideo.jp")) return L.T("MainForm_SourceNico");
            if (url.Contains("bilibili.com")) return "Bilibili";
            return L.T("MainForm_SourceExternal");
        }

        private static string GetStatusText(DownloadStatus status) => status switch
        {
            DownloadStatus.Pending => L.T("VideoDetailsForm_D001"),
            DownloadStatus.Downloading => L.T("VideoDetailsForm_D002"),
            DownloadStatus.WritingTags => L.T("VideoDetailsForm_D003"),
            DownloadStatus.Completed => L.T("VideoDetailsForm_D004"),
            DownloadStatus.Failed => L.T("VideoDetailsForm_D005"),
            DownloadStatus.Skipped => L.T("VideoDetailsForm_D006"),
            DownloadStatus.Paused => L.T("VideoDetailsForm_D007"),
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

        /// <summary>
        /// ローカルファイルの再マップ。状態を問わず常時有効 (右クリックメニュー側と違い、
        /// 既に紐付いているファイルが間違っていた場合の修正手段としても使えるように)。
        /// </summary>
        private async void btnRemapFile_Click(object? sender, EventArgs e)
        {
            var result = await Utils.LocalFileMapHelper.MapAsync(this, _video, _api, _database, _downloadManager);
            if (result != Utils.LocalFileMapHelper.MapResult.Mapped) return;

            Remapped = true;
            // TitleMatchImporter が DB へは即反映済み。表示側も最新値に合わせる。
            PopulateFields();
        }

        /// <summary>
        /// ローカルファイルとの紐付けを解除する (マップ済みの動画のみ)。
        /// 実ファイルは削除しない。ステータスは Paused に戻る。
        /// </summary>
        private void btnUnmapFile_Click(object? sender, EventArgs e)
        {
            var result = Utils.LocalFileMapHelper.Unmap(this, _video, _database, _downloadManager);
            if (result != Utils.LocalFileMapHelper.MapResult.Mapped) return;

            Remapped = true;
            PopulateFields();
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
            _video.IsFavorite = chkFavorite.Checked;
            _database.UpdateVideoTagsMemoFavorite(_video.Id, _video.Tags, _video.Memo, _video.IsFavorite);
        }
    }
}
