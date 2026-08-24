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
        private readonly DownloadManager? _downloadManager;

        // チェック間隔の選択肢(分)
        private readonly int[] _checkIntervalMinutes = { 30, 60, 120, 360, 720, 1440 };

        // ComboBox の index と VideoQuality の対応表(cmbQuality の Items と同順)
        private static readonly VideoQuality[] _qualityOrder =
        {
            VideoQuality.Source,
            VideoQuality.Quality540p,
            VideoQuality.Quality360p,
        };

        public SettingsForm() : this(null) { }

        public SettingsForm(DownloadManager? downloadManager)
        {
            InitializeComponent();
            Utils.Localizer.Apply(this);
            _settingsManager = SettingsManager.Instance;
            _database = DatabaseService.Instance;
            // DownloadManager と同じ IwaraApiService インスタンスを共有する。
            // 別インスタンスにするとここでの再ログインが MainForm 側に伝播せず、
            // 「設定画面で再ログイン → 閉じてもダウンロードがログイン状態にならない」となる。
            _iwaraApi = downloadManager?.IwaraApi ?? new IwaraApiService();
            _downloadManager = downloadManager;
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
            
            // 画質 ComboBox(未対応enum値はSourceにフォールバック)
            var qIdx = Array.IndexOf(_qualityOrder, settings.DefaultQuality);
            cmbQuality.SelectedIndex = qIdx >= 0 ? qIdx : 0;
            numConcurrent.Value = settings.MaxConcurrentDownloads;
            numRetry.Value = settings.MaxRetryCount;
            cmbThumbLocation.SelectedIndex = settings.ThumbnailCacheLocation == 1 ? 1 : 0;
            numMinFreeSpace.Value = Math.Clamp(settings.MinFreeSpaceGb, 0, 999);

            // 自動チェック
            chkAutoCheck.Checked = settings.AutoCheckEnabled;
            var intervalIndex = Array.IndexOf(_checkIntervalMinutes, settings.CheckIntervalMinutes);
            cmbCheckInterval.SelectedIndex = intervalIndex >= 0 ? intervalIndex : 1; // デフォルト1時間
            chkAutoDownload.Checked = settings.AutoDownloadOnCheck;
            chkDownloadExternal.Checked = settings.DownloadExternalVideosDefault;

            // 通知・起動
            chkToast.Checked = settings.EnableToastNotification;
            chkStartMinimized.Checked = settings.StartMinimized;
            chkMinimizeToTray.Checked = settings.MinimizeToTray;

            // 言語 (Tagに設定値を持たせ、表示名は各言語のネイティブ表記で固定)
            cmbLanguage.Items.Clear();
            cmbLanguage.Items.Add(new LanguageItem("auto", Utils.L.T("Settings_LanguageAuto")));
            cmbLanguage.Items.Add(new LanguageItem("ja", "日本語"));
            cmbLanguage.Items.Add(new LanguageItem("en", "English"));
            cmbLanguage.Items.Add(new LanguageItem("zh-Hans", "简体中文"));
            var langIndex = 0;
            for (var i = 0; i < cmbLanguage.Items.Count; i++)
            {
                if (((LanguageItem)cmbLanguage.Items[i]!).Code == settings.Language) { langIndex = i; break; }
            }
            cmbLanguage.SelectedIndex = langIndex;

            // Python環境
            txtPythonPath.Text = settings.PythonPath;
            txtYtDlpPath.Text = settings.YtDlpPath;

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

            // その他設定
            chkEnableSound.Checked = settings.EnableCompletionSound;
            txtSoundFile.Text = settings.CompletionSoundPath;
            chkEnableErrorSound.Checked = settings.EnableErrorSound;
            txtErrorSoundFile.Text = settings.ErrorSoundPath;
            txtFilenameTemplate.Text = settings.FilenameTemplate;
            chkSaveMetadata.Checked = settings.SaveMetadata;
            chkCheckUpdate.Checked = settings.CheckUpdateOnStartup;
            chkResumeOnStartup.Checked = settings.ResumeDownloadsOnStartup;
            lblCurrentVersion.Text = L.T("SettingsForm_D001", UpdateService.CurrentVersionString);

            // メディアサーバー
            chkWebServerAutoStart.Checked = settings.WebServerAutoStart;
            numWebPort.Value = Math.Clamp(settings.WebServerPort, 1024, 65535);
            chkWebBindAll.Checked = settings.WebServerBindAll;
            txtWebUsername.Text = settings.WebServerUsername;
            txtWebPassword.Text = _settingsManager.GetWebServerPassword();
            UpdateWebServerStatusDisplay();
        }

        /// <summary>
        /// UIの値を設定に保存
        /// </summary>
        private void SaveSettings()
        {
            var settings = _settingsManager.Settings;

            // ダウンロード設定
            settings.DownloadFolder = txtDownloadFolder.Text;
            if (cmbQuality.SelectedIndex >= 0 && cmbQuality.SelectedIndex < _qualityOrder.Length)
            {
                settings.DefaultQuality = _qualityOrder[cmbQuality.SelectedIndex];
            }
            settings.MaxConcurrentDownloads = (int)numConcurrent.Value;
            settings.MaxRetryCount = (int)numRetry.Value;
            settings.MinFreeSpaceGb = (int)numMinFreeSpace.Value;

            // サムネ保存先
            settings.ThumbnailCacheLocation = cmbThumbLocation.SelectedIndex == 1 ? 1 : 0;

            // 自動チェック
            settings.AutoCheckEnabled = chkAutoCheck.Checked;
            if (cmbCheckInterval.SelectedIndex >= 0 && cmbCheckInterval.SelectedIndex < _checkIntervalMinutes.Length)
            {
                settings.CheckIntervalMinutes = _checkIntervalMinutes[cmbCheckInterval.SelectedIndex];
            }
            settings.AutoDownloadOnCheck = chkAutoDownload.Checked;
            settings.DownloadExternalVideosDefault = chkDownloadExternal.Checked;

            // 通知・起動
            settings.EnableToastNotification = chkToast.Checked;
            settings.StartMinimized = chkStartMinimized.Checked;
            settings.MinimizeToTray = chkMinimizeToTray.Checked;

            // 言語 (変更時は再起動後に反映される旨を案内)
            if (cmbLanguage.SelectedItem is LanguageItem langItem && langItem.Code != settings.Language)
            {
                settings.Language = langItem.Code;
                MessageBox.Show(Utils.L.T("Msg_LanguageRestart"), "IwaraDownloader",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Python環境
            settings.PythonPath = txtPythonPath.Text.Trim();
            var ytDlp = txtYtDlpPath.Text.Trim();
            settings.YtDlpPath = string.IsNullOrEmpty(ytDlp) ? "yt-dlp" : ytDlp;

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

            // その他設定
            settings.EnableCompletionSound = chkEnableSound.Checked;
            settings.CompletionSoundPath = txtSoundFile.Text.Trim();
            settings.EnableErrorSound = chkEnableErrorSound.Checked;
            settings.ErrorSoundPath = txtErrorSoundFile.Text.Trim();
            settings.FilenameTemplate = txtFilenameTemplate.Text.Trim();
            settings.SaveMetadata = chkSaveMetadata.Checked;
            settings.CheckUpdateOnStartup = chkCheckUpdate.Checked;
            settings.ResumeDownloadsOnStartup = chkResumeOnStartup.Checked;

            // メディアサーバー
            settings.WebServerAutoStart = chkWebServerAutoStart.Checked;
            settings.WebServerPort = (int)numWebPort.Value;
            settings.WebServerBindAll = chkWebBindAll.Checked;
            settings.WebServerUsername = txtWebUsername.Text.Trim();
            _settingsManager.SetWebServerPassword(txtWebPassword.Text);

            // 保存
            _settingsManager.Save();

            // サムネ保存先が変わっていれば既存キャッシュを移行 (LastThumbnailCacheDir との差分で判断)。
            // 中断されても次回起動時の SyncCacheDirIfMoved で残りが自動移行される。
            _ = Task.Run(ThumbnailCacheService.SyncCacheDirIfMoved);
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = L.T("SettingsForm_D002"),
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
                Title = L.T("SettingsForm_D088"),
                Filter = L.T("SettingsForm_D003"),
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

        private void btnBrowseYtDlp_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = L.T("SettingsForm_D004"),
                Title = L.T("SettingsForm_D089")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtYtDlpPath.Text = dialog.FileName;
            }
        }

        private async void btnReLogin_Click(object sender, EventArgs e)
        {
            var email = txtEmail.Text.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show(L.T("SettingsForm_D005"), L.T("SettingsForm_D006"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 先に設定を保存(Pythonパスを含む)
            SaveSettings();

            btnReLogin.Enabled = false;
            btnReLogin.Text = L.T("SettingsForm_D007");

            try
            {
                // 一度ログアウトしてから再ログイン
                _iwaraApi.Logout();

                var (success, error) = await _iwaraApi.LoginAsync(email, password);

                if (success)
                {
                    _downloadManager?.ResumeAfterLogin();
                    MessageBox.Show(L.T("SettingsForm_D008"), L.T("SettingsForm_D009"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(L.T("SettingsForm_D010", error), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("SettingsForm_D012", ex.Message), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnReLogin.Enabled = true;
                btnReLogin.Text = L.T("SettingsForm_D013");
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
                lblLoginStatus.Text = L.T("SettingsForm_D014");
                lblLoginStatus.ForeColor = Color.Green;
            }
            else
            {
                lblLoginStatus.Text = L.T("SettingsForm_D015");
                lblLoginStatus.ForeColor = Color.Gray;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (!ApplySettings()) return; // 保存先変更がキャンセルされた場合は閉じない
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (!ApplySettings()) return;
            MessageBox.Show(L.T("SettingsForm_D016"), L.T("SettingsForm_D017"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 設定を保存する。DL先フォルダが変更されている場合は既存ファイルの移動を提案し、
        /// 容量チェック → 確認 → 移動 (進捗表示) まで行う。
        /// </summary>
        /// <returns>false = ユーザーがキャンセルした (設定は未保存)</returns>
        private bool ApplySettings()
        {
            var settings = _settingsManager.Settings;
            var oldFolder = settings.DownloadFolder;
            var newFolder = txtDownloadFolder.Text.Trim();

            List<(VideoInfo Video, string NewPath)>? movePlan = null;

            bool folderChanged;
            try
            {
                folderChanged =
                    !string.IsNullOrWhiteSpace(oldFolder)
                    && !string.IsNullOrWhiteSpace(newFolder)
                    && !string.Equals(
                        Path.GetFullPath(oldFolder).TrimEnd('\\'),
                        Path.GetFullPath(newFolder).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(oldFolder);
            }
            catch
            {
                folderChanged = false; // 不正なパス文字列は移動なしで従来通り保存
            }

            if (folderChanged)
            {
                // DL 実行中の移動はファイルロック・DB不整合の元なのでブロック
                if (_downloadManager != null
                    && (_downloadManager.DownloadingCount > 0 || _downloadManager.WritingTagsCount > 0))
                {
                    MessageBox.Show(this,
                        L.T("SettingsForm_D018") +
                        L.T("SettingsForm_D019"),
                        L.T("SettingsForm_D020"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // 個別保存先 (CustomSavePath) を設定したチャンネルのファイルは動かさない
                var excludeBases = _database.GetAllSubscribedUsers()
                    .Where(u => !string.IsNullOrWhiteSpace(u.CustomSavePath))
                    .Select(u => u.CustomSavePath);

                var movable = FileMoveHelper.GetMovableFiles(
                    _database.GetAllVideos(), oldFolder, excludeBases);

                var decision = FileMoveHelper.ConfirmMove(
                    this, L.T("SettingsForm_MoveSubjectDownloadFolder"), movable, oldFolder, newFolder);
                if (decision == FileMoveHelper.MoveDecision.Cancel) return false;
                if (decision == FileMoveHelper.MoveDecision.Move)
                {
                    movePlan = FileMoveHelper.BuildMovePlan(movable, oldFolder, newFolder);
                }
            }

            SaveSettings();

            if (movePlan != null)
            {
                using var progressForm = new FileMoveProgressForm(movePlan, _database);
                progressForm.ShowDialog(this);

                // 旧フォルダのインデックスキャッシュ掃除 + 空フォルダ削除
                FileMoveHelper.CleanupEmptyDirectories(oldFolder);

                MessageBox.Show(this,
                    L.T("SettingsForm_D021") +
                    L.T("SettingsForm_D022", progressForm.MovedCount, progressForm.FailedCount) +
                    (progressForm.FailedCount > 0
                        ? L.T("SettingsForm_D023") +
                          L.T("SettingsForm_D024")
                        : ""),
                    L.T("SettingsForm_D025"), MessageBoxButtons.OK,
                    progressForm.FailedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            return true;
        }

        #region Export/Import

        private void btnExportSettings_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = L.T("SettingsForm_D090"),
                Filter = L.T("SettingsForm_D026"),
                FileName = "iwara_settings.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = _settingsManager.ExportToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show(L.T("SettingsForm_D027"), L.T("SettingsForm_D028"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L.T("SettingsForm_D029", ex.Message), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnExportSubscriptions_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = L.T("SettingsForm_D091"),
                Filter = L.T("SettingsForm_D026"),
                FileName = "iwara_subscriptions.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = _database.ExportSubscriptionsToJson();
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show(L.T("SettingsForm_D030"), L.T("SettingsForm_D028"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L.T("SettingsForm_D031", ex.Message), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnImportSettings_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("SettingsForm_D092"),
                Filter = L.T("SettingsForm_D026")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    _settingsManager.ImportFromJson(json);
                    LoadSettings(); // UIを更新
                    MessageBox.Show(L.T("SettingsForm_D032"), L.T("SettingsForm_D028"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L.T("SettingsForm_D033", ex.Message), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnImportSubscriptions_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("SettingsForm_D093"),
                Filter = L.T("SettingsForm_D026")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var count = _database.ImportSubscriptionsFromJson(json);
                    MessageBox.Show(L.T("SettingsForm_D034", count), L.T("SettingsForm_D028"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L.T("SettingsForm_D035", ex.Message), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnMigrateExistingFiles_Click(object sender, EventArgs e)
        {
            if (_downloadManager == null)
            {
                MessageBox.Show(L.T("SettingsForm_D036"), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_downloadManager.IsMigrationRunning)
            {
                MessageBox.Show(this, L.T("SettingsForm_D037"),
                    L.T("SettingsForm_D038"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                L.T("SettingsForm_D039") +
                L.T("SettingsForm_D040") +
                L.T("SettingsForm_D041") +
                L.T("SettingsForm_D042") +
                L.T("SettingsForm_D043"),
                L.T("SettingsForm_D044"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (_downloadManager.StartMigrateExistingFiles())
            {
                MessageBox.Show(this, L.T("SettingsForm_D045"),
                    L.T("SettingsForm_D046"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// サムネ URL + 画像をバックグラウンドで一括補完。
        /// 設定画面を閉じても継続実行され、進捗はメイン画面のステータスバーに反映。
        /// </summary>
        private void btnBackfillThumbnails_Click(object sender, EventArgs e)
        {
            if (_downloadManager == null)
            {
                MessageBox.Show(L.T("SettingsForm_D036"), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_downloadManager.IsBackfillRunning)
            {
                MessageBox.Show(this, L.T("SettingsForm_D037"),
                    L.T("SettingsForm_D038"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                L.T("SettingsForm_D047") +
                L.T("SettingsForm_D048") +
                L.T("SettingsForm_D049") +
                L.T("SettingsForm_D050") +
                L.T("SettingsForm_D041") +
                L.T("SettingsForm_D042") +
                L.T("SettingsForm_D043"),
                L.T("SettingsForm_D051"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (_downloadManager.StartBackfillThumbnails())
            {
                MessageBox.Show(this, L.T("SettingsForm_D045"),
                    L.T("SettingsForm_D046"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // バックグラウンドタスクはアプリ寿命で管理してるので、設定画面は自由に閉じてOK
            base.OnFormClosing(e);
        }

        #endregion

        #region Sound Settings

        private void btnBrowseSound_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("SettingsForm_D094"),
                Filter = L.T("SettingsForm_D052")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtSoundFile.Text = dialog.FileName;
            }
        }

        private void btnTestSound_Click(object sender, EventArgs e)
        {
            var soundPath = txtSoundFile.Text.Trim();
            
            if (string.IsNullOrEmpty(soundPath))
            {
                // システム音をテスト
                System.Media.SystemSounds.Asterisk.Play();
            }
            else if (File.Exists(soundPath))
            {
                SoundService.Instance.PlaySound(soundPath);
            }
            else
            {
                MessageBox.Show(L.T("SettingsForm_D053"), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnBrowseErrorSound_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("SettingsForm_D095"),
                Filter = L.T("SettingsForm_D052")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtErrorSoundFile.Text = dialog.FileName;
            }
        }

        private void btnTestErrorSound_Click(object sender, EventArgs e)
        {
            var soundPath = txtErrorSoundFile.Text.Trim();
            
            if (string.IsNullOrEmpty(soundPath))
            {
                // システムエラー音をテスト
                System.Media.SystemSounds.Hand.Play();
            }
            else if (File.Exists(soundPath))
            {
                SoundService.Instance.PlaySound(soundPath);
            }
            else
            {
                MessageBox.Show(L.T("SettingsForm_D053"), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region Rename Files

        private async void btnRenameFiles_Click(object sender, EventArgs e)
        {
            // 現在のテンプレートを取得
            var template = txtFilenameTemplate.Text.Trim();
            if (string.IsNullOrEmpty(template))
            {
                MessageBox.Show(L.T("SettingsForm_D054"), L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // DL済みの動画を取得
            var completedVideos = _database.GetVideosByStatus(DownloadStatus.Completed)
                .Where(v => !string.IsNullOrEmpty(v.LocalFilePath))
                .ToList();

            if (completedVideos.Count == 0)
            {
                MessageBox.Show(L.T("SettingsForm_D055"), L.T("SettingsForm_D056"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnRenameFiles.Enabled = false;
            btnRenameFiles.Text = L.T("SettingsForm_D057");

            // リネーム項目を作成
            var items = new List<RenameItem>();
            var newPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                foreach (var video in completedVideos)
                {
                    var item = new RenameItem
                    {
                        Video = video,
                        OriginalPath = video.LocalFilePath!,
                        Status = RenameStatus.Pending
                    };

                    // ファイルが存在しない場合
                    if (!File.Exists(item.OriginalPath))
                    {
                        item.Status = RenameStatus.FileNotFound;
                        item.NewPath = item.OriginalPath;
                        items.Add(item);
                        continue;
                    }

                    var directory = Path.GetDirectoryName(item.OriginalPath)!;
                    var extension = Path.GetExtension(item.OriginalPath);

                    // 新しいファイル名を生成
                    var newFilename = Helpers.ApplyFilenameTemplate(
                        template,
                        video.Title,
                        video.AuthorUsername ?? "unknown",
                        video.VideoId,
                        video.PostedAt);

                    item.NewPath = Path.Combine(directory, newFilename + extension);

                    // 同じファイル名ならスキップ
                    if (item.OriginalPath.Equals(item.NewPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Status = RenameStatus.Skipped;
                        items.Add(item);
                        continue;
                    }

                    // 既存ファイルとの重複チェック
                    if (File.Exists(item.NewPath))
                    {
                        item.Status = RenameStatus.Conflict;
                        item.ConflictingPath = item.NewPath;
                        items.Add(item);
                        newPathSet.Add(item.NewPath);
                        continue;
                    }

                    // 他のリネーム対象との重複チェック
                    if (newPathSet.Contains(item.NewPath))
                    {
                        item.Status = RenameStatus.Conflict;
                        item.ConflictingPath = item.NewPath;
                        items.Add(item);
                        continue;
                    }

                    newPathSet.Add(item.NewPath);
                    item.Status = RenameStatus.Pending;
                    items.Add(item);
                }
            });

            // 重複があるか確認
            var conflictCount = items.Count(i => i.Status == RenameStatus.Conflict);
            var pendingCount = items.Count(i => i.Status == RenameStatus.Pending);
            var notFoundCount = items.Count(i => i.Status == RenameStatus.FileNotFound);

            if (conflictCount > 0)
            {
                var warningResult = MessageBox.Show(
                    L.T("SettingsForm_D058", completedVideos.Count) +
                    L.T("SettingsForm_D059", pendingCount) +
                    L.T("SettingsForm_D060", conflictCount) +
                    L.T("SettingsForm_D061", notFoundCount) +
                    L.T("SettingsForm_D062") +
                    L.T("SettingsForm_D063"),
                    L.T("SettingsForm_D064"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (warningResult != DialogResult.Yes)
                {
                    btnRenameFiles.Enabled = true;
                    btnRenameFiles.Text = L.T("SettingsForm_D065");
                    return;
                }
            }
            else if (pendingCount > 0)
            {
                var confirmResult = MessageBox.Show(
                    L.T("SettingsForm_D066", pendingCount) +
                    L.T("SettingsForm_D067", template) +
                    L.T("SettingsForm_D068"),
                    L.T("SettingsForm_D069"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    btnRenameFiles.Enabled = true;
                    btnRenameFiles.Text = L.T("SettingsForm_D065");
                    return;
                }
            }

            btnRenameFiles.Text = L.T("SettingsForm_D070");

            // Pending状態のファイルをリネーム
            await Task.Run(() =>
            {
                // 強制終了対策: リネームと DB 更新の間で死んでも次回起動時に復旧できるようジャーナルに記録
                using var journal = FileMoveJournal.Begin();

                foreach (var item in items.Where(i => i.Status == RenameStatus.Pending))
                {
                    try
                    {
                        // ファイルをリネーム
                        journal.RecordStart(item.Video.Id, item.OriginalPath, item.NewPath);
                        File.Move(item.OriginalPath, item.NewPath);

                        // メタデータファイル(.json)もリネーム
                        var originalJsonPath = Path.ChangeExtension(item.OriginalPath, ".json");
                        if (File.Exists(originalJsonPath))
                        {
                            var newJsonPath = Path.ChangeExtension(item.NewPath, ".json");
                            if (File.Exists(newJsonPath))
                                File.Delete(newJsonPath);
                            File.Move(originalJsonPath, newJsonPath);
                        }

                        // DB更新
                        item.Video.LocalFilePath = item.NewPath;
                        _database.UpdateVideo(item.Video);
                        journal.RecordDone(item.Video.Id);

                        item.Status = RenameStatus.Success;
                    }
                    catch (Exception ex)
                    {
                        item.Status = RenameStatus.Error;
                        item.ErrorMessage = ex.Message;
                    }
                }
            });

            btnRenameFiles.Enabled = true;
            btnRenameFiles.Text = L.T("SettingsForm_D065");

            // 結果ダイアログを表示
            using var resultForm = new RenameResultForm(items, template);
            resultForm.ShowDialog(this);
        }

        #endregion

        #region Rate Limit Presets

        /// <summary>
        /// 控えめプリセット(サーバー負荷を最小限に)
        /// </summary>
        private void btnPresetConservative_Click(object sender, EventArgs e)
        {
            numApiDelay.Value = 2000;        // 2秒
            numDownloadDelay.Value = 5000;    // 5秒
            numChannelDelay.Value = 10000;    // 10秒
            numPageDelay.Value = 1000;        // 1秒
            numRateLimitBase.Value = 60000;   // 60秒
            numRateLimitMax.Value = 600000;   // 10分
            chkExponentialBackoff.Checked = true;
        }

        /// <summary>
        /// 標準プリセット(バランス重視)
        /// </summary>
        private void btnPresetStandard_Click(object sender, EventArgs e)
        {
            numApiDelay.Value = 1000;        // 1秒
            numDownloadDelay.Value = 3000;    // 3秒
            numChannelDelay.Value = 5000;     // 5秒
            numPageDelay.Value = 500;         // 0.5秒
            numRateLimitBase.Value = 30000;   // 30秒
            numRateLimitMax.Value = 300000;   // 5分
            chkExponentialBackoff.Checked = true;
        }

        /// <summary>
        /// 積極的プリセット(速度優先、エラー増加の可能性あり)
        /// </summary>
        private void btnPresetAggressive_Click(object sender, EventArgs e)
        {
            numApiDelay.Value = 500;         // 0.5秒
            numDownloadDelay.Value = 1000;    // 1秒
            numChannelDelay.Value = 2000;     // 2秒
            numPageDelay.Value = 200;         // 0.2秒
            numRateLimitBase.Value = 15000;   // 15秒
            numRateLimitMax.Value = 120000;   // 2分
            chkExponentialBackoff.Checked = true;

            // 警告を表示
            MessageBox.Show(
                L.T("SettingsForm_D071") +
                L.T("SettingsForm_D072"),
                L.T("SettingsForm_D073"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        #endregion

        #region Update Check

        private async void btnCheckUpdateNow_Click(object sender, EventArgs e)
        {
            btnCheckUpdateNow.Enabled = false;
            btnCheckUpdateNow.Text = L.T("SettingsForm_D074");

            try
            {
                var result = await UpdateService.CheckForUpdateAsync();

                if (!result.Success)
                {
                    MessageBox.Show(L.T("SettingsForm_D075", result.ErrorMessage), 
                        L.T("SettingsForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (result.HasUpdate)
                {
                    var dialogResult = MessageBox.Show(
                        L.T("SettingsForm_D076") +
                        L.T("SettingsForm_D077", UpdateService.CurrentVersionString) +
                        L.T("SettingsForm_D078", result.LatestVersionString) +
                        L.T("SettingsForm_D079"),
                        L.T("SettingsForm_D080"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
                else
                {
                    MessageBox.Show(L.T("SettingsForm_D081", UpdateService.CurrentVersionString), 
                        L.T("SettingsForm_D082"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                btnCheckUpdateNow.Enabled = true;
                btnCheckUpdateNow.Text = L.T("SettingsForm_D083");
            }
        }

        #endregion

        #region Web Media Server

        private void UpdateWebServerStatusDisplay()
        {
            var webServer = WebServerServiceHolder.Instance;
            if (webServer != null && webServer.IsRunning)
            {
                lblWebStatus.Text = L.T("SettingsForm_D084");
                lblWebStatus.ForeColor = Color.Green;
                lblWebUrl.Text = webServer.BaseUrl ?? "";
                btnWebStartStop.Text = L.T("SettingsForm_D085");
            }
            else
            {
                lblWebStatus.Text = L.T("SettingsForm_D086");
                lblWebStatus.ForeColor = Color.Gray;
                lblWebUrl.Text = "";
                btnWebStartStop.Text = L.T("SettingsForm_D046");
            }
        }

        private async void btnWebStartStop_Click(object sender, EventArgs e)
        {
            btnWebStartStop.Enabled = false;
            try
            {
                var webServer = WebServerServiceHolder.Instance;
                if (webServer == null) return;

                if (webServer.IsRunning)
                {
                    await webServer.StopAsync();
                }
                else
                {
                    SaveSettings();
                    var settings = _settingsManager.Settings;
                    await webServer.StartAsync(settings.WebServerPort, settings.WebServerBindAll);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("SettingsForm_D087", ex.Message), L.T("SettingsForm_D011"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnWebStartStop.Enabled = true;
                UpdateWebServerStatusDisplay();
            }
        }

        private void btnWebOpenBrowser_Click(object sender, EventArgs e)
        {
            var webServer = WebServerServiceHolder.Instance;
            if (webServer == null || !webServer.IsRunning) return;

            var port = (int)numWebPort.Value;
            var url = $"http://localhost:{port}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// WebServerService のグローバルホルダー（WPF側MainViewModelで初期化、SettingsForm から参照）
    /// </summary>
    public static class WebServerServiceHolder
    {
        public static WebServerService? Instance { get; set; }
    }

    /// <summary>言語ComboBoxの項目 (Code=設定値, Display=ネイティブ表記)</summary>
    public sealed record LanguageItem(string Code, string Display)
    {
        public override string ToString() => Display;
    }
}
