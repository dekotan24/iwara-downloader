using System.Windows;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Wpf.Theme
{
    public enum AppTheme
    {
        Dark,
        Light,
    }

    /// <summary>
    /// WPFウィンドウのテーマ(ダーク/ライト)を管理する。
    ///
    /// System.Windows.Application を起動しない構成(WinFormsがホストのメッセージループを持つ)
    /// のため、App.xaml でのグローバルなテーマ適用は行わず、各Windowのコンストラクタで
    /// ThemeManager.Apply(this) を呼んでリソースをマージする方式を取る。
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme Current { get; private set; } = ParseTheme(SettingsManager.Instance.Settings.Theme);

        /// <summary>指定テーマのパレット+共通スタイルをマージした ResourceDictionary を作る</summary>
        public static ResourceDictionary BuildResources(AppTheme theme)
        {
            var dict = new ResourceDictionary();
            var paletteUri = theme == AppTheme.Dark
                ? "pack://application:,,,/IwaraDownloader;component/Wpf/Themes/Palette.Dark.xaml"
                : "pack://application:,,,/IwaraDownloader;component/Wpf/Themes/Palette.Light.xaml";

            dict.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(paletteUri) });
            dict.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/IwaraDownloader;component/Wpf/Themes/BaseStyles.xaml")
            });
            return dict;
        }

        /// <summary>ウィンドウに現在(または指定)テーマのリソースを適用する</summary>
        public static void Apply(Window window, AppTheme? theme = null)
        {
            var t = theme ?? Current;
            window.Resources.MergedDictionaries.Clear();
            foreach (ResourceDictionary rd in BuildResources(t).MergedDictionaries)
            {
                window.Resources.MergedDictionaries.Add(rd);
            }
        }

        /// <summary>テーマを切り替えて設定に保存する。既に開いているウィンドウへの再適用は呼び出し側の責務</summary>
        public static void SetTheme(AppTheme theme)
        {
            Current = theme;
            SettingsManager.Instance.Settings.Theme = theme == AppTheme.Dark ? "dark" : "light";
            SettingsManager.Instance.Save();
        }

        private static AppTheme ParseTheme(string value)
            => string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? AppTheme.Light : AppTheme.Dark;
    }
}
