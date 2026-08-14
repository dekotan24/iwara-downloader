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

            // WPF移行 検証用の一時的な起動フラグ。Phase8カットオーバーまでに削除すること。
            if (args.Contains("--wpf-styleguide"))
            {
                new Wpf.Views.StyleGuideWindow().ShowDialog();
                return;
            }
            if (args.Contains("--wpf-about"))
            {
                new Wpf.Views.AboutWindow().ShowDialog();
                return;
            }
            if (args.Contains("--thumbcache-selftest"))
            {
                RunThumbnailCacheSelfTest();
                return;
            }
            if (args.Contains("--wpf-main"))
            {
                new Wpf.Views.MainWindow().ShowDialog();
                return;
            }

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
        /// WPF移行 Phase3 検証用の一時的な自己診断。ThumbnailCacheServiceのbyte[]化改修が
        /// 実際のキャッシュ済みファイルに対して正しく動くかを確認する。Phase8までに削除すること。
        /// </summary>
        private static void RunThumbnailCacheSelfTest()
        {
            var resultPath = Path.Combine(Path.GetTempPath(), "thumbcache_selftest_result.txt");
            var lines = new List<string>();
            try
            {
                var dir = Services.ThumbnailCacheService.ResolveCacheDir();
                var file = Directory.EnumerateFiles(dir, "*.jpg").FirstOrDefault();
                if (file == null)
                {
                    lines.Add("NO_CACHE_FILE_FOUND");
                }
                else
                {
                    var videoId = Path.GetFileNameWithoutExtension(file);
                    var svc = Services.ThumbnailCacheService.Instance;

                    var diskImg = svc.TryGetCached(videoId);
                    lines.Add($"TryGetCached (disk->mem promote): {(diskImg != null ? $"OK {diskImg.Width}x{diskImg.Height}" : "NULL")}");
                    diskImg?.Dispose();

                    var memImg = svc.TryGetMemoryCached(videoId);
                    lines.Add($"TryGetMemoryCached (mem hit): {(memImg != null ? $"OK {memImg.Width}x{memImg.Height}" : "NULL")}");
                    memImg?.Dispose();

                    var bytes = svc.TryGetMemoryCachedBytes(videoId);
                    lines.Add($"TryGetMemoryCachedBytes: {(bytes != null ? $"OK {bytes.Length} bytes" : "NULL")}");

                    if (bytes != null)
                    {
                        try
                        {
                            using var ms = new MemoryStream(bytes);
                            using var decoded = Image.FromStream(ms);
                            lines.Add($"WPF-side decode simulation (Image.FromStream on cached bytes): OK {decoded.Width}x{decoded.Height}");
                        }
                        catch (Exception ex)
                        {
                            lines.Add($"WPF-side decode simulation: FAILED {ex.Message}");
                        }
                    }

                    // 別のvideoIdでミス確認 (キャッシュに無いキーはnullを返すこと)
                    var missBytes = svc.TryGetMemoryCachedBytes("__nonexistent__");
                    lines.Add($"Miss case (non-cached id): {(missBytes == null ? "OK (null)" : "UNEXPECTED NON-NULL")}");
                }
            }
            catch (Exception ex)
            {
                lines.Add($"EXCEPTION: {ex}");
            }
            File.WriteAllLines(resultPath, lines, System.Text.Encoding.UTF8);
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
