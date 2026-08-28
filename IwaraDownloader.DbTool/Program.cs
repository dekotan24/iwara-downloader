using System.Diagnostics;
using IwaraDownloader.Forms;
using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.DbTool
{
    /// <summary>
    /// DB操作ツール(上級者向け)の独立エントリーポイント。
    /// IwaraDownloader本体とは別プロセスで動く。DownloadManagerを共有できないため、
    /// 本体が起動中のまま使うと裏のDL処理とDB書き込みが競合しうる(メモリ上のタスク状態と
    /// DBの内容がズレる)。そのため起動時に本体プロセスの有無を確認し、起動中なら警告する。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplyUiLanguage();

            // Application.EnableVisualStyles() (ApplicationConfiguration.Initialize()内で呼ばれる) は
            // プロセス内で最初のコモンコントロールが生成される前に呼ばないと効かない。
            // MessageBox.Showもコモンコントロールを使うため、二重起動確認より必ず先に呼ぶこと
            // (逆順にすると、この確認ダイアログだけテーマ無効の古いスタイルで表示されてしまう)。
            ApplicationConfiguration.Initialize();

            if (!ConfirmProceedIfDownloaderRunning())
                return;

            var logger = LoggingService.Instance;
            logger.Info("DbTool starting...");

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
                logger.Error($"Unhandled UI exception: {e.Exception.Message}", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                logger.Error($"Unhandled exception: {(e.ExceptionObject as Exception)?.Message}", e.ExceptionObject as Exception);

            try
            {
                Application.Run(new DatabaseToolForm());
            }
            finally
            {
                logger.Dispose();
            }
        }

        /// <summary>IwaraDownloader本体が起動中なら警告し、続行するか確認する。</summary>
        private static bool ConfirmProceedIfDownloaderRunning()
        {
            var running = Process.GetProcessesByName("IwaraDownloader");
            if (running.Length == 0) return true;

            var result = MessageBox.Show(
                L.T("DbTool_Msg_DownloaderRunning"),
                L.T("DbTool_Msg_DownloaderRunningTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return result == DialogResult.Yes;
        }

        /// <summary>
        /// downloader本体がsettings.jsonを作成済みである前提で、そこのLanguage設定に従う。
        /// ファイルが無ければ(セットアップ未完了/削除済み等)OS言語へフォールバックする。
        /// SettingsManager.Load()自体はファイル不在時にAppSettings.CreateDefault()
        /// (Language="auto")を返すだけでファイルを新規作成する副作用は無いが、
        /// ここでは意図を明示するためファイルの有無を先に見る。
        /// </summary>
        private static void ApplyUiLanguage()
        {
            try
            {
                var setting = File.Exists(AppSettings.ConfigFilePath)
                    ? SettingsManager.Instance.Settings.Language
                    : "auto";
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
                _ => System.Globalization.CultureInfo.GetCultureInfo("en"),
            };
        }
    }
}
