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
            Utils.Localizer.Apply(this);
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
            lblUpdateStatus.Text = L.T("AboutForm_D001");

            try
            {
                var result = await UpdateService.CheckForUpdateAsync();

                if (result.HasUpdate)
                {
                    lblUpdateStatus.Text = L.T("AboutForm_D002", result.LatestVersion);
                    var dialogResult = MessageBox.Show(
                        L.T("AboutForm_D003", result.LatestVersion),
                        L.T("AboutForm_D004"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
                else
                {
                    lblUpdateStatus.Text = L.T("AboutForm_D005");
                }
            }
            catch (Exception ex)
            {
                lblUpdateStatus.Text = L.T("AboutForm_D006");
                MessageBox.Show(L.T("AboutForm_D007", ex.Message), L.T("AboutForm_D008"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnCheckUpdate.Enabled = true;
            }
        }
    }
}
