namespace IwaraDownloader.Wpf.Theme
{
    /// <summary>
    /// 完全にお遊び要素のイースターエッグ。ダウンロード機能には一切関与しない。
    /// バージョン情報画面のアイコンを連打すると発火する(MainWindow側が見た目を演出する)。
    /// アプリを再起動すると元に戻る(設定には保存しない)。
    /// </summary>
    public static class PartyModeService
    {
        public static bool IsEnabled { get; private set; }

        public static event Action<bool>? Changed;

        public static void Toggle()
        {
            IsEnabled = !IsEnabled;
            Changed?.Invoke(IsEnabled);
        }
    }
}
