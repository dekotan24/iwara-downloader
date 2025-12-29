using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 設定フォーム
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly SettingsManager _settingsManager;
        private readonly DatabaseService _database;
        private readonly IwaraApiService _iwaraApi;

        // チェック間隔の選択肢（分）
        private readonly int[] _checkIntervalMinutes = { 30, 60, 120, 360, 720, 1440 };

        public SettingsForm()
        {
            InitializeComponent();
            _settingsManager = SettingsManager.Instance;
            _database = DatabaseService.Instance;
            _iwaraApi = new IwaraApiService();
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            LoadSettings();
            UpdateLoginStatusDisplay();
        }

        /// <summary>
        /// 設定を読み込んでUIに反映
        /// </summary>
        private void LoadSettings()
        {
            var settings = _settingsManager.Settings;

            // ダウンロード設定
            txtDownloadFolder.Text = settings.DownloadFolder;
            
            // ComboBoxの選択（範囲チェック）
            var qualityIndex = (int)settings.DefaultQuality;
            if (qualityIndex >= 0 && qualityIndex < cmbQuality.Items.Count)
            {
                cmbQuality.SelectedIndex = qualityIndex;
            }
            else
            {
                cmbQuality.SelectedIndex = 0; // デフォルト: Source
            }
            numConcurrent.Value = settings.MaxConcurrentDownloads;
            numRetry.Value = settings.MaxRetryCount;

            // 自動チェック
            chkAutoCheck.Checked = settings.AutoCheckEnabled;
            var intervalIndex = Array.IndexOf(_checkIntervalMinutes, settings.CheckIntervalMinutes);
            cmbCheckInterval.SelectedIndex = intervalIndex >= 0 ? intervalIndex : 1; // デフォルト1時間
            chkAutoDownload.Checked = settings.AutoDownloadOnCheck;

            // 通知・起動
            chkToast.Checked = settings.EnableToastNotification;
            chkStartMinimized.Checked = settings.StartMinimized;
            chkMinimizeToTray.Checked = settings.MinimizeToTray;

            // Python環境
            txtPythonPath.Text = settings.PythonPath;

            // アカウント
            txtEmail.Text = settings.IwaraEmail;
            txtPassword.Text = _settingsManager.GetIwaraPassword();

            // レート制限設定
            numApiDelay.Value = settings.ApiRequestDelayMs;
            numDownloadDelay.Value = settings.DownloadDelayMs;
            numChannelDelay.Value = settings.ChannelCheckDelayMs;
            numPageDelay.Value = settings.PageFetchDelayMs;
            numRateLimitBase.Value = settings.RateLimitBaseDelayMs;
            numRateLimitMax.Value = settings.RateLimitMaxDelayMs;
            chkExponentialBackoff.Checked = settings.EnableExponentialBackoff;
        }

        /// <summary>
        /// UIの値を設定に保存
        /// </summary>
        private void SaveSettings()
        {
            var settings = _settingsManager.Settings;

            // ダウンロード設定
            settings.DownloadFolder = txtDownloadFolder.Text;
            settings.DefaultQuality = (VideoQuality)cmbQuality.SelectedIndex;
            settings.MaxConcurrentDownloads = (int)numConcurrent.Value;
            settings.MaxRetryCount = (int)numRetry.Value;

            // 自動チェック
            settings.AutoCheckEnabled = chkAutoCheck.Checked;
            if (cmbCheckInterval.SelectedIndex >= 0 && cmbCheckInterval.SelectedIndex < _checkIntervalMinutes.Length)
            {
                settings.CheckIntervalMinutes = _checkIntervalMinutes[cmbCheckInterval.SelectedIndex];
            }
            settings.AutoDownloadOnCheck = chkAutoDownload.Checked;

            // 通知・起動
            settings.EnableToastNotification = chkToast.Checked;
            settings.StartMinimized = chkStartMinimized.Checked;
            settings.MinimizeToTray = chkMinimizeToTray.Checked;

            // Python環境
            settings.PythonPath = txtPythonPath.Text.Trim();

            // アカウント
            settings.IwaraEmail = txtEmail.Text.Trim();
            _settingsManager.SetIwaraPassword(txtPassword.Text);

            // レート制限設定
            settings.ApiRequestDelayMs = (int)numApiDelay.Value;
            settings.DownloadDelayMs = (int)numDownloadDelay.Value;
            settings.ChannelCheckDelayMs = (int)numChannelDelay.Value;
            settings.PageFetchDelayMs = (int)numPageDelay.Value;
            settings.RateLimitBaseDelayMs = (int)numRateLimitBase.Value;
            settings.RateLimitMaxDelayMs = (int)numRateLimitMax.Value;
            settings.EnableExponentialBackoff = chkExponentialBackoff.Checked;

            // 保存
            _settingsManager.Save();
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "ダウンロード先フォルダを選択してください",
                ShowNewFolderButton = true,
                SelectedPath = txtDownloadFolder.Text
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtDownloadFolder.Text = dialog.SelectedPath;
            }
        }

        private void btnBrowsePython_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Python実行ファイルを選択",
                Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
                FileName = "python.exe"
            };

            // よくあるPythonインストール先を探す
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
                @"C:\Python311",
                @"C:\Python312",
                @"C:\Python310"
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    dialog.InitialDirectory = path;
                    break;
                }
            }

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtPythonPath.Text = dialog.FileName;
            }
        }

        private async void btnReLogin_Click(object sender, EventArgs e)
        {
            var email = txtEmail.Text.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("メールアドレスとパスワードを入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 先に設定を保存（Pythonパスを含む）
            SaveSettings();

            btnReLogin.Enabled = false;
            btnReLogin.Text = "ログイン中...";

            try
            {
                // 一度ログアウトしてから再ログイン
                _iwaraApi.Logout();

                var (success, error) = await _iwaraApi.LoginAsync(email, password);

                if (success)
                {
                    MessageBox.Show("ログインに成功しました！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"ログインに失敗しました:\n{error}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ログイン中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnReLogin.Enabled = true;
                btnReLogin.Text = "再ログイン";
                UpdateLoginStatusDisplay();
            }
        }

        /// <summary>
        /// ログイン状態表示を更新
        /// </summary>
        private void UpdateLoginStatusDisplay()
        {
            if (_iwaraApi.IsLoggedIn)
            {
                lblLoginStatus.Text = "(ログイン済)";
                lblLoginStatus.ForeColor = Color.Green;
            }
            else
            {
                lblLoginStatus.Text = "(未ログイン)";
                lblLoginStatus.ForeColor = Color.Gray;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            SaveSettings();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            SaveSettings();
            MessageBox.Show("設定を保存しました。", "設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #region Export/Import

        private void btnExportSettings_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "設定をエクスポート",
                Filter = "JSONファイル (*.json)|*.json",
                FileName = "iwara_settings.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = _settingsManager.ExportToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show("設定をエクスポートしました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnExportSubscriptions_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "購読リストをエクスポート",
                Filter = "JSONファイル (*.json)|*.json",
                FileName = "iwara_subscriptions.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = _database.ExportSubscriptionsToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show("購読リストをエクスポートしました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnImportSettings_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "設定をインポート",
                Filter = "JSONファイル (*.json)|*.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    _settingsManager.ImportFromJson(json);
                    LoadSettings(); // UIを更新
                    MessageBox.Show("設定をインポートしました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"インポートに失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnImportSubscriptions_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "購読リストをインポート",
                Filter = "JSONファイル (*.json)|*.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var count = _database.ImportSubscriptionsFromJson(json);
                    MessageBox.Show($"{count}件の購読をインポートしました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"インポートに失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion
    }
}
