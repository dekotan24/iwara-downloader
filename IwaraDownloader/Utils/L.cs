using System.Globalization;
using System.Resources;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 文言リソースへの短縮アクセサ。<c>L.T("Key")</c> で現在のUI言語の文言を返す。
    /// リソースは Resources/Strings.resx (日本語=ニュートラル) / Strings.en.resx / Strings.zh-Hans.resx。
    /// キーが見つからない場合はキー文字列をそのまま返す(翻訳漏れでもクラッシュさせない)。
    /// </summary>
    public static class L
    {
        private static readonly ResourceManager _rm =
            new("IwaraDownloader.Resources.Strings", typeof(L).Assembly);

        public static string T(string key)
            => _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string T(string key, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, T(key), args);
    }
}
