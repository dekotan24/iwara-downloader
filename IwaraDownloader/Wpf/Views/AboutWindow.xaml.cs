using System.Windows;
using IwaraDownloader.Wpf.Theme;
using IwaraDownloader.Wpf.ViewModels;

namespace IwaraDownloader.Wpf.Views
{
    /// <summary>
    /// Phase2: 垂直スライス。旧WinForms版AboutFormのWPF移植第一号。
    /// テーマ・i18n・MVVM(CommunityToolkit.Mvvm)・Service呼び出し(UpdateService)を
    /// 一通り通しで検証する。
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(this);
            DataContext = new AboutViewModel();
        }

        private void OnOkClick(object sender, RoutedEventArgs e) => Close();
    }
}
