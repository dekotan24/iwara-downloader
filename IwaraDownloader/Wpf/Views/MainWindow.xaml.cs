using System.Linq;
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

        private void VideoList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedVideos = ((System.Windows.Controls.ListView)sender).SelectedItems
                    .Cast<VideoListItemViewModel>().ToList();
            }
        }

        private void ChannelTree_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            (DataContext as MainViewModel)?.RefreshChannelContextMenuState();
        }

        private void VideoColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e.OriginalSource is not System.Windows.Controls.GridViewColumnHeader header || header.Column == null) return;
            var listView = (System.Windows.Controls.ListView)sender;
            var gridView = (System.Windows.Controls.GridView)listView.View;
            int index = gridView.Columns.IndexOf(header.Column);
            if (index >= 0)
            {
                (DataContext as MainViewModel)?.SortVideosByColumn(index);
            }
        }
    }
}
