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
        static void Main(string[] args)
        {
            // UI言語の適用。フォーム生成前(=文言が読まれる前)に必ず行う
            ApplyUiLanguage();

            // 多重起動防止
            using var mutex = new Mutex(true, "IwaraDownloader_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    Utils.L.T("Msg_AlreadyRunning"),
                    "IwaraDownloader",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // アプリケーション設定 (WinFormsブリッジダイアログ群の視覚スタイル初期化。
            // DPI対応そのものはapp.manifestのdpiAwareness宣言でプロセス全体へ担保する)
            ApplicationConfiguration.Initialize();

            // 子プロセス管理用 Job Object を初期化
            // (親 (このプロセス) が死ぬと紐付けた子プロセス = Rust helper等も自動 Kill される)
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
            // WPF側(MainWindow)のDispatcherループはApplication.ThreadExceptionでは捕捉されないため個別に登録する
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += Dispatcher_UnhandledException;

            try
            {
                // Phase8c: メインウィンドウはWPF側(MainWindow)。DownloadManagerは1個だけここで生成し、
                // MainWindow(→MainViewModel→WebServerService)へ同じ参照を渡す。
                // System.Windows.Applicationオブジェクトは意図的に導入しない。
                // Window.ShowDialog()内部のメッセージポンプ(PushFrame)のみで
                // NotifyIcon/クリップボード監視/ブリッジダイアログ表示まで含めて動作することは
                // Phase8bの--wpf-mainデバッグ経路で検証済みのため、追加の複雑性を避ける。
                var sharedDownloadManager = new Services.DownloadManager();
                new Wpf.Views.MainWindow(sharedDownloadManager).ShowDialog();
            }
            finally
            {
                // ログサービス終了
                logger.Dispose();
            }
        }

        /// <summary>
        /// 設定に基づいてUI言語を適用する。
        /// "auto" はOSのUI言語に従う: 日本語OS→ja(ニュートラル)、中国語系OS→zh-Hans、それ以外→en。
        /// </summary>
        private static void ApplyUiLanguage()
        {
            try
            {
                var setting = Utils.SettingsManager.Instance.Settings.Language;
                var culture = setting switch
                {
                    "ja" => System.Globalization.CultureInfo.GetCultureInfo("ja"),
                    "en" => System.Globalization.CultureInfo.GetCultureInfo("en"),
                    "zh-Hans" => System.Globalization.CultureInfo.GetCultureInfo("zh-Hans"),
                    _ => ResolveAutoCulture()
                };
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch
            {
                // 言語適用に失敗してもニュートラル(日本語)で起動できるため無視する
            }
        }

        private static System.Globalization.CultureInfo ResolveAutoCulture()
        {
            var os = System.Globalization.CultureInfo.CurrentUICulture;
            var lang = os.TwoLetterISOLanguageName;
            return lang switch
            {
                "ja" => System.Globalization.CultureInfo.GetCultureInfo("ja"),
                "zh" => System.Globalization.CultureInfo.GetCultureInfo("zh-Hans"),
                _ => System.Globalization.CultureInfo.GetCultureInfo("en")
            };
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
        /// WPF(MainWindow)のDispatcherループでの未処理例外。
        /// Application.ThreadExceptionはWinFormsのメッセージループ専用でWPF側には効かないため、
        /// Phase8cで個別に登録する。
        /// </summary>
        private static void Dispatcher_UnhandledException(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowErrorAndLog(e.Exception);
            e.Handled = true;
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
