using System;
using System.Windows;
using System.Windows.Input;
using IwaraDownloader.Utils;
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
        // 隠しイースターエッグ: アイコンを短時間で連打するとパーティーモードをトグルする。
        private const int PartyModeClickThreshold = 7;
        private static readonly TimeSpan PartyModeClickWindow = TimeSpan.FromMilliseconds(800);
        private int _iconClickCount;
        private DateTime _lastIconClickAt;

        public AboutWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(this);
            DataContext = new AboutViewModel();
        }

        private void OnOkClick(object sender, RoutedEventArgs e) => Close();

        private void AppIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            if (now - _lastIconClickAt > PartyModeClickWindow)
                _iconClickCount = 0;
            _lastIconClickAt = now;
            _iconClickCount++;

            if (_iconClickCount >= PartyModeClickThreshold)
            {
                _iconClickCount = 0;

                // 有効化するときだけ確認する。解除は即座(お遊びをやめるのに確認は不要)。
                if (PartyModeService.IsEnabled)
                {
                    PartyModeService.Toggle();
                    return;
                }

                var result = System.Windows.MessageBox.Show(
                    L.T("AboutForm_D009"),
                    L.T("AboutForm_D010"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    PartyModeService.Toggle();
                }
            }
        }
    }
}
