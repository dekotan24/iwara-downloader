using CommunityToolkit.Mvvm.ComponentModel;
using IwaraDownloader.Models;
using IwaraDownloader.Wpf.Models;
using IwaraDownloader.Wpf.Theme;
using Brush = System.Windows.Media.Brush;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// チャンネルツリーの1行分。固定ノード(全ての動画/お気に入り等)と購読チャンネル行の両方を表す。
    /// 旧WinForms版は単純な単層リスト(親子ネストなし)だったため、WPF側も同様にフラットな
    /// コレクションとして扱う。
    /// </summary>
    public partial class ChannelTreeNodeViewModel : ObservableObject
    {
        public TreeNodeKind Kind { get; init; }

        /// <summary>Kind == Channel の場合のみ使用</summary>
        public SubscribedUser? Channel { get; init; }

        [ObservableProperty]
        private string _text = "";

        /// <summary>
        /// テーマ解決済みのBrush。ThemeManager.GetBrush("Brush.Success")のように構築時点で解決する。
        /// ツリーは状態変化のたびに丸ごと再構築される前提のため、DynamicResourceのような
        /// 継続追従は不要(テーマ切替時もツリー再構築で最新色になる)。
        /// </summary>
        [ObservableProperty]
        private Brush _foreground = ThemeManager.GetBrush("Brush.Text");

        [ObservableProperty]
        private bool _isBold;
    }
}
