namespace IwaraDownloader.Models
{
    /// <summary>
    /// 購読ユーザー情報
    /// </summary>
    public class SubscribedUser
    {
        /// <summary>DB上のID</summary>
        public int Id { get; set; }

        /// <summary>iwaraユーザーID</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>ユーザー名(表示名)</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>プロフィールURL</summary>
        public string ProfileUrl { get; set; } = string.Empty;

        /// <summary>サムネイルURL</summary>
        public string ThumbnailUrl { get; set; } = string.Empty;

        /// <summary>ローカルサムネイルパス</summary>
        public string LocalThumbnailPath { get; set; } = string.Empty;

        /// <summary>登録日時</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>最終チェック日時</summary>
        public DateTime? LastCheckedAt { get; set; }

        /// <summary>ダウンロード済み動画数</summary>
        public int DownloadedCount { get; set; }

        /// <summary>総動画数</summary>
        public int TotalVideoCount { get; set; }

        /// <summary>有効/無効</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>カスタム保存先フォルダ(空の場合はデフォルトを使用)</summary>
        public string CustomSavePath { get; set; } = string.Empty;

        /// <summary>iwara外動画(YouTube埋め込み等)のDL設定オーバーライド。null=グローバル設定継承、true=DLする、false=DLしない</summary>
        public bool? DownloadExternalVideosOverride { get; set; }

        /// <summary>所属サイト ("www.iwara.tv" or "www.iwara.ai")。空文字なら iwara.tv 扱い</summary>
        public string Site { get; set; } = string.Empty;

        /// <summary>
        /// グローバル設定を加味した実効値で「iwara外動画をDLするか」を返す
        /// </summary>
        public bool ResolveDownloadExternal(bool globalDefault)
            => DownloadExternalVideosOverride ?? globalDefault;

        /// <summary>
        /// 保存先フォルダを取得(カスタムがあればカスタム、なければデフォルト+ユーザー名)
        /// </summary>
        public string GetSavePath(string defaultSavePath)
        {
            if (!string.IsNullOrWhiteSpace(CustomSavePath))
                return CustomSavePath;
            
            return Path.Combine(defaultSavePath, Username);
        }

        /// <summary>
        /// 表示用の文字列
        /// </summary>
        public override string ToString()
        {
            return $"{Username} ({DownloadedCount}/{TotalVideoCount})";
        }
    }
}
