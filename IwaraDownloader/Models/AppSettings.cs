using System.Text.Json.Serialization;

namespace IwaraDownloader.Models
{
    /// <summary>
    /// アプリケーション設定
    /// </summary>
    public class AppSettings
    {
        /// <summary>ダウンロード先フォルダ</summary>
        public string DownloadFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Iwara");

        /// <summary>デフォルト画質</summary>
        public VideoQuality DefaultQuality { get; set; } = VideoQuality.Source;

        /// <summary>同時ダウンロード数(1-3)</summary>
        public int MaxConcurrentDownloads { get; set; } = 2;

        /// <summary>新着チェック間隔(分)</summary>
        public int CheckIntervalMinutes { get; set; } = 60;

        /// <summary>リトライ回数</summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>空き容量下限(GB)。DL先ドライブの空きがこれ未満ならDLを開始しない。0=チェック無効</summary>
        public int MinFreeSpaceGb { get; set; } = 1;

        #region Rate Limiting Settings

        /// <summary>APIリクエスト間隔(ミリ秒)- 動画情報取得等</summary>
        public int ApiRequestDelayMs { get; set; } = 1000;

        /// <summary>ダウンロード間隔(ミリ秒)- 動画DL完了後の待機</summary>
        public int DownloadDelayMs { get; set; } = 3000;

        /// <summary>チャンネル巡回間隔(ミリ秒)- 次のチャンネルチェックまでの待機</summary>
        public int ChannelCheckDelayMs { get; set; } = 5000;

        /// <summary>429エラー時の基本待機時間(ミリ秒)</summary>
        public int RateLimitBaseDelayMs { get; set; } = 30000;

        /// <summary>429エラー時の最大待機時間(ミリ秒)</summary>
        public int RateLimitMaxDelayMs { get; set; } = 300000;

        /// <summary>エクスポネンシャルバックオフを有効にする</summary>
        public bool EnableExponentialBackoff { get; set; } = true;

        /// <summary>ページ取得間隔(ミリ秒)- 動画一覧のページング時</summary>
        public int PageFetchDelayMs { get; set; } = 500;

        #endregion

        /// <summary>トースト通知を有効にする</summary>
        public bool EnableToastNotification { get; set; } = true;

        /// <summary>クリップボード監視を有効にする (iwara URL を自動でキュー追加)</summary>
        public bool EnableClipboardMonitor { get; set; } = false;

        /// <summary>NSFW フィルタモード: 0=全部 / 1=SFWのみ / 2=NSFWのみ</summary>
        public int NsfwFilterMode { get; set; } = 0;

        /// <summary>動画リストの表示モード: 0=詳細(列表示) / 1=サムネ(タイル)</summary>
        public int VideoListViewMode { get; set; } = 0;

        /// <summary>サムネイルキャッシュの保存先: 0=アプリ設定フォルダ(Roaming) / 1=ダウンロード先フォルダ配下のthumbs</summary>
        public int ThumbnailCacheLocation { get; set; } = 0;

        /// <summary>
        /// 前回実際に使用していたサムネイルキャッシュフォルダ。
        /// 現在の解決結果と異なる場合、移行が中断された (または設定が外部で変更された)
        /// とみなして起動時に残りを自動移行する。
        /// </summary>
        public string LastThumbnailCacheDir { get; set; } = string.Empty;

        /// <summary>起動時に最小化</summary>
        public bool StartMinimized { get; set; } = false;

        /// <summary>閉じるボタンでタスクトレイに最小化</summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>自動チェックを有効にする</summary>
        public bool AutoCheckEnabled { get; set; } = true;

        /// <summary>チェック時に自動でダウンロードを開始</summary>
        public bool AutoDownloadOnCheck { get; set; } = true;

        /// <summary>iwara外動画(YouTube埋め込み等)をデフォルトでDLするか(チャンネル個別設定で上書き可能)</summary>
        public bool DownloadExternalVideosDefault { get; set; } = false;

        /// <summary>yt-dlp実行パス(空または"yt-dlp"でPATH参照)。未インストールの場合は自動で pip install を試行</summary>
        public string YtDlpPath { get; set; } = "yt-dlp";

        #region Sound Settings

        /// <summary>ダウンロード完了音を有効にする</summary>
        public bool EnableCompletionSound { get; set; } = false;

        /// <summary>完了音のファイルパス(空の場合はシステム音)</summary>
        public string CompletionSoundPath { get; set; } = string.Empty;

        /// <summary>ダウンロードエラー音を有効にする</summary>
        public bool EnableErrorSound { get; set; } = false;

        /// <summary>エラー音のファイルパス(空の場合はシステム音)</summary>
        public string ErrorSoundPath { get; set; } = string.Empty;

        #endregion

        #region Filename Template

        /// <summary>ファイル名テンプレート</summary>
        public string FilenameTemplate { get; set; } = "{id}_{title}";

        #endregion

        #region Metadata Settings

        /// <summary>動画と一緒にメタデータ(.json)を保存</summary>
        public bool SaveMetadata { get; set; } = false;

        #endregion

        #region Update Settings

        /// <summary>起動時に更新チェックを行う</summary>
        public bool CheckUpdateOnStartup { get; set; } = true;

        #endregion

        #region Startup Settings

        /// <summary>起動時に未完了のダウンロードを再開</summary>
        public bool ResumeDownloadsOnStartup { get; set; } = true;

        #endregion

        /// <summary>Pythonの実行パス</summary>
        public string PythonPath { get; set; } = "python";

        /// <summary>iwaraメールアドレス</summary>
        public string IwaraEmail { get; set; } = string.Empty;

        /// <summary>iwaraユーザー名(表示用)</summary>
        public string IwaraUsername { get; set; } = string.Empty;

        /// <summary>iwaraパスワード(暗号化済み)</summary>
        public string IwaraPasswordEncrypted { get; set; } = string.Empty;

        #region Web Media Server Settings

        /// <summary>Webメディアサーバーを有効にする</summary>
        public bool WebServerEnabled { get; set; } = false;

        /// <summary>Webサーバーのポート番号</summary>
        public int WebServerPort { get; set; } = 7860;

        /// <summary>LAN内の他デバイスからのアクセスを許可 (true=0.0.0.0, false=127.0.0.1)</summary>
        public bool WebServerBindAll { get; set; } = true;

        /// <summary>Webサーバーのログインユーザー名</summary>
        public string WebServerUsername { get; set; } = "admin";

        /// <summary>Webサーバーのログインパスワード(暗号化済み)</summary>
        public string WebServerPasswordEncrypted { get; set; } = string.Empty;

        /// <summary>アプリ起動時にWebサーバーを自動開始</summary>
        public bool WebServerAutoStart { get; set; } = false;

        #endregion

        /// <summary>設定ファイルのパス</summary>
        [JsonIgnore]
        public static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IwaraDownloader",
            "settings.json");

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }
    }
}
