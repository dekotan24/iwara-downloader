using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// AboutWindow用ViewModel。旧WinForms版AboutFormと同等の機能
    /// (バージョン表示・更新チェック・GitHubリンク)をMVVMで再現する垂直スライス。
    /// </summary>
    public partial class AboutViewModel : ObservableObject
    {
        public const string GitHubUrl = "https://github.com/dekotan24/iwara-downloader";

        public string VersionText => $"Version {UpdateService.CurrentVersionString}";
        public string CopyrightText => $"© {DateTime.Now.Year} Ogura Deko";
        public string DescriptionText => L.T("AboutForm_lblDescription");

        [ObservableProperty]
        private string _updateStatusText = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CheckUpdateCommand))]
        private bool _isCheckingUpdate;

        [RelayCommand]
        private void OpenGitHub() => Helpers.OpenUrl(GitHubUrl);

        private bool CanCheckUpdate() => !IsCheckingUpdate;

        [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
        private async Task CheckUpdateAsync()
        {
            IsCheckingUpdate = true;
            UpdateStatusText = L.T("AboutForm_D001");

            try
            {
                var result = await UpdateService.CheckForUpdateAsync();

                if (result.HasUpdate)
                {
                    UpdateStatusText = L.T("AboutForm_D002", result.LatestVersion);
                    var dialogResult = System.Windows.MessageBox.Show(
                        L.T("AboutForm_D003", result.LatestVersion),
                        L.T("AboutForm_D004"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (dialogResult == System.Windows.MessageBoxResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
                else
                {
                    UpdateStatusText = L.T("AboutForm_D005");
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = L.T("AboutForm_D006");
                System.Windows.MessageBox.Show(
                    L.T("AboutForm_D007", ex.Message),
                    L.T("AboutForm_D008"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }
    }
}
