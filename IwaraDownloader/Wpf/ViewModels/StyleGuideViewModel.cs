using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IwaraDownloader.Wpf.Theme;

namespace IwaraDownloader.Wpf.ViewModels
{
    /// <summary>
    /// スタイルガイド用テストウィンドウのViewModel。
    /// CommunityToolkit.Mvvmのソースジェネレータ([ObservableProperty]/[RelayCommand])が
    /// このプロジェクト構成で正しく動くかを検証する目的も兼ねる。
    /// </summary>
    public partial class StyleGuideViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sampleText = "";

        [ObservableProperty]
        private AppTheme _currentTheme = ThemeManager.Current;

        public event Action? ThemeToggled;

        [RelayCommand]
        private void ToggleTheme()
        {
            var next = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            ThemeManager.SetTheme(next);
            CurrentTheme = next;
            ThemeToggled?.Invoke();
        }
    }
}
