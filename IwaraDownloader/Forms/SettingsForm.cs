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
            lblCurrentVersion.Text = $"現在: {UpdateService.CurrentVersionString}";

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

            // サムネ保存先の移動元は DownloadFolder 更新前 (=現状の実フォルダ) で確定させる
            var oldThumbDir = ThumbnailCacheService.ResolveCacheDir();

            // ダウンロード設定
            settings.DownloadFolder = txtDownloadFolder.Text;
            if (cmbQuality.SelectedIndex >= 0 && cmbQuality.SelectedIndex < _qualityOrder.Length)
            {
                settings.DefaultQuality = _qualityOrder[cmbQuality.SelectedIndex];
            }
            settings.MaxConcurrentDownloads = (int)numConcurrent.Value;
            settings.MaxRetryCount = (int)numRetry.Value;
            settings.MinFreeSpaceGb = (int)numMinFreeSpace.Value;

            // サムネ保存先 (変更時は既存キャッシュを新フォルダへ移動)
            settings.ThumbnailCacheLocation = cmbThumbLocation.SelectedIndex == 1 ? 1 : 0;
            var newThumbDir = ThumbnailCacheService.ResolveCacheDir();
            if (!string.Equals(oldThumbDir, newThumbDir, StringComparison.OrdinalIgnoreCase))
            {
                // フォーム閉鎖後も完走するようバックグラウンドで移動。
                // 移動が間に合わないファイルはキャッシュミス扱いで再DLされるだけなので安全。
                _ = Task.Run(() => ThumbnailCacheService.MigrateCacheDir(oldThumbDir, newThumbDir));
            }

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

        private void btnBrowseYtDlp_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "yt-dlp実行ファイル (yt-dlp.exe;yt-dlp)|yt-dlp.exe;yt-dlp|すべてのファイル (*.*)|*.*",
                Title = "yt-dlpの実行ファイルを選択"
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
                MessageBox.Show("メールアドレスとパスワードを入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 先に設定を保存(Pythonパスを含む)
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
            if (!ApplySettings()) return; // 保存先変更がキャンセルされた場合は閉じない
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (!ApplySettings()) return;
            MessageBox.Show("設定を保存しました。", "設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        "ダウンロード実行中は保存先フォルダを変更できません。\n" +
                        "完了を待つか、キューを停止してから再度実行してください。",
                        "保存先変更", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // 個別保存先 (CustomSavePath) を設定したチャンネルのファイルは動かさない
                var excludeBases = _database.GetAllSubscribedUsers()
                    .Where(u => !string.IsNullOrWhiteSpace(u.CustomSavePath))
                    .Select(u => u.CustomSavePath);

                var movable = FileMoveHelper.GetMovableFiles(
                    _database.GetAllVideos(), oldFolder, excludeBases);

                var decision = FileMoveHelper.ConfirmMove(
                    this, "ダウンロード保存先", movable, oldFolder, newFolder);
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
                    $"ファイル移動が完了しました。\n\n" +
                    $"成功: {progressForm.MovedCount} 件 / 失敗: {progressForm.FailedCount} 件\n" +
                    (progressForm.FailedCount > 0
                        ? "\n失敗したファイルは元の場所に残っています。詳細はログを確認してください。"
                        : ""),
                    "移動完了", MessageBoxButtons.OK,
                    progressForm.FailedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            return true;
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

        private void btnMigrateExistingFiles_Click(object sender, EventArgs e)
        {
            if (_downloadManager == null)
            {
                MessageBox.Show("DownloadManager が利用できません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_downloadManager.IsMigrationRunning)
            {
                MessageBox.Show(this, "既に実行中です。完了まで待ってから再度開始してください。",
                    "実行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "ダウンロード済みの mp4 ファイルに iwara の UUID タグを書き込みます。\n" +
                "DB に登録されている動画のうち、ローカルにファイルが存在するもの全てが対象です。\n\n" +
                "★ バックグラウンドで実行します。設定画面を閉じてもアプリ起動中は処理が続きます。\n" +
                "  進捗はメイン画面のステータスバーに表示されます。\n\n" +
                "続行しますか？",
                "既存ファイルにタグを書き込む",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (_downloadManager.StartMigrateExistingFiles())
            {
                MessageBox.Show(this, "バックグラウンドで開始しました。\nこの画面を閉じても処理は継続されます。",
                    "開始", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("DownloadManager が利用できません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_downloadManager.IsBackfillRunning)
            {
                MessageBox.Show(this, "既に実行中です。完了まで待ってから再度開始してください。",
                    "実行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "DB 内の動画のうちサムネ URL が未保存 / 画像未キャッシュのものを補完します。\n\n" +
                "・FileUuid を既に持つ動画 → 即時組み立て (API リクエスト不要)\n" +
                "・FileUuid 無し動画 → iwara API で取得 (要ログイン・時間かかる)\n" +
                "・確定後、サムネ画像もネット DL (レート制限あり)\n\n" +
                "★ バックグラウンドで実行します。設定画面を閉じてもアプリ起動中は処理が続きます。\n" +
                "  進捗はメイン画面のステータスバーに表示されます。\n\n" +
                "続行しますか？",
                "サムネ URL 一括補完",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (_downloadManager.StartBackfillThumbnails())
            {
                MessageBox.Show(this, "バックグラウンドで開始しました。\nこの画面を閉じても処理は継続されます。",
                    "開始", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Title = "音声ファイルを選択",
                Filter = "音声ファイル (*.wav;*.mp3;*.m4a)|*.wav;*.mp3;*.m4a|すべてのファイル (*.*)|*.*"
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
                MessageBox.Show("指定されたファイルが見つかりません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnBrowseErrorSound_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "エラー音声ファイルを選択",
                Filter = "音声ファイル (*.wav;*.mp3;*.m4a)|*.wav;*.mp3;*.m4a|すべてのファイル (*.*)|*.*"
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
                MessageBox.Show("指定されたファイルが見つかりません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("テンプレートを入力してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // DL済みの動画を取得
            var completedVideos = _database.GetVideosByStatus(DownloadStatus.Completed)
                .Where(v => !string.IsNullOrEmpty(v.LocalFilePath))
                .ToList();

            if (completedVideos.Count == 0)
            {
                MessageBox.Show("リネーム対象のファイルがありません。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnRenameFiles.Enabled = false;
            btnRenameFiles.Text = "スキャン中...";

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
                    $"リネーム対象: {completedVideos.Count}件\n\n" +
                    $"・処理可能: {pendingCount}件\n" +
                    $"・重複あり: {conflictCount}件\n" +
                    $"・ファイル不在: {notFoundCount}件\n\n" +
                    $"重複ファイルは結果画面で個別に処理できます。\n" +
                    $"続行しますか？",
                    "重複警告",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (warningResult != DialogResult.Yes)
                {
                    btnRenameFiles.Enabled = true;
                    btnRenameFiles.Text = "DL済みファイルを一括リネーム";
                    return;
                }
            }
            else if (pendingCount > 0)
            {
                var confirmResult = MessageBox.Show(
                    $"{pendingCount}件のファイルをリネームします。\n\n" +
                    $"テンプレート: {template}\n\n" +
                    "この操作は取り消しできません。続行しますか？",
                    "一括リネーム確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    btnRenameFiles.Enabled = true;
                    btnRenameFiles.Text = "DL済みファイルを一括リネーム";
                    return;
                }
            }

            btnRenameFiles.Text = "リネーム中...";

            // Pending状態のファイルをリネーム
            await Task.Run(() =>
            {
                foreach (var item in items.Where(i => i.Status == RenameStatus.Pending))
                {
                    try
                    {
                        // ファイルをリネーム
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
            btnRenameFiles.Text = "DL済みファイルを一括リネーム";

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
                "積極的プリセットはエラーが発生しやすくなります。\n" +
                "403/429エラーが頻発する場合は、標準または控えめに変更してください。",
                "注意",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        #endregion

        #region Update Check

        private async void btnCheckUpdateNow_Click(object sender, EventArgs e)
        {
            btnCheckUpdateNow.Enabled = false;
            btnCheckUpdateNow.Text = "チェック中...";

            try
            {
                var result = await UpdateService.CheckForUpdateAsync();

                if (!result.Success)
                {
                    MessageBox.Show($"更新チェックに失敗しました:\n{result.ErrorMessage}", 
                        "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (result.HasUpdate)
                {
                    var dialogResult = MessageBox.Show(
                        $"新しいバージョンがあります！\n\n" +
                        $"現在: {UpdateService.CurrentVersionString}\n" +
                        $"最新: {result.LatestVersionString}\n\n" +
                        $"ダウンロードページを開きますか？",
                        "更新があります",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
                else
                {
                    MessageBox.Show($"最新バージョンです！\n\n現在: {UpdateService.CurrentVersionString}", 
                        "更新チェック", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                btnCheckUpdateNow.Enabled = true;
                btnCheckUpdateNow.Text = "今すぐチェック";
            }
        }

        #endregion

        #region Web Media Server

        private void UpdateWebServerStatusDisplay()
        {
            var webServer = WebServerServiceHolder.Instance;
            if (webServer != null && webServer.IsRunning)
            {
                lblWebStatus.Text = "稼働中";
                lblWebStatus.ForeColor = Color.Green;
                lblWebUrl.Text = webServer.BaseUrl ?? "";
                btnWebStartStop.Text = "停止";
            }
            else
            {
                lblWebStatus.Text = "停止中";
                lblWebStatus.ForeColor = Color.Gray;
                lblWebUrl.Text = "";
                btnWebStartStop.Text = "開始";
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
                MessageBox.Show($"サーバー操作に失敗しました:\n{ex.Message}", "エラー",
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
    /// WebServerService のグローバルホルダー（MainForm で初期化、SettingsForm から参照）
    /// </summary>
    public static class WebServerServiceHolder
    {
        public static WebServerService? Instance { get; set; }
    }
}
