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
        /// 設定を保存する
        /// </summary>
        public void Save()
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
                File.WriteAllText(AppSettings.ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// iwaraパスワードを設定（暗号化して保存）
        /// </summary>
        public void SetIwaraPassword(string password)
        {
            Settings.IwaraPasswordEncrypted = CryptoHelper.Encrypt(password);
        }

        /// <summary>
        /// iwaraパスワードを取得（復号化して取得）
        /// </summary>
        public string GetIwaraPassword()
        {
            return CryptoHelper.Decrypt(Settings.IwaraPasswordEncrypted);
        }

        /// <summary>
        /// 設定をJSON文字列としてエクスポート（パスワードは除外）
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
                PythonPath = Settings.PythonPath,
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
                EnableExponentialBackoff = Settings.EnableExponentialBackoff
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(exportSettings, options);
        }

        /// <summary>
        /// JSONから設定をインポート
        /// </summary>
        public void ImportFromJson(string json)
        {
            var importedSettings = JsonSerializer.Deserialize<AppSettings>(json);
            if (importedSettings != null)
            {
                // パスワードは維持
                var currentPassword = Settings.IwaraPasswordEncrypted;
                Settings = importedSettings;
                Settings.IwaraPasswordEncrypted = currentPassword;
                Save();
            }
        }
    }
}
