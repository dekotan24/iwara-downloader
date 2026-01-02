using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using System.Diagnostics;
using System.Reflection;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// バージョン情報ダイアログ
    /// </summary>
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
        }

        private void AboutForm_Load(object sender, EventArgs e)
        {
            // バージョン情報を設定
            lblVersion.Text = $"Version {UpdateService.CurrentVersionString}";
            lblCopyright.Text = $"© {DateTime.Now.Year} Ogura Deko";
        }

        private void linkGitHub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Helpers.OpenUrl("https://github.com/dekotan24/iwara-downloader");
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void btnCheckUpdate_Click(object sender, EventArgs e)
        {
            btnCheckUpdate.Enabled = false;
            lblUpdateStatus.Text = "確認中...";

            try
            {
                var result = await UpdateService.CheckForUpdateAsync();

                if (result.HasUpdate)
                {
                    lblUpdateStatus.Text = $"新バージョンあり: {result.LatestVersion}";
                    var dialogResult = MessageBox.Show(
                        $"新しいバージョンがあります！\n\n最新: {result.LatestVersion}\n\nリリースページを開きますか？",
                        "更新のお知らせ",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
                else
                {
                    lblUpdateStatus.Text = "最新バージョンです";
                }
            }
            catch (Exception ex)
            {
                lblUpdateStatus.Text = "確認に失敗しました";
                MessageBox.Show($"更新確認に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnCheckUpdate.Enabled = true;
            }
        }
    }
}
