using System.Windows;
using IwaraDownloader.Wpf.Theme;
using IwaraDownloader.Wpf.ViewModels;

namespace IwaraDownloader.Wpf.Views
{
    /// <summary>
    /// Phase4: WPF版メインウィンドウ。Phase4aは骨格のみ。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(this);
            var viewModel = new MainViewModel { OwnerWindow = this };
            DataContext = viewModel;
            Closed += (_, _) => viewModel.Dispose();
        }

        private void VideoList_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            (DataContext as MainViewModel)?.RefreshVideoContextMenuState();
        }

        private void ChannelTree_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            (DataContext as MainViewModel)?.RefreshChannelContextMenuState();
        }
    }
}
