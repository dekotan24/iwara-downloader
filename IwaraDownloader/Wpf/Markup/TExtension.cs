using System.Windows.Markup;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Wpf.Markup
{
    /// <summary>
    /// XAMLから既存の文言リソース(Strings.resx / Strings.en.resx / Strings.zh-Hans.resx)を
    /// 参照するためのMarkupExtension。使い方: xmlns:loc="clr-namespace:IwaraDownloader.Wpf.Markup"
    /// のうえで Text="{loc:T MainForm_D177}"。
    ///
    /// 内部実装はWinForms側と同じ Utils.L.T(key) をそのまま呼ぶ(リソース定義を二重管理しない)。
    /// 言語切替は既存仕様と同様アプリ再起動で反映する前提のため、ここでは値を1回評価するだけで
    /// INotifyPropertyChanged等の動的更新は行わない。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class TExtension : MarkupExtension
    {
        /// <summary>Strings.resx のキー</summary>
        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public TExtension() { }

        public TExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;
            return L.T(Key);
        }
    }
}
