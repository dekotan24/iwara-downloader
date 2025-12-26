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

        /// <summary>同時ダウンロード数（1-3）</summary>
        public int MaxConcurrentDownloads { get; set; } = 2;

        /// <summary>新着チェック間隔（分）</summary>
        public int CheckIntervalMinutes { get; set; } = 60;

        /// <summary>リトライ回数</summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>トースト通知を有効にする</summary>
        public bool EnableToastNotification { get; set; } = true;

        /// <summary>起動時に最小化</summary>
        public bool StartMinimized { get; set; } = false;

        /// <summary>閉じるボタンでタスクトレイに最小化</summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>自動チェックを有効にする</summary>
        public bool AutoCheckEnabled { get; set; } = true;

        /// <summary>チェック時に自動でダウンロードを開始</summary>
        public bool AutoDownloadOnCheck { get; set; } = true;

        /// <summary>iwaraユーザー名</summary>
        public string IwaraUsername { get; set; } = string.Empty;

        /// <summary>iwaraパスワード（暗号化済み）</summary>
        public string IwaraPasswordEncrypted { get; set; } = string.Empty;

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
