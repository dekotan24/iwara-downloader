using System.Text.Json;
using IwaraDownloader.Models;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 設定マネージャー
    /// </summary>
    public class SettingsManager
    {
        private static SettingsManager? _instance;
        private static readonly object _lock = new();

        /// <summary>現在の設定</summary>
        public AppSettings Settings { get; private set; }

        /// <summary>メディアサーバーのユーザー名/パスワードが変更された (既存セッション失効用)</summary>
        public event EventHandler? WebServerCredentialsChanged;

        /// <summary>シングルトンインスタンス</summary>
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsManager();
                    }
                }
                return _instance;
            }
        }

        private SettingsManager()
        {
            Settings = Load();
        }

        /// <summary>
        /// 設定を読み込む
        /// </summary>
        public AppSettings Load()
        {
            try
            {
                if (File.Exists(AppSettings.ConfigFilePath))
                {
                    var json = File.ReadAllText(AppSettings.ConfigFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        Settings = settings;
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            Settings = AppSettings.CreateDefault();
            return Settings;
        }

        /// <summary>
        /// 設定を保存する。
        /// 書き込み途中でプロセスが強制終了されても settings.json が破損しないよう、
        /// 一時ファイルに書いてからアトミックに差し替える。
        /// (バックグラウンドのキャッシュ移行などからも呼ばれるため lock で直列化)
        /// </summary>
        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(AppSettings.ConfigFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(Settings, options);
                    var tmpPath = AppSettings.ConfigFilePath + ".tmp";
                    File.WriteAllText(tmpPath, json);
                    File.Move(tmpPath, AppSettings.ConfigFilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// iwaraパスワードを設定(暗号化して保存)
        /// </summary>
        public void SetIwaraPassword(string password)
        {
            Settings.IwaraPasswordEncrypted = CryptoHelper.Encrypt(password);
        }

        /// <summary>
        /// iwaraパスワードを取得(復号化して取得)
        /// </summary>
        public string GetIwaraPassword()
        {
            return CryptoHelper.Decrypt(Settings.IwaraPasswordEncrypted);
        }

        public void SetWebServerPassword(string password)
        {
            SetWebServerCredentials(Settings.WebServerUsername, password);
        }

        /// <summary>
        /// メディアサーバーのユーザー名とパスワードを設定する。
        /// どちらかが変わった場合は WebServerCredentialsChanged を発火し、ログイン済みセッションを失効させる。
        /// </summary>
        public void SetWebServerCredentials(string username, string password)
        {
            var oldUser = Settings.WebServerUsername;
            var oldPass = GetWebServerPassword();
            bool hadEncrypted = !string.IsNullOrEmpty(Settings.WebServerPasswordEncrypted);

            Settings.WebServerUsername = username;
            Settings.WebServerPasswordEncrypted = string.IsNullOrEmpty(password)
                ? string.Empty
                : CryptoHelper.Encrypt(password);

            // 復号できなかった旧パスワード (hadEncrypted && oldPass=="") からの再設定も変更扱い
            bool changed = !string.Equals(oldUser, username, StringComparison.Ordinal)
                || !string.Equals(oldPass, password ?? string.Empty, StringComparison.Ordinal)
                || (hadEncrypted && string.IsNullOrEmpty(oldPass));
            if (changed)
                WebServerCredentialsChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetWebServerPassword()
        {
            return CryptoHelper.Decrypt(Settings.WebServerPasswordEncrypted);
        }

        /// <summary>
        /// 設定をJSON文字列としてエクスポート(パスワードは除外)
        /// </summary>
        public string ExportToJson()
        {
            var exportSettings = new AppSettings
            {
                DownloadFolder = Settings.DownloadFolder,
                DefaultQuality = Settings.DefaultQuality,
                MaxConcurrentDownloads = Settings.MaxConcurrentDownloads,
                CheckIntervalMinutes = Settings.CheckIntervalMinutes,
                MaxRetryCount = Settings.MaxRetryCount,
                EnableToastNotification = Settings.EnableToastNotification,
                StartMinimized = Settings.StartMinimized,
                AutoCheckEnabled = Settings.AutoCheckEnabled,
                AutoDownloadOnCheck = Settings.AutoDownloadOnCheck,
                DownloadExternalVideosDefault = Settings.DownloadExternalVideosDefault,
                PythonPath = Settings.PythonPath,
                YtDlpPath = Settings.YtDlpPath,
                IwaraEmail = Settings.IwaraEmail,
                IwaraUsername = Settings.IwaraUsername,
                // パスワードは除外
                IwaraPasswordEncrypted = string.Empty,
                // レート制限設定もエクスポート
                ApiRequestDelayMs = Settings.ApiRequestDelayMs,
                DownloadDelayMs = Settings.DownloadDelayMs,
                ChannelCheckDelayMs = Settings.ChannelCheckDelayMs,
                PageFetchDelayMs = Settings.PageFetchDelayMs,
                RateLimitBaseDelayMs = Settings.RateLimitBaseDelayMs,
                RateLimitMaxDelayMs = Settings.RateLimitMaxDelayMs,
                EnableExponentialBackoff = Settings.EnableExponentialBackoff,
                // ファイル/通知/起動
                FilenameTemplate = Settings.FilenameTemplate,
                MinimizeToTray = Settings.MinimizeToTray,
                SaveMetadata = Settings.SaveMetadata,
                CheckUpdateOnStartup = Settings.CheckUpdateOnStartup,
                ResumeDownloadsOnStartup = Settings.ResumeDownloadsOnStartup,
                EnableCompletionSound = Settings.EnableCompletionSound,
                CompletionSoundPath = Settings.CompletionSoundPath,
                EnableErrorSound = Settings.EnableErrorSound,
                ErrorSoundPath = Settings.ErrorSoundPath,
                // Webメディアサーバー (パスワードは除外)
                WebServerPort = Settings.WebServerPort,
                WebServerBindAll = Settings.WebServerBindAll,
                WebServerUsername = Settings.WebServerUsername,
                WebServerAutoStart = Settings.WebServerAutoStart
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(exportSettings, options);
        }

        /// <summary>
        /// JSONから設定をインポート
        /// </summary>
        public void ImportFromJson(string json)
        {
            var i = JsonSerializer.Deserialize<AppSettings>(json);
            if (i == null) return;

            // 設定オブジェクトを丸ごと差し替えると、エクスポート対象外の項目 (言語/テーマ/サムネ保存先 等) が
            // 既定値に戻ってしまう。また PythonPath / YtDlpPath は実行ファイルの指定なので、
            // 配布された設定ファイル経由で任意のプログラムを実行させられないよう取り込まない。
            // → ExportToJson と同じ項目だけを現在の設定へ上書きする。
            var s = Settings;
            s.DownloadFolder = i.DownloadFolder;
            s.DefaultQuality = i.DefaultQuality;
            s.MaxConcurrentDownloads = i.MaxConcurrentDownloads;
            s.CheckIntervalMinutes = i.CheckIntervalMinutes;
            s.MaxRetryCount = i.MaxRetryCount;
            s.EnableToastNotification = i.EnableToastNotification;
            s.StartMinimized = i.StartMinimized;
            s.AutoCheckEnabled = i.AutoCheckEnabled;
            s.AutoDownloadOnCheck = i.AutoDownloadOnCheck;
            s.DownloadExternalVideosDefault = i.DownloadExternalVideosDefault;
            s.IwaraEmail = i.IwaraEmail;
            s.IwaraUsername = i.IwaraUsername;
            s.ApiRequestDelayMs = i.ApiRequestDelayMs;
            s.DownloadDelayMs = i.DownloadDelayMs;
            s.ChannelCheckDelayMs = i.ChannelCheckDelayMs;
            s.PageFetchDelayMs = i.PageFetchDelayMs;
            s.RateLimitBaseDelayMs = i.RateLimitBaseDelayMs;
            s.RateLimitMaxDelayMs = i.RateLimitMaxDelayMs;
            s.EnableExponentialBackoff = i.EnableExponentialBackoff;
            s.FilenameTemplate = i.FilenameTemplate;
            s.MinimizeToTray = i.MinimizeToTray;
            s.SaveMetadata = i.SaveMetadata;
            s.CheckUpdateOnStartup = i.CheckUpdateOnStartup;
            s.ResumeDownloadsOnStartup = i.ResumeDownloadsOnStartup;
            s.EnableCompletionSound = i.EnableCompletionSound;
            s.CompletionSoundPath = i.CompletionSoundPath;
            s.EnableErrorSound = i.EnableErrorSound;
            s.ErrorSoundPath = i.ErrorSoundPath;
            s.WebServerPort = i.WebServerPort;
            s.WebServerBindAll = i.WebServerBindAll;
            s.WebServerAutoStart = i.WebServerAutoStart;
            if (!string.Equals(s.WebServerUsername, i.WebServerUsername, StringComparison.Ordinal))
            {
                s.WebServerUsername = i.WebServerUsername;
                WebServerCredentialsChanged?.Invoke(this, EventArgs.Empty);
            }
            Save();
        }
    }
}
