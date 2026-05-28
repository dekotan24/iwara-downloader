using IwaraDownloader.Forms;
using IwaraDownloader.Services;

namespace IwaraDownloader
{
    internal static class Program
    {
        /// <summary>
        /// アプリケーションのメインエントリーポイント
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 多重起動防止
            using var mutex = new Mutex(true, "IwaraDownloader_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "IwaraDownloaderは既に起動しています。",
                    "IwaraDownloader",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // アプリケーション設定
            ApplicationConfiguration.Initialize();

            // 子プロセス管理用 Job Object を初期化
            // (親 (このプロセス) が死ぬと紐付けた子プロセス = Python ヘルパー等も自動 Kill される)
            IwaraDownloader.Utils.ChildProcessJob.EnsureInitialized();

            // ログサービス初期化
            var logger = LoggingService.Instance;
            logger.Info("Application starting...");

            // 未処理例外のハンドリング
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            // fire-and-forget Task 内の未捕捉例外 (await されない _ = Task.Run など)
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                // スプラッシュスクリーンを表示
                SplashForm.ShowSplash();
                SplashForm.UpdateStatus("初期化中...", 0);

                // メインフォームを起動
                Application.Run(new MainForm());
            }
            finally
            {
                // スプラッシュが残っていれば閉じる
                SplashForm.CloseSplash();

                // ログサービス終了
                logger.Dispose();
            }
        }

        /// <summary>
        /// UIスレッドでの未処理例外
        /// </summary>
        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowErrorAndLog(e.Exception);
        }

        /// <summary>
        /// 非UIスレッドでの未処理例外
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowErrorAndLog(ex);
            }
        }

        /// <summary>
        /// fire-and-forget Task の未観測例外 (GC されるまで気付かれない)。
        /// アプリは落とさず、ログに残してダイアログは出さない (頻度未知のため UI 連発回避)。
        /// </summary>
        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                LoggingService.Instance.Error("Unobserved task exception", e.Exception);
            }
            catch { }
            e.SetObserved();
        }

        /// <summary>
        /// エラーを表示してログに記録
        /// </summary>
        private static void ShowErrorAndLog(Exception ex)
        {
            try
            {
                // LoggingServiceでエラーを記録
                LoggingService.Instance.Fatal("Unhandled exception", ex);
            }
            catch
            {
                // ログ書き込み失敗時は旧形式でバックアップ
                try
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "IwaraDownloader",
                        "error.log");

                    var logDir = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                    File.AppendAllText(logPath, logMessage);
                }
                catch { }
            }

            MessageBox.Show(
                $"予期しないエラーが発生しました。\n\n{ex.Message}",
                "エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
