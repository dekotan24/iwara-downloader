using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using System.Diagnostics;
using System.Text;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 他PCでDL済みのファイル群 (iwara カスタムタグ付き mp4) を読み取って
    /// 動画情報・作者情報を iwara API で逆引きし、現在の DB に取り込むウィザード。
    /// </summary>
    public partial class ImportFromFolderWizard : Form
    {
        // 同時に複数起動しないための静的参照
        private static ImportFromFolderWizard? _instance;

        /// <summary>
        /// モードレスで開く (既に開いていれば最前面に持ってくる)。
        /// owner を渡すと閉じても残るが、Owner が破棄されると挙動が不安定。
        /// 通常は MainForm を owner にする。
        /// </summary>
        public static void ShowOrActivate(IWin32Window? owner, DownloadManager downloadManager)
        {
            if (_instance != null && !_instance.IsDisposed)
            {
                // 最小化されていれば復元
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;

                // 処理中でなければ Step 1 にリセットして再利用
                if (!_instance._busy)
                    _instance.ResetToStep1();

                _instance.Activate();
                _instance.BringToFront();
                return;
            }
            _instance = new ImportFromFolderWizard(downloadManager);
            _instance.FormClosed += (_, _) => _instance = null;
            if (owner != null)
                _instance.Show(owner);
            else
                _instance.Show();
        }

        /// <summary>
        /// ウィザードを最初の状態 (Step 1, スキャン/取り込み結果クリア) に戻す。
        /// 処理中 (_busy=true) は呼び出さないこと。
        /// </summary>
        private void ResetToStep1()
        {
            _step = 1;
            _scanned.Clear();
            _untaggedCount = 0;
            _importedNew = 0;
            _mergedCount = 0;
            _skippedExistingCount = 0;
            _failedCount = 0;
            _untaggedFiles.Clear();
            _apiFailedItems.Clear();
            _dbFailedItems.Clear();
            _lastErrorLogPath = null;

            // UI クリア
            clbAuthors.Items.Clear();
            clbAuthors.Tag = null;
            lblScanResult.Text = "";
            lblScanStatus.Text = "準備中...";
            progressScan.Value = 0;
            progressScan.Style = ProgressBarStyle.Marquee;
            txtImportLog.Clear();
            lblImportStatus.Text = "準備中...";
            progressImport.Value = 0;
            lblSummary.Text = "";
            lblDupNotice.Text = "";
            lblSingleVideos.Text = "";

            UpdateStepUi();
        }

        private readonly DownloadManager _downloadManager;
        private readonly DatabaseService _database;

        private int _step = 1;
        private CancellationTokenSource? _cts;
        private bool _busy;

        // スキャン結果
        private readonly List<ScannedVideo> _scanned = new();
        private int _untaggedCount;

        // 取り込み結果
        private int _importedNew;
        private int _mergedCount;
        private int _skippedExistingCount;
        private int _failedCount;

        // エラー記録 (完了時にログファイル出力する)
        private readonly List<string> _untaggedFiles = new();
        private readonly List<(string VideoId, string Error)> _apiFailedItems = new();
        private readonly List<(string Title, string VideoId, string Error)> _dbFailedItems = new();
        private string? _lastErrorLogPath;

        public ImportFromFolderWizard(DownloadManager downloadManager)
        {
            InitializeComponent();
            _downloadManager = downloadManager;
            _database = DatabaseService.Instance;
            UpdateStepUi();
        }

        private void UpdateStepUi()
        {
            pnlStep1.Visible = _step == 1;
            pnlStep2.Visible = _step == 2;
            pnlStep3.Visible = _step == 3;
            pnlStep4.Visible = _step == 4;
            pnlStep5.Visible = _step == 5;

            lblStep.Text = $"ステップ {_step}/5";

            btnBack.Enabled = _step == 3 && !_busy;
            btnNext.Enabled = !_busy && _step != 2 && _step != 4;
            // 取り込み実行中 (Step 4) と完了画面 (Step 5) はキャンセル不可。
            //   - Step 4: 途中で止めると DB が中途半端な状態で残るので止めさせない
            //             (ウィザードを隠したい場合は「バックグラウンドで実行」を使う)
            //   - Step 5: 既に処理は終わっているので「閉じる」ボタンだけ使う
            btnCancel.Enabled = _step != 4 && _step != 5;
            // 「裏で実行」は処理中 (Step 2/4) のみ表示
            btnHide.Visible = _busy;

            btnNext.Text = _step switch
            {
                2 => "スキャン中...",
                3 => "取り込み実行 >",
                4 => "取り込み中...",
                5 => "閉じる",
                _ => "次へ >",
            };

            btnCancel.Text = _busy ? "中止" : "キャンセル";
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var d = new FolderBrowserDialog
            {
                Description = "スキャンするフォルダを選択",
                UseDescriptionForTitle = true,
                SelectedPath = string.IsNullOrEmpty(txtFolder.Text)
                    ? SettingsManager.Instance.Settings.DownloadFolder
                    : txtFolder.Text,
            };
            if (d.ShowDialog(this) == DialogResult.OK)
                txtFolder.Text = d.SelectedPath;
        }

        private async void btnNext_Click(object sender, EventArgs e)
        {
            switch (_step)
            {
                case 1:
                    if (string.IsNullOrWhiteSpace(txtFolder.Text) || !Directory.Exists(txtFolder.Text))
                    {
                        MessageBox.Show(this, "有効なフォルダを指定してください。",
                            "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (!_downloadManager.IsLoggedIn)
                    {
                        MessageBox.Show(this,
                            "iwara にログインしていません。\n設定画面からログインしてから再度お試しください。",
                            "ログイン必要", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _step = 2;
                    UpdateStepUi();
                    await RunScanAsync();
                    break;

                case 3:
                    _step = 4;
                    UpdateStepUi();
                    await RunImportAsync();
                    break;

                case 5:
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
            }
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            if (_step == 3)
            {
                _step = 1;
                UpdateStepUi();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_busy)
            {
                if (MessageBox.Show(this, "処理を中止しますか?", "確認",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _cts?.Cancel();
                return;
            }
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnHide_Click(object sender, EventArgs e)
        {
            // ウィザードのみ最小化 (タスクバーには残す)
            // メインフォーム・設定画面は引き続き操作可能
            WindowState = FormWindowState.Minimized;
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbAuthors.Items.Count; i++) clbAuthors.SetItemChecked(i, true);
        }

        private void btnSelectNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbAuthors.Items.Count; i++) clbAuthors.SetItemChecked(i, false);
        }

        #region Step 2: スキャン (ファイル列挙 + タグ読取 + iwara API 逆引き)

        private async Task RunScanAsync()
        {
            _busy = true;
            UpdateStepUi();
            _cts = new CancellationTokenSource();
            _scanned.Clear();
            _untaggedCount = 0;
            _untaggedFiles.Clear();
            _apiFailedItems.Clear();
            lblScanResult.Text = "";

            var folder = txtFolder.Text.Trim();
            var recursive = chkRecursive.Checked;
            var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var ct = _cts.Token;

            try
            {
                // Phase A: ファイル列挙 + タグ読取り
                progressScan.Style = ProgressBarStyle.Marquee;
                lblScanStatus.Text = "ファイルを列挙中...";

                var taggedItems = await Task.Run(() =>
                {
                    var list = new List<ScannedVideo>();
                    int processed = 0;
                    var files = Directory.EnumerateFiles(folder, "*.*", searchOpt)
                        .Where(p =>
                        {
                            var ext = Path.GetExtension(p).ToLowerInvariant();
                            return ext == ".mp4" || ext == ".m4v";
                        })
                        .ToList();

                    ReportScan($"スキャン対象: {files.Count} ファイル", null, files.Count);

                    foreach (var f in files)
                    {
                        ct.ThrowIfCancellationRequested();
                        var (videoId, fileUuid) = MetadataService.ReadIwaraTags(f);
                        if (string.IsNullOrEmpty(videoId))
                        {
                            System.Threading.Interlocked.Increment(ref _untaggedCount);
                            lock (_untaggedFiles) _untaggedFiles.Add(f);
                        }
                        else
                        {
                            list.Add(new ScannedVideo
                            {
                                FilePath = f,
                                VideoId = videoId,
                                FileUuid = fileUuid ?? "",
                            });
                        }
                        processed++;
                        if (processed % 20 == 0 || processed == files.Count)
                            ReportScan($"タグ読取り {processed}/{files.Count}", null, files.Count);
                    }
                    return list;
                }, ct);

                _scanned.AddRange(taggedItems);

                ReportScan($"タグ付きファイル {_scanned.Count} 件 / タグ無し {_untaggedCount} 件", null, _scanned.Count);

                // Phase B: 重複videoIdの集約 (同じvideoIdが複数あれば1つだけAPI叩く)
                var uniqueVideoIds = _scanned
                    .GroupBy(s => s.VideoId)
                    .Select(g => g.First())
                    .ToList();

                // Phase C: iwara API で逆引き
                progressScan.Style = ProgressBarStyle.Continuous;
                progressScan.Maximum = Math.Max(1, uniqueVideoIds.Count);

                var apiDelayMs = SettingsManager.Instance.Settings.ApiRequestDelayMs;
                int apiProcessed = 0;
                int apiFailed = 0;
                int apiSkipped = 0;

                foreach (var item in uniqueVideoIds)
                {
                    ct.ThrowIfCancellationRequested();
                    apiProcessed++;
                    ReportScan(
                        $"iwara API 問い合わせ {apiProcessed}/{uniqueVideoIds.Count}: {item.VideoId}",
                        apiProcessed, uniqueVideoIds.Count);

                    // 差分インポート: DB に既に video が存在し、author 情報も埋まってる場合は
                    // API スキップ。中断後の再実行・連続インポートで API 連打を防ぐ。
                    // (API レート制限警告対策にもなる)
                    var existingVideoForApi = _database.GetVideoByVideoId(item.VideoId);
                    if (existingVideoForApi != null && !string.IsNullOrEmpty(existingVideoForApi.AuthorUsername))
                    {
                        item.Title = existingVideoForApi.Title ?? "";
                        item.AuthorUsername = existingVideoForApi.AuthorUsername;
                        // AuthorName は API 専用の表示名なので、DB に保存されてない。
                        // 代用として AuthorUsername をそのまま使う。
                        item.AuthorName = existingVideoForApi.AuthorUsername;
                        if (string.IsNullOrEmpty(item.FileUuid) && !string.IsNullOrEmpty(existingVideoForApi.FileUuid))
                            item.FileUuid = existingVideoForApi.FileUuid;
                        item.ApiOk = true;
                        apiSkipped++;

                        // 重複videoIdの他のScannedにも伝播
                        foreach (var s in _scanned.Where(s => s.VideoId == item.VideoId && s != item))
                        {
                            s.Title = item.Title;
                            s.AuthorUsername = item.AuthorUsername;
                            s.AuthorName = item.AuthorName;
                            s.FileUuid = string.IsNullOrEmpty(s.FileUuid) ? item.FileUuid : s.FileUuid;
                            s.ApiOk = item.ApiOk;
                            s.ApiError = item.ApiError;
                        }
                        // API 叩いてないので apiDelayMs もスキップ
                        continue;
                    }

                    try
                    {
                        // site 未指定で叩く → IwaraApiService 内で iwara.ai に自動フォールバックする
                        var info = await _downloadManager.IwaraApi.GetDownloadUrlAsync(item.VideoId);
                        if (info.Success)
                        {
                            item.Title = info.Title ?? "";
                            item.AuthorUsername = info.AuthorUsername ?? "";
                            item.AuthorName = info.AuthorName ?? "";
                            if (!string.IsNullOrEmpty(info.FileUuid))
                                item.FileUuid = info.FileUuid;
                            if (!string.IsNullOrEmpty(info.ResolvedSite))
                                item.Site = info.ResolvedSite;
                            item.ApiOk = true;
                        }
                        else
                        {
                            item.ApiOk = false;
                            item.ApiError = info.Error ?? "Unknown error";
                            apiFailed++;
                            _apiFailedItems.Add((item.VideoId, item.ApiError));
                            AppendScanResult($"[API 失敗] {item.VideoId}: {info.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        item.ApiOk = false;
                        item.ApiError = ex.Message;
                        apiFailed++;
                        _apiFailedItems.Add((item.VideoId, $"例外: {ex.Message}"));
                        AppendScanResult($"[例外] {item.VideoId}: {ex.Message}");
                    }

                    // 重複videoIdの他のScannedにもAPI結果を伝播
                    foreach (var s in _scanned.Where(s => s.VideoId == item.VideoId && s != item))
                    {
                        s.Title = item.Title;
                        s.AuthorUsername = item.AuthorUsername;
                        s.AuthorName = item.AuthorName;
                        s.FileUuid = string.IsNullOrEmpty(s.FileUuid) ? item.FileUuid : s.FileUuid;
                        s.ApiOk = item.ApiOk;
                        s.ApiError = item.ApiError;
                    }

                    if (apiDelayMs > 0) await Task.Delay(apiDelayMs, ct);
                }

                // Phase D: 作者一覧抽出 (新規作者のみ) と単発カウント
                var existingUsers = await Task.Run(() => _database.GetAllSubscribedUsers());
                var existingUsernames = existingUsers
                    .Select(u => (u.Username ?? "").ToLowerInvariant())
                    .ToHashSet();

                var authorGroups = _scanned
                    .Where(s => s.ApiOk && !string.IsNullOrEmpty(s.AuthorUsername))
                    .GroupBy(s => s.AuthorUsername!.ToLowerInvariant())
                    .Select(g => new AuthorEntry
                    {
                        Username = g.First().AuthorUsername!,
                        DisplayName = g.First().AuthorName ?? g.First().AuthorUsername!,
                        VideoCount = g.Count(),
                        AlreadySubscribed = existingUsernames.Contains(g.Key),
                    })
                    .OrderBy(a => a.AlreadySubscribed)
                    .ThenByDescending(a => a.VideoCount)
                    .ToList();

                int singleVideoCount = _scanned.Count(s => s.ApiOk && string.IsNullOrEmpty(s.AuthorUsername));

                // UI更新 (Step 3 へ)
                clbAuthors.Items.Clear();
                foreach (var a in authorGroups)
                {
                    // AuthorEntry.ToString() で表示文字列が返る。チェック状態は新規ユーザーのみON
                    clbAuthors.Items.Add(a, isChecked: !a.AlreadySubscribed);
                }
                clbAuthors.Tag = authorGroups;

                lblSingleVideos.Text = singleVideoCount > 0
                    ? $"+ 単発動画 (作者情報なし): {singleVideoCount} 件 — 自動的に取り込まれます"
                    : "";

                AppendScanResult(
                    $"=== スキャン完了 ===\r\n" +
                    $"  タグ付きファイル: {_scanned.Count}\r\n" +
                    $"  タグ無し (スキップ): {_untaggedCount}\r\n" +
                    $"  ユニーク videoId: {uniqueVideoIds.Count}\r\n" +
                    $"  API 問い合わせスキップ (DB既存): {apiSkipped}\r\n" +
                    $"  API 取得失敗: {apiFailed}\r\n" +
                    $"  新規作者: {authorGroups.Count(a => !a.AlreadySubscribed)}\r\n" +
                    $"  単発動画: {singleVideoCount}");

                _step = 3;
            }
            catch (OperationCanceledException)
            {
                AppendScanResult("[中止] スキャンを中止しました");
                _step = 1;
            }
            catch (Exception ex)
            {
                AppendScanResult($"[エラー] {ex.Message}");
                MessageBox.Show(this, $"スキャン中にエラー:\n{ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _step = 1;
            }
            finally
            {
                _busy = false;
                _cts?.Dispose();
                _cts = null;
                UpdateStepUi();
            }
        }

        private void ReportScan(string status, int? progressValue, int progressMax)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)(() => ReportScan(status, progressValue, progressMax))); } catch { } return; }
            lblScanStatus.Text = status;
            if (progressValue.HasValue)
            {
                progressScan.Maximum = Math.Max(1, progressMax);
                progressScan.Value = Math.Min(progressScan.Maximum, progressValue.Value);
            }
        }

        private void AppendScanResult(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)(() => AppendScanResult(msg))); } catch { } return; }
            lblScanResult.Text = (lblScanResult.Text + "\r\n" + msg).TrimStart('\r', '\n');
            // 長すぎる場合は末尾だけ残す
            var lines = lblScanResult.Text.Split('\n');
            if (lines.Length > 10)
                lblScanResult.Text = string.Join("\n", lines.Skip(lines.Length - 10));
        }

        #endregion

        #region Step 4: 取り込み実行 (DB書き込み)

        private async Task RunImportAsync()
        {
            _busy = true;
            UpdateStepUi();
            _cts = new CancellationTokenSource();
            _importedNew = _mergedCount = _skippedExistingCount = _failedCount = 0;
            txtImportLog.Clear();
            progressImport.Value = 0;
            lblImportStatus.Text = "準備中...";

            // チェックされた作者ユーザー名を取得
            var authorEntries = clbAuthors.Tag as List<AuthorEntry> ?? new List<AuthorEntry>();
            var selectedUsernames = new HashSet<string>();
            for (int i = 0; i < clbAuthors.Items.Count && i < authorEntries.Count; i++)
            {
                if (clbAuthors.GetItemChecked(i))
                    selectedUsernames.Add(authorEntries[i].Username.ToLowerInvariant());
            }

            var ct = _cts.Token;

            try
            {
                await Task.Run(() =>
                {
                    // 既存購読ユーザーをロード (Username -> SubscribedUser)
                    // ToDictionary は重複キーで例外を投げるので、GroupBy で先に正規化する
                    // (Username 空のエントリや重複が混入していても落ちないように)
                    var existingUsers = _database.GetAllSubscribedUsers()
                        .GroupBy(u => (u.Username ?? "").ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.First());

                    int total = _scanned.Count(s => s.ApiOk);
                    int processed = 0;
                    progressImport.Maximum = Math.Max(1, total);

                    foreach (var sv in _scanned.Where(s => s.ApiOk))
                    {
                        ct.ThrowIfCancellationRequested();
                        processed++;
                        ReportImport($"({processed}/{total}) {sv.Title}", processed, total);

                        try
                        {
                            // 作者 SubscribedUser を解決 (チェック済かつ未登録なら新規追加)
                            SubscribedUser? subUser = null;
                            var authorKey = (sv.AuthorUsername ?? "").ToLowerInvariant();
                            if (!string.IsNullOrEmpty(authorKey))
                            {
                                if (existingUsers.TryGetValue(authorKey, out var existing))
                                {
                                    // UserId が壊れている (空 or "pending:" 接頭辞) なら、
                                    // 今回 API から取得した username で修復する。
                                    // 過去バージョンで UserId="" のまま登録された残骸が居る場合に効く。
                                    if (string.IsNullOrEmpty(existing.UserId)
                                        || existing.UserId.StartsWith("pending:", StringComparison.Ordinal))
                                    {
                                        existing.UserId = sv.AuthorUsername!;
                                        _database.UpdateSubscribedUser(existing);
                                        AppendImportLog($"~ UserId 修復: @{existing.Username}");
                                    }
                                    subUser = existing;
                                }
                                else if (selectedUsernames.Contains(authorKey))
                                {
                                    // 新規 SubscribedUser を作成
                                    // このアプリは UserId = username 運用 (AddSubscribedUserAsync と同じ)。
                                    // UserId が UNIQUE 制約なので "" を入れると2人目以降衝突する。
                                    var newUser = new SubscribedUser
                                    {
                                        Username = sv.AuthorUsername!,
                                        UserId = sv.AuthorUsername!,
                                        ProfileUrl = $"https://www.iwara.tv/profile/{sv.AuthorUsername}/videos",
                                        IsEnabled = true,
                                        CreatedAt = DateTime.Now,
                                        LastCheckedAt = DateTime.Now,
                                    };
                                    var newId = _database.AddSubscribedUser(newUser);
                                    newUser.Id = newId;
                                    existingUsers[authorKey] = newUser;
                                    subUser = newUser;
                                    AppendImportLog($"+ 新規購読: {sv.AuthorName ?? sv.AuthorUsername} (@{newUser.Username})");
                                }
                            }

                            // 既存videoIdチェック
                            var existingVideo = _database.GetVideoByVideoId(sv.VideoId);
                            if (existingVideo != null)
                            {
                                bool existingFileExists =
                                    !string.IsNullOrEmpty(existingVideo.LocalFilePath)
                                    && File.Exists(existingVideo.LocalFilePath);

                                if (!existingFileExists)
                                {
                                    // マージ: LocalFilePath を更新 + 欠けてる author 関連も補完
                                    existingVideo.LocalFilePath = sv.FilePath;
                                    existingVideo.Status = DownloadStatus.Completed;
                                    existingVideo.DownloadedAt = existingVideo.DownloadedAt == default
                                        ? DateTime.Now : existingVideo.DownloadedAt;
                                    try { existingVideo.FileSize = new FileInfo(sv.FilePath).Length; } catch { }
                                    if (string.IsNullOrEmpty(existingVideo.FileUuid) && !string.IsNullOrEmpty(sv.FileUuid))
                                        existingVideo.FileUuid = sv.FileUuid;
                                    // 既存 video の author 情報が欠けてる場合は今回解決した分で補完
                                    if (string.IsNullOrEmpty(existingVideo.AuthorUsername) && !string.IsNullOrEmpty(sv.AuthorUsername))
                                        existingVideo.AuthorUsername = sv.AuthorUsername;
                                    if (subUser != null)
                                    {
                                        if (!existingVideo.SubscribedUserId.HasValue)
                                            existingVideo.SubscribedUserId = subUser.Id;
                                        if (string.IsNullOrEmpty(existingVideo.AuthorUserId))
                                            existingVideo.AuthorUserId = subUser.UserId;
                                    }
                                    _database.UpdateVideo(existingVideo);
                                    _mergedCount++;
                                    AppendImportLog($"≈ マージ: {sv.Title}");
                                }
                                else
                                {
                                    // 実ファイルパスが存在するならインポートをスキップ
                                    _skippedExistingCount++;
                                    AppendImportLog($"→ スキップ (既存ファイルあり): {sv.Title}");
                                }
                                continue;
                            }

                            // 新規追加 (自動フォールバックで判明した site も継承)
                            var v = new VideoInfo
                            {
                                VideoId = sv.VideoId,
                                Title = sv.Title,
                                AuthorUserId = sv.AuthorUsername ?? "",   // UserId = username 運用
                                AuthorUsername = sv.AuthorUsername ?? "",
                                FileUuid = sv.FileUuid,
                                Site = sv.Site,
                                LocalFilePath = sv.FilePath,
                                Status = DownloadStatus.Completed,
                                SubscribedUserId = subUser?.Id,
                                DownloadedAt = DateTime.Now,
                                CreatedAt = DateTime.Now,
                                PostedAt = null,
                            };
                            try { v.FileSize = new FileInfo(sv.FilePath).Length; } catch { }
                            _database.AddVideo(v);
                            _importedNew++;
                            AppendImportLog($"+ 取り込み: {sv.Title}");
                        }
                        catch (Exception ex)
                        {
                            _failedCount++;
                            _dbFailedItems.Add((sv.Title, sv.VideoId, ex.Message));
                            AppendImportLog($"[失敗] {sv.Title}: {ex.Message}");
                            LoggingService.Instance.Warn($"Import 失敗 ({sv.VideoId}): {ex.Message}");
                        }
                    }
                }, ct);

                ShowSummary();
                _step = 5;
            }
            catch (OperationCanceledException)
            {
                AppendImportLog("[中止] 取り込みを中止しました");
                ShowSummary();
                _step = 5;
            }
            catch (Exception ex)
            {
                AppendImportLog($"[エラー] {ex.Message}");
                MessageBox.Show(this, $"取り込み中にエラー:\n{ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // バッファに残ってる最後のログを必ず吐き出す
                _lastImportLogFlush = DateTime.MinValue;
                FlushImportLog();
                _busy = false;
                _cts?.Dispose();
                _cts = null;
                UpdateStepUi();
            }
        }

        private void ShowSummary()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)ShowSummary); } catch { } return; }

            var apiFailed = _scanned.Count(s => !s.ApiOk);

            // エラー詳細を永続ログファイルに書き出す
            // (UI 上の txtImportLog はウィザード閉じると消えるので、
            //  後から「何が失敗したか」追跡できるように)
            _lastErrorLogPath = WriteImportErrorLog();

            lblSummary.Text =
                $"新規取り込み:       {_importedNew} 件\r\n" +
                $"マージ (パス更新):  {_mergedCount} 件\r\n" +
                $"スキップ (既存):    {_skippedExistingCount} 件\r\n" +
                $"タグ無しスキップ:   {_untaggedCount} 件\r\n" +
                $"API 取得失敗:       {apiFailed} 件\r\n" +
                $"DB 書込失敗:        {_failedCount} 件" +
                (string.IsNullOrEmpty(_lastErrorLogPath)
                    ? ""
                    : $"\r\n\r\nエラー詳細ログ:\r\n{_lastErrorLogPath}");

            lblDupNotice.Text = "";

            // バックグラウンド実行で最小化されていた場合は復元してサマリを見せる + バルーン通知
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();

            try
            {
                var msg =
                    $"新規 {_importedNew} / マージ {_mergedCount} / スキップ(既存) {_skippedExistingCount}";
                Services.NotificationService.Instance.ShowNotification("インポート完了", msg);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"通知失敗: {ex.Message}");
            }

            // MainForm にチャンネル一覧 + 動画リスト更新を通知
            // (Owner 経由で渡しても良いが、SettingsForm 経由で開かれた場合に
            //  Owner が SettingsForm になってる可能性があるので OpenForms から直接探す)
            try
            {
                foreach (Form f in Application.OpenForms)
                {
                    if (f is MainForm mf) { mf.RefreshAfterImport(); break; }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainForm refresh 通知失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// インポートで発生したエラーを永続ログファイルに書き出す。
        /// エラーが1件も無ければファイルは作成せず null を返す。
        /// </summary>
        private string? WriteImportErrorLog()
        {
            int totalErrors = _untaggedFiles.Count + _apiFailedItems.Count + _dbFailedItems.Count;
            if (totalErrors == 0) return null;

            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "logs");
                Directory.CreateDirectory(logDir);

                var path = Path.Combine(logDir,
                    $"import_errors_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                var sb = new StringBuilder();
                sb.AppendLine("=== IwaraDownloader Import Errors ===");
                sb.AppendLine($"日時 : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"対象フォルダ : {txtFolder.Text}");
                sb.AppendLine($"再帰スキャン : {chkRecursive.Checked}");
                sb.AppendLine();
                sb.AppendLine($"タグ無しスキップ : {_untaggedFiles.Count} 件");
                sb.AppendLine($"API 取得失敗     : {_apiFailedItems.Count} 件");
                sb.AppendLine($"DB 書込失敗      : {_dbFailedItems.Count} 件");
                sb.AppendLine();

                if (_untaggedFiles.Count > 0)
                {
                    sb.AppendLine("--- タグ無しスキップ (mp4 内に iwara カスタムタグが無いファイル) ---");
                    foreach (var f in _untaggedFiles) sb.AppendLine(f);
                    sb.AppendLine();
                }
                if (_apiFailedItems.Count > 0)
                {
                    sb.AppendLine("--- API 取得失敗 (videoId / 理由) ---");
                    foreach (var it in _apiFailedItems)
                        sb.AppendLine($"{it.VideoId}\t{it.Error}");
                    sb.AppendLine();
                }
                if (_dbFailedItems.Count > 0)
                {
                    sb.AppendLine("--- DB 書込失敗 (videoId / タイトル / 理由) ---");
                    foreach (var it in _dbFailedItems)
                        sb.AppendLine($"{it.VideoId}\t{it.Title}\t{it.Error}");
                    sb.AppendLine();
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                // アプリ全体ログにも要約を残す
                LoggingService.Instance.Warn(
                    $"Import errors: 無タグ {_untaggedFiles.Count}, API失敗 {_apiFailedItems.Count}, DB失敗 {_dbFailedItems.Count}, log={path}");

                return path;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"WriteImportErrorLog failed: {ex.Message}");
                return null;
            }
        }

        private void ReportImport(string status, int processed, int total)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)(() => ReportImport(status, processed, total))); } catch { } return; }
            lblImportStatus.Text = status;
            progressImport.Maximum = Math.Max(1, total);
            progressImport.Value = Math.Min(progressImport.Maximum, processed);
        }

        // ログを大量出力するとBeginInvokeのコストでUIが詰まるのでバッファリング。
        // 200ms ごと or バッファに50行たまったら一括フラッシュ。
        private readonly StringBuilder _importLogBuffer = new();
        private DateTime _lastImportLogFlush = DateTime.MinValue;
        private const int ImportLogFlushIntervalMs = 200;
        private const int ImportLogFlushBatchLines = 50;

        private void AppendImportLog(string msg)
        {
            if (IsDisposed) return;
            int lineCount;
            lock (_importLogBuffer)
            {
                _importLogBuffer.AppendLine(msg);
                lineCount = _importLogBuffer.Length > 0 ? CountNewLines(_importLogBuffer) : 0;
            }
            var elapsed = (DateTime.Now - _lastImportLogFlush).TotalMilliseconds;
            if (elapsed < ImportLogFlushIntervalMs && lineCount < ImportLogFlushBatchLines)
                return;

            _lastImportLogFlush = DateTime.Now;
            if (InvokeRequired) { try { BeginInvoke((Action)FlushImportLog); } catch { } return; }
            FlushImportLog();
        }

        private static int CountNewLines(StringBuilder sb)
        {
            int n = 0;
            for (int i = 0; i < sb.Length; i++)
                if (sb[i] == '\n') n++;
            return n;
        }

        private void FlushImportLog()
        {
            if (IsDisposed) return;
            string text;
            lock (_importLogBuffer)
            {
                if (_importLogBuffer.Length == 0) return;
                text = _importLogBuffer.ToString();
                _importLogBuffer.Clear();
            }
            txtImportLog.AppendText(text);
        }

        #endregion

        private void ImportFromFolderWizard_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy)
            {
                var r = MessageBox.Show(this, "処理中です。中止して閉じますか?",
                    "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
                _cts?.Cancel();
            }
        }

        // 内部データ型
        private class ScannedVideo
        {
            public string FilePath = "";
            public string VideoId = "";
            public string FileUuid = "";
            public string Title = "";
            public string? AuthorUsername;
            public string? AuthorName;
            public string Site = "";  // 自動 site フォールバックで判明した場合に格納
            public bool ApiOk;
            public string? ApiError;
        }

        private class AuthorEntry
        {
            public string Username = "";
            public string DisplayName = "";
            public int VideoCount;
            public bool AlreadySubscribed;

            public override string ToString()
            {
                var disp = string.IsNullOrEmpty(DisplayName) || DisplayName == Username
                    ? Username : $"{DisplayName} (@{Username})";
                return AlreadySubscribed
                    ? $"{disp} — {VideoCount} 件  [既に登録済]"
                    : $"{disp} — {VideoCount} 件";
            }
        }
    }
}
