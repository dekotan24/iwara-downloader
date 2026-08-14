using System.Windows;
using IwaraDownloader.Wpf.Theme;
using IwaraDownloader.Wpf.ViewModels;

namespace IwaraDownloader.Wpf.Views
{
    /// <summary>
    /// Phase1: WPF移行基盤(テーマ・i18n・MVVM)の動作確認用の開発ウィンドウ。
    /// 最終成果物には含めない想定(Phase8カットオーバー前に削除するか、開発者ツールとして残すか要検討)。
    /// </summary>
    public partial class StyleGuideWindow : Window
    {
        private readonly StyleGuideViewModel _viewModel;

        public StyleGuideWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(this);

            _viewModel = new StyleGuideViewModel();
            _viewModel.ThemeToggled += () => ThemeManager.Apply(this, _viewModel.CurrentTheme);
            DataContext = _viewModel;
        }
    }
}
