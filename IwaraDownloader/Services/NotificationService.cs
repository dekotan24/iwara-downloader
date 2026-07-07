using IwaraDownloader.Utils;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 通知サービス
    /// NotifyIconのバルーン通知を使用
    /// </summary>
    public class NotificationService
    {
        private static NotificationService? _instance;
        private static readonly object _lock = new();
        private NotifyIcon? _notifyIcon;

        /// <summary>シングルトンインスタンス</summary>
        public static NotificationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new NotificationService();
                    }
                }
                return _instance;
            }
        }

        private NotificationService() { }

        /// <summary>
        /// NotifyIconを設定(MainFormから呼び出し)
        /// </summary>
        public void SetNotifyIcon(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
        }

        /// <summary>
        /// トースト通知が有効かどうか
        /// </summary>
        private bool IsEnabled => SettingsManager.Instance.Settings.EnableToastNotification && _notifyIcon != null;

        /// <summary>
        /// ダウンロード完了通知
        /// </summary>
        public void NotifyDownloadComplete(string title, string filePath)
        {
            if (!IsEnabled) return;

            try
            {
                _notifyIcon!.ShowBalloonTip(
                    3000,
                    L.T("SvcNotificationService_D001"),
                    title,
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 新着動画検出通知
        /// </summary>
        public void NotifyNewVideosFound(string username, int count)
        {
            if (!IsEnabled) return;

            try
            {
                _notifyIcon!.ShowBalloonTip(
                    3000,
                    L.T("SvcNotificationService_D002"),
                    L.T("SvcNotificationService_D003", username, count),
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ダウンロードエラー通知
        /// </summary>
        public void NotifyDownloadError(string title, string errorMessage)
        {
            if (!IsEnabled) return;

            try
            {
                _notifyIcon!.ShowBalloonTip(
                    3000,
                    L.T("SvcNotificationService_D004"),
                    $"{title}: {errorMessage}",
                    ToolTipIcon.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 全ダウンロード完了通知
        /// </summary>
        public void NotifyAllDownloadsComplete(int successCount, int failedCount)
        {
            if (!IsEnabled) return;

            try
            {
                var message = failedCount > 0
                    ? $"完了: {successCount} 件, 失敗: {failedCount} 件"
                    : $"{successCount} 件のダウンロードが完了しました";

                _notifyIcon!.ShowBalloonTip(
                    3000,
                    L.T("SvcNotificationService_D001"),
                    message,
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 汎用通知
        /// </summary>
        public void ShowNotification(string title, string message)
        {
            if (!IsEnabled) return;

            try
            {
                _notifyIcon!.ShowBalloonTip(
                    3000,
                    title,
                    message,
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知エラー: {ex.Message}");
            }
        }
    }
}
