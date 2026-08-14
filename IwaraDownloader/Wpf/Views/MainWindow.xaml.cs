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
            DataContext = new MainViewModel();
        }
    }
}
