namespace IwaraDownloader.Models
{
    /// <summary>
    /// 動画情報
    /// </summary>
    public class VideoInfo
    {
        /// <summary>DB上のID</summary>
        public int Id { get; set; }

        /// <summary>iwara動画ID</summary>
        public string VideoId { get; set; } = string.Empty;

        /// <summary>埋め込み元URL(YouTubeなどiwara外動画の場合に設定)。通常iwara動画では空</summary>
        public string EmbedUrl { get; set; } = string.Empty;

        /// <summary>iwara外動画(YouTube埋め込みなど)かどうか</summary>
        public bool IsExternal => !string.IsNullOrEmpty(EmbedUrl);

        /// <summary>動画タイトル</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>動画URL</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>サムネイルURL</summary>
        public string ThumbnailUrl { get; set; } = string.Empty;

        /// <summary>ローカルサムネイルパス</summary>
        public string LocalThumbnailPath { get; set; } = string.Empty;

        /// <summary>投稿者ユーザーID</summary>
        public string AuthorUserId { get; set; } = string.Empty;

        /// <summary>投稿者ユーザー名</summary>
        public string AuthorUsername { get; set; } = string.Empty;

        /// <summary>動画の長さ(秒)</summary>
        public int DurationSeconds { get; set; }

        /// <summary>投稿日時</summary>
        public DateTime? PostedAt { get; set; }

        /// <summary>ローカルファイルパス</summary>
        public string LocalFilePath { get; set; } = string.Empty;

        /// <summary>
        /// iwara が割り当てる動画ファイルの UUID (例: 0b01dd60-8826-4a9f-a39b-7cd012ae7883)。
        /// DL 済みファイルの mp4 カスタムタグにも同じ値が書き込まれる。
        /// iwara 側で動画マスターが差し替わると変わるため、再DLの判定指標になる。
        /// </summary>
        public string FileUuid { get; set; } = string.Empty;

        /// <summary>ファイルサイズ(バイト)</summary>
        public long FileSize { get; set; }

        /// <summary>ダウンロードステータス</summary>
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;

        /// <summary>ダウンロード日時</summary>
        public DateTime? DownloadedAt { get; set; }

        /// <summary>関連する購読ユーザーID(個別DLの場合はnull)</summary>
        public int? SubscribedUserId { get; set; }

        /// <summary>リトライ回数</summary>
        public int RetryCount { get; set; }

        /// <summary>最後のエラーメッセージ</summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>登録日時</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>タグ(カンマ区切り)</summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>ユーザーメモ</summary>
        public string Memo { get; set; } = string.Empty;

        /// <summary>iwara API の rating 値 ("general" / "ecchi" / 空="未取得"扱い)</summary>
        public string Rating { get; set; } = string.Empty;

        /// <summary>
        /// 所属サイト ("www.iwara.tv" or "www.iwara.ai")。
        /// 空文字なら旧データ扱いで iwara.tv とみなす。API 呼び出し時に X-Site ヘッダーへ載せる。
        /// </summary>
        public string Site { get; set; } = string.Empty;

        /// <summary>お気に入りフラグ</summary>
        public bool IsFavorite { get; set; }

        /// <summary>サムネイル取得ステータス (0=未試行, 1=キャッシュ済, 2=失敗)</summary>
        public int ThumbnailStatus { get; set; }

        /// <summary>
        /// 動画の長さを表示用にフォーマット
        /// </summary>
        public string DurationFormatted
        {
            get
            {
                var ts = TimeSpan.FromSeconds(DurationSeconds);
                return ts.Hours > 0
                    ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                    : $"{ts.Minutes}:{ts.Seconds:D2}";
            }
        }

        /// <summary>
        /// ファイルサイズを表示用にフォーマット
        /// </summary>
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize <= 0) return "-";
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                double size = FileSize;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// ダウンロード済みかどうか
        /// </summary>
        public bool IsDownloaded => Status == DownloadStatus.Completed && !string.IsNullOrEmpty(LocalFilePath);

        /// <summary>
        /// ローカルファイルが存在するか
        /// </summary>
        public bool LocalFileExists => !string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath);

        /// <summary>
        /// タグリストを取得
        /// </summary>
        public List<string> TagList => string.IsNullOrEmpty(Tags) 
            ? new List<string>() 
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        /// <summary>
        /// タグを追加
        /// </summary>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            var tags = TagList;
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag.Trim());
                Tags = string.Join(",", tags);
            }
        }

        /// <summary>
        /// タグを削除
        /// </summary>
        public void RemoveTag(string tag)
        {
            var tags = TagList;
            tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
            Tags = string.Join(",", tags);
        }

        /// <summary>
        /// タグがあるかチェック
        /// </summary>
        public bool HasTag(string tag) => TagList.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }
}
