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
            _titleMatches.Clear();
            _titleMatchImported = 0;
            _titleMatchApiFailed = 0;
            _alreadyOwnedFiles.Clear();

            // UI クリア
            clbAuthors.Items.Clear();
            clbAuthors.Tag = null;
            dgvTitleMatches.Rows.Clear();
            lblScanResult.Text = "";
            lblScanStatus.Text = L.T("ImportFromFolderWizard_D001");
            progressScan.Value = 0;
            progressScan.Style = ProgressBarStyle.Marquee;
            txtImportLog.Clear();
            lblImportStatus.Text = L.T("ImportFromFolderWizard_D001");
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

        // タイトル照合結果 (タグ無しファイルをファイル名から推測して照合した候補)
        private readonly List<TitleMatchDisplayItem> _titleMatches = new();
        private int _titleMatchImported;
        private int _titleMatchApiFailed;
        // videoId直接一致で「既に別の場所にDL済み」と判明したファイル (重複コピー、取り込み対象外)
        private readonly List<string> _alreadyOwnedFiles = new();

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

        // 大量取り込み時の停止箇所をアプリ全体ログで追跡するための実行単位。
        // 1ファイルごとのInfoログは性能とログサイズに影響するため出力しない。
        private string? _importTraceId;
        private Stopwatch? _importTraceStopwatch;

        // 進捗表示は最新値だけをUIタイマーで反映する。1ファイルごとのBeginInvokeは、
        // 4万件規模だとUIキューを埋め尽くして完了処理を遅延させるため行わない。
        private readonly object _importProgressLock = new();
        private System.Windows.Forms.Timer? _importProgressTimer;
        private bool _hasPendingImportProgress;
        private string _pendingImportStatus = "";
        private int _pendingImportProcessed;
        private int _pendingImportTotal;

        public ImportFromFolderWizard(DownloadManager downloadManager)
        {
            InitializeComponent();
            Utils.Localizer.Apply(this);
            _downloadManager = downloadManager;
            _database = DatabaseService.Instance;
            _importProgressTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _importProgressTimer.Tick += (_, _) => FlushPendingImportProgress();
            FormClosed += (_, _) =>
            {
                _importProgressTimer?.Stop();
                _importProgressTimer?.Dispose();
                _importProgressTimer = null;
            };
            UpdateStepUi();
        }

        private void UpdateStepUi()
        {
            pnlStep1.Visible = _step == 1;
            pnlStep2.Visible = _step == 2;
            pnlStep3.Visible = _step == 3;
            pnlStep4.Visible = _step == 4;
            pnlStep5.Visible = _step == 5;

            lblStep.Text = L.T("ImportFromFolderWizard_D002", _step);

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
                2 => L.T("ImportFromFolderWizard_D003"),
                3 => L.T("ImportFromFolderWizard_D004"),
                4 => L.T("ImportFromFolderWizard_D005"),
                5 => L.T("ImportFromFolderWizard_D006"),
                _ => L.T("ImportFromFolderWizard_D007"),
            };

            btnCancel.Text = _busy ? L.T("ImportFromFolderWizard_D008") : L.T("ImportFromFolderWizard_D009");
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var d = new FolderBrowserDialog
            {
                Description = L.T("ImportFromFolderWizard_D010"),
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
                        MessageBox.Show(this, L.T("ImportFromFolderWizard_D011"),
                            L.T("ImportFromFolderWizard_D012"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (!_downloadManager.IsLoggedIn)
                    {
                        MessageBox.Show(this,
                            L.T("ImportFromFolderWizard_D013"),
                            L.T("ImportFromFolderWizard_D014"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (MessageBox.Show(this, L.T("ImportFromFolderWizard_D015"), L.T("ImportFromFolderWizard_D016"),
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

        /// <summary>
        /// チェックボックスセルはクリック直後だと編集がコミットされておらず Value が古い値のままなので、
        /// EndEdit で即座にコミットする (DataGridView の定番の落とし穴)。
        /// </summary>
        private void dgvTitleMatches_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != colTMChecked.Index) return;
            dgvTitleMatches.EndEdit();
        }

        /// <summary>
        /// 想定外のセル書式化エラー (作者列の ComboBox/TextBox 切り替え周り等) が起きても、
        /// 既定の DataError ダイアログを連打させずログに残すだけにする保険。
        /// </summary>
        private void dgvTitleMatches_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            LoggingService.Instance.Warn(
                $"dgvTitleMatches DataError: row={e.RowIndex} col={e.ColumnIndex} ctx={e.Context}: {e.Exception?.Message}");
            e.ThrowException = false;
        }

        /// <summary>
        /// 作者列のセルを組み立てる。1ファイルに複数候補があって自動確定できなかった行だけ
        /// ドロップダウン (DataGridViewComboBoxCell) にし、他候補への選び直しを可能にする。
        /// それ以外の行は通常のテキスト表示 (ReadOnly) のまま。
        /// </summary>
        private void PopulateArtistCell(DataGridViewRow row, TitleMatchDisplayItem m)
        {
            var alternatives = m.Candidate.AlternativeCandidates;
            if (alternatives is { Count: > 1 })
            {
                var comboCell = new DataGridViewComboBoxCell();
                var options = alternatives.Select(v => new ArtistCandidateOption(v)).ToArray();
                // DataGridView にアタッチする前に Value を設定すると例外になることがあるため、
                // 差し替え → Items → Value → ReadOnly の順で行う。
                row.Cells[colTMArtist.Index] = comboCell;
                comboCell.Items.AddRange(options);
                comboCell.Value = options.FirstOrDefault(o => ReferenceEquals(o.Video, m.SelectedVideo)) ?? options[0];
                comboCell.ReadOnly = false;
                comboCell.ToolTipText = L.T("ImportFromFolderWizard_TMArtistDropdownHint");
            }
            else
            {
                // 列自体は DataGridViewComboBoxColumn なので、この行にセットするだけでは
                // Items 無しの ComboBoxCell に書式化不能な値を入れることになり FormatException
                // (DataError) を招く。選び直しの余地が無い行は普通のテキストセルに差し替える。
                var textCell = new DataGridViewTextBoxCell();
                row.Cells[colTMArtist.Index] = textCell;
                textCell.Value = m.SelectedVideo.AuthorUsername;
                textCell.ReadOnly = true;
            }
        }

        /// <summary>
        /// ComboBox セルは選択直後だと編集がコミットされておらず CellValueChanged が飛ばないので、
        /// 即座にコミットする (チェックボックス列と同じ定番の落とし穴)。
        /// </summary>
        private void dgvTitleMatches_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvTitleMatches.CurrentCell is DataGridViewComboBoxCell && dgvTitleMatches.IsCurrentCellDirty)
                dgvTitleMatches.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        /// <summary>
        /// 作者ドロップダウンで選び直された時、表示中の TitleMatchDisplayItem.SelectedVideo と
        /// タイトル/長さ差/確度の各列を更新する。選び直した動画に対して長さ差を再計算することで
        /// 「どの候補を選んでも長さが合わない」ようなケースをこの場で気付けるようにする。
        /// </summary>
        private void dgvTitleMatches_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != colTMArtist.Index) return;
            var row = dgvTitleMatches.Rows[e.RowIndex];
            if (row.Tag is not TitleMatchDisplayItem item) return;
            if (row.Cells[colTMArtist.Index].Value is not ArtistCandidateOption opt) return;
            if (ReferenceEquals(item.SelectedVideo, opt.Video)) return;

            item.SelectedVideo = opt.Video;
            item.ManuallySelected = true;

            double? diff = null;
            bool durationOk = false;
            if (opt.Video.DurationSeconds > 0)
            {
                var fileDuration = TryReadDuration(item.Candidate.FilePath);
                if (fileDuration.HasValue)
                {
                    diff = Math.Abs(fileDuration.Value - opt.Video.DurationSeconds);
                    durationOk = diff.Value <= 5.0;
                }
            }
            item.DurationDiffSeconds = diff;
            item.DurationOk = durationOk;

            row.Cells[colTMTitle.Index].Value = opt.Video.Title;
            row.Cells[colTMDuration.Index].Value = item.DurationLabel;
            row.Cells[colTMConfidence.Index].Value = item.ConfidenceLabel;
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
            // 戻る→次へで再スキャンした際に前回分が残って重複表示されるのを防ぐ
            _titleMatches.Clear();
            _alreadyOwnedFiles.Clear();
            _dbFailedItems.Clear();
            lblScanResult.Text = "";

            var folder = txtFolder.Text.Trim();
            var recursive = chkRecursive.Checked;
            var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var ct = _cts.Token;

            try
            {
                // Phase A: ファイル列挙 + タグ読取り
                progressScan.Style = ProgressBarStyle.Marquee;
                lblScanStatus.Text = L.T("ImportFromFolderWizard_D017");

                var scanResult = await Task.Run(() =>
                {
                    // 拡張子の判定をLINQで全ファイルに対して行わず、列挙段階でmp4だけに絞る。
                    var files = Directory.EnumerateFiles(folder, "*.mp4", searchOpt).ToList();
                    ReportScan(L.T("ImportFromFolderWizard_ScanTarget", files.Count), null, files.Count);

                    var scanResults = new (string VideoId, string FileUuid)[files.Count];
                    var cacheEntries = new ImportScanCacheService.Entry[files.Count];
                    var cache = ImportScanCacheService.Load(folder);
                    int processed = 0;

                    // TagLib#はファイル単位で完結するため、同時実行数を抑えて読み取りだけ並列化する。
                    // 無制限並列はHDDやネットワークドライブで逆に遅くなり、ファイルハンドルも圧迫する。
                    Parallel.For(
                        0,
                        files.Count,
                        new ParallelOptions
                        {
                            CancellationToken = ct,
                            MaxDegreeOfParallelism = 4,
                        },
                        i =>
                        {
                            ct.ThrowIfCancellationRequested();
                            var filePath = files[i];
                            string videoId = "";
                            string fileUuid = "";
                            long fileSize = 0;
                            long mtimeTicks = 0;
                            bool stampRead = false;

                            try
                            {
                                var info = new FileInfo(filePath);
                                fileSize = info.Length;
                                mtimeTicks = info.LastWriteTimeUtc.Ticks;
                                stampRead = true;
                            }
                            catch
                            {
                                // ReadIwaraTags側で従来通り再判定する。stampが取れないファイルはキャッシュしない。
                            }

                            if (!stampRead
                                || !ImportScanCacheService.TryGet(
                                    cache, folder, filePath, fileSize, mtimeTicks,
                                    out videoId, out fileUuid))
                            {
                                (videoId, fileUuid) = MetadataService.ReadIwaraTags(filePath);
                            }

                            scanResults[i] = (videoId ?? "", fileUuid ?? "");
                            // 空の結果は「本当にタグ無し」と「一時的な読み取り失敗」を
                            // 区別できないため、次回再試行できるよう保存しない。
                            if (stampRead && !string.IsNullOrEmpty(videoId))
                            {
                                cacheEntries[i] = ImportScanCacheService.CreateEntry(
                                    fileSize, mtimeTicks, videoId, fileUuid);
                            }

                            var done = System.Threading.Interlocked.Increment(ref processed);
                            if (done % 20 == 0 || done == files.Count)
                            {
                                ReportScan(
                                    L.T("ImportFromFolderWizard_TagReading", done, files.Count),
                                    null, files.Count);
                            }
                        });

                    var taggedItems = new List<ScannedVideo>();
                    var untaggedFiles = new List<string>();
                    var cacheToSave = new Dictionary<string, ImportScanCacheService.Entry>(
                        StringComparer.OrdinalIgnoreCase);

                    // 結果は元の列挙順に組み立て、従来の表示・重複判定の順序を変えない。
                    for (int i = 0; i < files.Count; i++)
                    {
                        var filePath = files[i];
                        var (videoId, fileUuid) = scanResults[i];
                        if (string.IsNullOrEmpty(videoId))
                        {
                            untaggedFiles.Add(filePath);
                        }
                        else
                        {
                            taggedItems.Add(new ScannedVideo
                            {
                                FilePath = filePath,
                                VideoId = videoId,
                                FileUuid = fileUuid,
                            });
                        }

                        if (cacheEntries[i] != null)
                        {
                            cacheToSave[ImportScanCacheService.GetRelativeKey(folder, filePath)] = cacheEntries[i];
                        }
                    }

                    // キャンセル中はここまで到達しないため、不完全なキャッシュは保存しない。
                    ImportScanCacheService.Save(folder, cacheToSave);
                    return (TaggedItems: taggedItems, UntaggedFiles: untaggedFiles);
                }, ct);

                _scanned.AddRange(scanResult.TaggedItems);
                _untaggedFiles.AddRange(scanResult.UntaggedFiles);
                _untaggedCount = _untaggedFiles.Count;

                ReportScan(L.T("ImportFromFolderWizard_TaggedUntaggedCount", _scanned.Count, _untaggedCount), null, _scanned.Count);

                // Phase A2: タグ無しファイルを一覧表示し、ファイル/フォルダ単位で照合方法を割り当てる。
                // (アーティストフォルダ選択検索 / ファイル名による検索(最終手段) / スキップ)。
                // ファイル名だけを頼りにした全体検索は誤マッチの温床になるため、ユーザーが明示的に割り当てる。
                if (_untaggedFiles.Count > 0)
                {
                    var scanRoot = txtFolder.Text.Trim();
                    IReadOnlyList<UntaggedFileMatchForm.UntaggedMatchPlan>? matchPlans;
                    using (var matchForm = new UntaggedFileMatchForm(_untaggedFiles.ToList(), _database, _downloadManager, scanRoot))
                    {
                        var dr = matchForm.ShowDialog(this);
                        matchPlans = dr == DialogResult.OK ? matchForm.Plans : null;
                    }

                    if (matchPlans != null)
                    {
                        foreach (var plan in matchPlans)
                        {
                            var displayItems = await Task.Run(
                                () => BuildTitleMatchDisplayItems(plan.Result.Matches, plan.ResolvedSubUser, ct), ct);

                            // マッチ / 重複判明したファイルは「タグ無しスキップ」から除外する
                            // (拾えたのに失敗扱いのままだと後のエラーログで混乱するため)
                            var resolvedFiles = displayItems.Select(m => m.Candidate.FilePath)
                                .Concat(plan.Result.AlreadyOwnedFiles)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                            _untaggedFiles.RemoveAll(f => resolvedFiles.Contains(f));
                            _titleMatches.AddRange(displayItems);
                            _alreadyOwnedFiles.AddRange(plan.Result.AlreadyOwnedFiles);
                        }
                        _untaggedCount = _untaggedFiles.Count;

                        ReportScan(
                            L.T("ImportFromFolderWizard_TitleMatchSummary",
                                _titleMatches.Count, _titleMatches.Count(m => m.HighConfidence),
                                _alreadyOwnedFiles.Count, _untaggedCount),
                            null, 1);
                    }
                    // スキップ/キャンセルなら _untaggedFiles はそのまま (従来通りエラーログに記録される)
                }

                // Phase B: 重複videoIdの集約 (同じvideoIdが複数あれば1つだけAPI叩く)。
                // グループも保持しておき、API結果の伝播で全件走査(O(動画数×ユニークID数))に
                // ならないようにする。
                var scannedGroups = _scanned
                    .GroupBy(s => s.VideoId)
                    .ToList();
                var uniqueVideoIds = scannedGroups.Select(g => g.First()).ToList();
                var scannedByVideoId = scannedGroups.ToDictionary(
                    g => g.Key, g => g.ToList(), StringComparer.Ordinal);

                // APIスキップ判定用のDB参照を動画IDごとの接続開閉から1回の読み取りにまとめる。
                // 参照時点のスナップショットは従来のループ開始時と同じで、APIの判定条件は変えない。
                var existingVideosById = uniqueVideoIds.Count == 0
                    ? new Dictionary<string, VideoInfo>(StringComparer.Ordinal)
                    : await Task.Run(() => _database.GetAllVideos()
                        .Where(v => !string.IsNullOrEmpty(v.VideoId))
                        // GetVideoByVideoId() に ORDER BY は無く、旧DBに重複行がある場合は
                        // 通常最初に作られた行(Idの小さい行)が代表になるため、その順序を維持する。
                        .OrderBy(v => v.Id)
                        .GroupBy(v => v.VideoId, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal), ct);

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
                        L.T("ImportFromFolderWizard_ApiQuerying", apiProcessed, uniqueVideoIds.Count, item.VideoId),
                        apiProcessed, uniqueVideoIds.Count);

                    // 差分インポート: DB に既に video が存在し、author 情報も埋まってる場合は
                    // API スキップ。中断後の再実行・連続インポートで API 連打を防ぐ。
                    // (API レート制限警告対策にもなる)
                    existingVideosById.TryGetValue(item.VideoId, out var existingVideoForApi);
                    if (existingVideoForApi != null && !string.IsNullOrEmpty(existingVideoForApi.AuthorUsername))
                    {
                        item.Title = string.IsNullOrEmpty(existingVideoForApi.Title)
                            ? item.VideoId : existingVideoForApi.Title;
                        item.Url = existingVideoForApi.Url;
                        item.Site = existingVideoForApi.Site;
                        item.ThumbnailUrl = existingVideoForApi.ThumbnailUrl;
                        item.DurationSeconds = existingVideoForApi.DurationSeconds;
                        item.PostedAt = existingVideoForApi.PostedAt;
                        item.Rating = existingVideoForApi.Rating;
                        item.EmbedUrl = existingVideoForApi.EmbedUrl;
                        item.ApiRawJson = existingVideoForApi.ApiRawJson;
                        item.AuthorUsername = existingVideoForApi.AuthorUsername;
                        // AuthorName は API 専用の表示名なので、DB に保存されてない。
                        // 代用として AuthorUsername をそのまま使う。
                        item.AuthorName = existingVideoForApi.AuthorUsername;
                        if (string.IsNullOrEmpty(item.FileUuid) && !string.IsNullOrEmpty(existingVideoForApi.FileUuid))
                            item.FileUuid = existingVideoForApi.FileUuid;
                        item.ApiOk = true;
                        apiSkipped++;

                        // 重複videoIdの他のScannedにも伝播
                        PropagateToDuplicates(item, scannedByVideoId[item.VideoId]);
                        // API 叩いてないので apiDelayMs もスキップ
                        continue;
                    }

                    try
                    {
                        // site 未指定で叩く → IwaraApiService 内で iwara.ai に自動フォールバックする
                        // ファイルタグにUUIDがある場合、取り込みに必要なのは動画メタデータだけ。
                        // filesq/CDN問い合わせを含むダウンロードURL取得を避ける。
                        var info = string.IsNullOrEmpty(item.FileUuid)
                            ? await _downloadManager.IwaraApi.GetDownloadUrlAsync(item.VideoId)
                            : await _downloadManager.IwaraApi.GetVideoInfoAsync(item.VideoId);
                        if (info.Success)
                        {
                            item.Title = string.IsNullOrEmpty(info.Title) ? item.VideoId : info.Title;
                            item.ThumbnailUrl = info.ThumbnailUrl ?? "";
                            item.DurationSeconds = info.DurationSeconds;
                            item.PostedAt = info.PostedAt;
                            item.Rating = info.Rating ?? "";
                            item.EmbedUrl = info.EmbedUrl ?? "";
                            item.ApiRawJson = info.ApiRawJson ?? "";
                            item.AuthorUsername = info.AuthorUsername ?? "";
                            item.AuthorName = info.AuthorName ?? "";
                            if (!string.IsNullOrEmpty(info.FileUuid))
                                item.FileUuid = info.FileUuid;
                            if (!string.IsNullOrEmpty(info.ResolvedSite))
                                item.Site = info.ResolvedSite;
                            // get_url の Url は期限付きのDL URLなのでDBには保存せず、
                            // 常に現在のサイトに対する動画ページURLを保存する。
                            item.Url = $"https://{(string.IsNullOrEmpty(item.Site) ? Helpers.SiteTv : item.Site)}/video/{item.VideoId}";
                            item.ApiOk = true;
                        }
                        else
                        {
                            item.ApiOk = false;
                            item.ApiError = info.Error ?? "Unknown error";
                            apiFailed++;
                            _apiFailedItems.Add((item.VideoId, item.ApiError));
                            AppendScanResult(L.T("ImportFromFolderWizard_ApiFailedLog", item.VideoId, info.Error));
                        }
                    }
                    catch (Exception ex)
                    {
                        item.ApiOk = false;
                        item.ApiError = ex.Message;
                        apiFailed++;
                        _apiFailedItems.Add((item.VideoId, L.T("ImportFromFolderWizard_ExceptionPrefix", ex.Message)));
                        AppendScanResult(L.T("ImportFromFolderWizard_ExceptionLog", item.VideoId, ex.Message));
                    }

                    // 重複videoIdの他のScannedにもAPI結果を伝播
                    PropagateToDuplicates(item, scannedByVideoId[item.VideoId]);

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
                    ? L.T("ImportFromFolderWizard_D018", singleVideoCount)
                    : "";

                dgvTitleMatches.Rows.Clear();
                foreach (var m in _titleMatches)
                {
                    var rowIdx = dgvTitleMatches.Rows.Add(
                        m.HighConfidence, m.ConfidenceLabel, m.TierLabel,
                        m.SelectedVideo.Title, "",
                        m.FileName, m.DurationLabel);
                    var row = dgvTitleMatches.Rows[rowIdx];
                    row.Tag = m;
                    row.Cells[colTMFileName.Index].ToolTipText = m.Candidate.FilePath;
                    PopulateArtistCell(row, m);
                }

                AppendScanResult(
                    L.T("ImportFromFolderWizard_ScanCompleteHeader") + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryTagged", _scanned.Count) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryTitleMatch", _titleMatches.Count, _titleMatches.Count(m => m.HighConfidence)) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryDup", _alreadyOwnedFiles.Count) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryUntagged", _untaggedCount) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryUniqueId", uniqueVideoIds.Count) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryApiSkipped", apiSkipped) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryApiFailed", apiFailed) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummaryNewAuthors", authorGroups.Count(a => !a.AlreadySubscribed)) + "\r\n" +
                    L.T("ImportFromFolderWizard_SummarySingleVideos", singleVideoCount));

                _step = 3;
            }
            catch (OperationCanceledException)
            {
                AppendScanResult(L.T("ImportFromFolderWizard_ScanCancelled"));
                _step = 1;
            }
            catch (Exception ex)
            {
                AppendScanResult(L.T("ImportFromFolderWizard_ErrorLog", ex.Message));
                MessageBox.Show(this, L.T("ImportFromFolderWizard_D019", ex.Message),
                    L.T("ImportFromFolderWizard_D020"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        /// <summary>
        /// FilenameMatcher の結果を表示用アイテムに変換する。Duration 照合は Prefix tier や
        /// 曖昧な候補には行わない (どのみち手動確認行きなのでファイルを開く I/O が無駄になる)。
        /// </summary>
        private List<TitleMatchDisplayItem> BuildTitleMatchDisplayItems(
            List<Utils.TitleMatchCandidate> matches, Models.SubscribedUser? subUser, CancellationToken ct)
        {
            var displayItems = new List<TitleMatchDisplayItem>();
            foreach (var m in matches)
            {
                ct.ThrowIfCancellationRequested();
                double? diff = null;
                bool durationOk = false;
                if (m.Tier != Utils.TitleMatchTier.Prefix && !m.Ambiguous && m.Video.DurationSeconds > 0)
                {
                    var fileDuration = TryReadDuration(m.FilePath);
                    if (fileDuration.HasValue)
                    {
                        diff = Math.Abs(fileDuration.Value - m.Video.DurationSeconds);
                        durationOk = diff.Value <= 5.0;
                    }
                }
                displayItems.Add(new TitleMatchDisplayItem
                {
                    Candidate = m,
                    DurationDiffSeconds = diff,
                    DurationOk = durationOk,
                    SubUser = subUser,
                });
            }
            return displayItems;
        }

        /// <summary>
        /// mp4 の再生時間を TagLib で読み取る (フルデコード無しの軽量な読み取り)。
        /// 失敗時は null。
        /// </summary>
        private static double? TryReadDuration(string filePath)
        {
            try
            {
                using var f = TagLib.File.Create(filePath);
                var sec = f.Properties.Duration.TotalSeconds;
                return sec > 0 ? sec : null;
            }
            catch
            {
                return null;
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
            _titleMatchImported = 0;
            _titleMatchApiFailed = 0;
            txtImportLog.Clear();
            progressImport.Value = 0;
            lblImportStatus.Text = L.T("ImportFromFolderWizard_D001");

            lock (_importProgressLock)
            {
                _hasPendingImportProgress = false;
                _pendingImportStatus = "";
                _pendingImportProcessed = 0;
                _pendingImportTotal = 0;
            }
            _importProgressTimer?.Stop();
            _importProgressTimer?.Start();

            BeginImportTrace();
            TraceImport($"START folder=\"{txtFolder.Text}\" scanned={_scanned.Count} " +
                $"titleMatches={_titleMatches.Count} untagged={_untaggedFiles.Count}");

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

                    var importVideos = _scanned.Where(s => s.ApiOk).ToList();
                    // 同じvideoIdが複数ある場合は、従来の逐次処理で「1件目を登録し、
                    // 2件目以降を既存実体としてスキップする」挙動を維持する。
                    // 一意な取り込みだけをバッチ化し、重複の意味を変えない。
                    bool canBatchNewVideos = !importVideos
                        .GroupBy(s => s.VideoId, StringComparer.Ordinal)
                        .Any(g => g.Count() > 1);
                    var pendingNewVideos = new List<VideoInfo>();

                    int total = importVideos.Count;
                    int processed = 0;
                    var dbImportStopwatch = Stopwatch.StartNew();
                    TraceImport($"DB_IMPORT_BEGIN total={total} apiOk={importVideos.Count} " +
                        $"canBatchNew={canBatchNewVideos}");

                    foreach (var sv in importVideos)
                    {
                        ct.ThrowIfCancellationRequested();
                        processed++;
                        ReportImport($"({processed}/{total}) {sv.Title}", processed, total);
                        if (processed == 1 || processed % 1000 == 0 || processed == total)
                        {
                            TraceImport($"DB_IMPORT_PROGRESS itemStart={processed}/{total} " +
                                $"new={_importedNew} merged={_mergedCount} skipped={_skippedExistingCount} failed={_failedCount}");
                        }

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
                                        AppendImportLog(L.T("ImportFromFolderWizard_UserIdRepaired", existing.Username));
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
                                        ProfileUrl = $"https://{(string.IsNullOrEmpty(sv.Site) ? Helpers.SiteTv : sv.Site)}/profile/{sv.AuthorUsername}/videos",
                                        Site = sv.Site,
                                        IsEnabled = true,
                                        CreatedAt = DateTime.Now,
                                        LastCheckedAt = DateTime.Now,
                                    };
                                    var newId = _database.AddSubscribedUser(newUser);
                                    newUser.Id = newId;
                                    existingUsers[authorKey] = newUser;
                                    subUser = newUser;
                                    AppendImportLog(L.T("ImportFromFolderWizard_NewSubscription", sv.AuthorName ?? sv.AuthorUsername, newUser.Username));
                                }
                                else
                                {
                                    // チェックを外した作者も「単発動画」という別分類には逃さず、
                                    // 自動チェックOFFのチャンネルとして取り込む(あとで手動で有効化すれば
                                    // 通常の購読チャンネルになる)。
                                    var placeholderUser = _database.EnsureChannelForAuthor(sv.AuthorUsername!, sv.Site);
                                    existingUsers[authorKey] = placeholderUser;
                                    subUser = placeholderUser;
                                    AppendImportLog(L.T("ImportFromFolderWizard_NewPlaceholderChannel", sv.AuthorName ?? sv.AuthorUsername, placeholderUser.Username));
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
                                    if (string.IsNullOrEmpty(existingVideo.Url))
                                        existingVideo.Url = sv.Url;
                                    if (string.IsNullOrEmpty(existingVideo.ThumbnailUrl))
                                        existingVideo.ThumbnailUrl = sv.ThumbnailUrl;
                                    if (existingVideo.DurationSeconds <= 0)
                                        existingVideo.DurationSeconds = sv.DurationSeconds;
                                    if (!existingVideo.PostedAt.HasValue)
                                        existingVideo.PostedAt = sv.PostedAt;
                                    if (string.IsNullOrEmpty(existingVideo.Rating))
                                        existingVideo.Rating = sv.Rating;
                                    if (string.IsNullOrEmpty(existingVideo.EmbedUrl))
                                        existingVideo.EmbedUrl = sv.EmbedUrl;
                                    if (string.IsNullOrEmpty(existingVideo.ApiRawJson))
                                        existingVideo.ApiRawJson = sv.ApiRawJson;
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
                                    AppendImportLog(L.T("ImportFromFolderWizard_Merged", sv.Title));
                                }
                                else
                                {
                                    // 実ファイルパスが存在するならインポートをスキップ
                                    _skippedExistingCount++;
                                    AppendImportLog(L.T("ImportFromFolderWizard_SkippedExisting", sv.Title));
                                }
                                continue;
                            }

                            // 新規追加 (自動フォールバックで判明した site も継承)
                            var v = new VideoInfo
                            {
                                VideoId = sv.VideoId,
                                Title = sv.Title,
                                Url = sv.Url,
                                ThumbnailUrl = sv.ThumbnailUrl,
                                AuthorUserId = sv.AuthorUsername ?? "",   // UserId = username 運用
                                AuthorUsername = sv.AuthorUsername ?? "",
                                DurationSeconds = sv.DurationSeconds,
                                PostedAt = sv.PostedAt,
                                FileUuid = sv.FileUuid,
                                EmbedUrl = sv.EmbedUrl,
                                Rating = sv.Rating,
                                Site = sv.Site,
                                ApiRawJson = sv.ApiRawJson,
                                LocalFilePath = sv.FilePath,
                                Status = DownloadStatus.Completed,
                                SubscribedUserId = subUser?.Id,
                                DownloadedAt = DateTime.Now,
                                CreatedAt = DateTime.Now,
                            };
                            try { v.FileSize = new FileInfo(sv.FilePath).Length; } catch { }
                            if (canBatchNewVideos)
                            {
                                pendingNewVideos.Add(v);
                            }
                            else
                            {
                                _database.AddVideo(v);
                                _importedNew++;
                                AppendImportLog(L.T("ImportFromFolderWizard_Imported", sv.Title));
                            }
                        }
                        catch (Exception ex)
                        {
                            _failedCount++;
                            _dbFailedItems.Add((sv.Title, sv.VideoId, ex.Message));
                            AppendImportLog(L.T("ImportFromFolderWizard_FailedLog", sv.Title, ex.Message));
                            LoggingService.Instance.Warn($"Import 失敗 ({sv.VideoId}): {ex.Message}");
                        }
                    }

                    if (pendingNewVideos.Count > 0)
                    {
                        var batchStopwatch = Stopwatch.StartNew();
                        TraceImport($"DB_BATCH_BEGIN count={pendingNewVideos.Count}");
                        try
                        {
                            var addedCount = _database.AddVideosBatch(pendingNewVideos);
                            batchStopwatch.Stop();
                            TraceImport($"DB_BATCH_END elapsedMs={batchStopwatch.ElapsedMilliseconds} " +
                                $"requested={pendingNewVideos.Count} added={addedCount}");
                            if (addedCount == pendingNewVideos.Count)
                            {
                                _importedNew += addedCount;
                                foreach (var video in pendingNewVideos)
                                    AppendImportLog(L.T("ImportFromFolderWizard_Imported", video.Title));
                            }
                            else
                            {
                                // 外部経路が同じvideoIdを先に追加した等の競合時だけ、状態を再確認する。
                                // 通常経路では一意性チェックによりここには来ない。
                                var presentIds = _database.GetAllVideos()
                                    .Select(v => v.VideoId)
                                    .ToHashSet(StringComparer.Ordinal);
                                foreach (var video in pendingNewVideos)
                                {
                                    if (presentIds.Contains(video.VideoId))
                                    {
                                        _skippedExistingCount++;
                                        AppendImportLog(L.T("ImportFromFolderWizard_SkippedExisting", video.Title));
                                        continue;
                                    }

                                    try
                                    {
                                        _database.AddVideo(video);
                                        _importedNew++;
                                        AppendImportLog(L.T("ImportFromFolderWizard_Imported", video.Title));
                                    }
                                    catch (Exception ex)
                                    {
                                        _failedCount++;
                                        _dbFailedItems.Add((video.Title, video.VideoId, ex.Message));
                                        AppendImportLog(L.T("ImportFromFolderWizard_FailedLog", video.Title, ex.Message));
                                        LoggingService.Instance.Warn($"Import 失敗 ({video.VideoId}): {ex.Message}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            batchStopwatch.Stop();
                            TraceImport($"DB_BATCH_FAILED elapsedMs={batchStopwatch.ElapsedMilliseconds} " +
                                $"count={pendingNewVideos.Count}; fallback=perItem");
                            // バッチ全体が失敗した場合はトランザクションがロールバックされるため、
                            // 1件ずつの旧経路へ戻して、従来通り他の動画の取り込みを継続する。
                            foreach (var video in pendingNewVideos)
                            {
                                try
                                {
                                    _database.AddVideo(video);
                                    _importedNew++;
                                    AppendImportLog(L.T("ImportFromFolderWizard_Imported", video.Title));
                                }
                                catch (Exception ex)
                                {
                                    _failedCount++;
                                    _dbFailedItems.Add((video.Title, video.VideoId, ex.Message));
                                    AppendImportLog(L.T("ImportFromFolderWizard_FailedLog", video.Title, ex.Message));
                                    LoggingService.Instance.Warn($"Import 失敗 ({video.VideoId}): {ex.Message}");
                                }
                            }
                        }
                    }

                    dbImportStopwatch.Stop();
                    TraceImport($"DB_IMPORT_END elapsedMs={dbImportStopwatch.ElapsedMilliseconds} " +
                        $"new={_importedNew} merged={_mergedCount} skipped={_skippedExistingCount} " +
                        $"failed={_failedCount} pendingNew={pendingNewVideos.Count}");
                }, ct);

                // UIキューにまだ反映されていない最後の進捗を、完了後処理の前に反映する。
                FlushPendingImportProgress();
                TraceImport("DB_IMPORT_WORKER_COMPLETED");
                await ImportCheckedTitleMatchesAsync(ct);

                FlushPendingImportProgress();
                TraceImport("SUMMARY_CALL_BEGIN");
                ShowSummary();
                TraceImport("SUMMARY_CALL_END");
                _step = 5;
            }
            catch (OperationCanceledException)
            {
                TraceImport("CANCELED");
                AppendImportLog(L.T("ImportFromFolderWizard_ImportCancelled"));
                ShowSummary();
                _step = 5;
            }
            catch (Exception ex)
            {
                TraceImport($"FAILED exception={ex.GetType().Name}: {ex.Message}");
                AppendImportLog(L.T("ImportFromFolderWizard_ErrorLog", ex.Message));
                MessageBox.Show(this, L.T("ImportFromFolderWizard_D021", ex.Message),
                    L.T("ImportFromFolderWizard_D020"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _importProgressTimer?.Stop();
                FlushPendingImportProgress();

                // バッファに残ってる最後のログを必ず吐き出す
                var finalizeStopwatch = Stopwatch.StartNew();
                TraceImport("FINALIZE_BEGIN logFlush");
                _lastImportLogFlush = DateTime.MinValue;
                FlushImportLog();
                finalizeStopwatch.Stop();
                TraceImport($"FINALIZE_LOG_FLUSH_END elapsedMs={finalizeStopwatch.ElapsedMilliseconds}");
                _busy = false;
                _cts?.Dispose();
                _cts = null;
                UpdateStepUi();
                TraceImport($"END totalMs={_importTraceStopwatch?.ElapsedMilliseconds ?? 0} " +
                    $"step={_step} new={_importedNew} merged={_mergedCount} " +
                    $"skipped={_skippedExistingCount} failed={_failedCount} titleMatch={_titleMatchImported}");
                EndImportTrace();
            }
        }

        /// <summary>
        /// Step 3 でチェックされたタイトル照合候補を取り込む。
        /// タグ付きファイルの取り込み (Task.Run 内、同期) とは別に、こちらは非同期な
        /// API 呼び出し (FileUuid 未解決分の逆引き) を伴うため独立したループにしてある。
        /// </summary>
        private async Task ImportCheckedTitleMatchesAsync(CancellationToken ct)
        {
            var titleMatchStopwatch = Stopwatch.StartNew();
            var checkedItems = new List<TitleMatchDisplayItem>();
            foreach (DataGridViewRow row in dgvTitleMatches.Rows)
            {
                if (row.Tag is TitleMatchDisplayItem item
                    && row.Cells[colTMChecked.Index].Value is bool isChecked && isChecked)
                    checkedItems.Add(item);
            }
            TraceImport($"TITLE_MATCH_BEGIN checked={checkedItems.Count} totalCandidates={_titleMatches.Count}");
            if (checkedItems.Count == 0)
            {
                titleMatchStopwatch.Stop();
                TraceImport($"TITLE_MATCH_END elapsedMs={titleMatchStopwatch.ElapsedMilliseconds} imported=0");
                return;
            }

            var apiDelayMs = SettingsManager.Instance.Settings.ApiRequestDelayMs;
            int processed = 0;

            foreach (var item in checkedItems)
            {
                ct.ThrowIfCancellationRequested();
                processed++;
                var candidate = item.Candidate;
                var chosen = item.SelectedVideo;
                ReportImport(L.T("ImportFromFolderWizard_TitleMatchImporting", processed, checkedItems.Count, chosen.Title),
                    processed, checkedItems.Count);

                try
                {
                    // Step 2 時点のスナップショットではなく最新の DB 行を使う
                    // (ウィザードを開いている間に別経路で状態が変わっている可能性への保険)。
                    // ただし chosen.Id==0 (アーティストフォルダ選択検索で iwara API から
                    // 直接取得しただけの、まだ DB に存在しない動画) は GetVideoByVideoId で見つからない
                    // のが正常なので、その場合は候補をそのまま使う (ImportOneAsync 側が新規追加する)。
                    var video = _database.GetVideoByVideoId(chosen.VideoId)
                        ?? (chosen.Id == 0 ? chosen : null);
                    if (video == null)
                    {
                        AppendImportLog(L.T("ImportFromFolderWizard_TitleMatchLostFromDb", chosen.Title));
                        _failedCount++;
                        continue;
                    }
                    if (video.LocalFileExists)
                    {
                        AppendImportLog(L.T("ImportFromFolderWizard_SkippedAlreadyDownloaded", chosen.Title));
                        _skippedExistingCount++;
                        continue;
                    }

                    bool neededApiResolve = string.IsNullOrEmpty(video.FileUuid);
                    // LocalFileMapHelper.MapAsync と同じ理由: 取込先の動画がダウンロードキューに
                    // 投入済み(Pending/Active)だと、一覧の状態列が古いタスクの Status を優先表示して
                    // 「進捗100%なのに待機中」のまま化ける。ImportOneAsync の Completed 書き込みより
                    // 先にキャンセルしておく。
                    _downloadManager.CancelTask(video.VideoId);
                    var outcome = await Utils.TitleMatchImporter.ImportOneAsync(
                        video, candidate.FilePath, _downloadManager.IwaraApi, _database, item.SubUser);

                    if (outcome.TagWritten)
                    {
                        AppendImportLog(L.T("ImportFromFolderWizard_TitleMatchImportedTagged", chosen.Title));
                    }
                    else
                    {
                        _titleMatchApiFailed++;
                        AppendImportLog(
                            L.T("ImportFromFolderWizard_TitleMatchImportedUntagged", outcome.ApiError, chosen.Title));
                    }
                    _titleMatchImported++;

                    // API を実際に叩いた場合のみレート制限ディレイを入れる
                    if (neededApiResolve && apiDelayMs > 0) await Task.Delay(apiDelayMs, ct);
                }
                catch (Exception ex)
                {
                    _failedCount++;
                    _dbFailedItems.Add((chosen.Title, chosen.VideoId, ex.Message));
                    AppendImportLog(L.T("ImportFromFolderWizard_TitleMatchFailedLog", chosen.Title, ex.Message));
                    LoggingService.Instance.Warn($"TitleMatch Import 失敗 ({chosen.VideoId}): {ex.Message}");
                }
            }

            titleMatchStopwatch.Stop();
            TraceImport($"TITLE_MATCH_END elapsedMs={titleMatchStopwatch.ElapsedMilliseconds} " +
                $"checked={checkedItems.Count} imported={_titleMatchImported} apiFailed={_titleMatchApiFailed} " +
                $"failed={_failedCount}");
        }

        private void ShowSummary()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)ShowSummary); } catch { } return; }

            var summaryStopwatch = Stopwatch.StartNew();
            TraceImport("SUMMARY_BEGIN");

            var apiFailed = _scanned.Count(s => !s.ApiOk);

            // エラー詳細を永続ログファイルに書き出す
            // (UI 上の txtImportLog はウィザード閉じると消えるので、
            //  後から「何が失敗したか」追跡できるように)
            var errorLogStopwatch = Stopwatch.StartNew();
            _lastErrorLogPath = WriteImportErrorLog();
            errorLogStopwatch.Stop();
            TraceImport($"SUMMARY_ERROR_LOG_END elapsedMs={errorLogStopwatch.ElapsedMilliseconds} " +
                $"path={(string.IsNullOrEmpty(_lastErrorLogPath) ? "none" : "written")}");

            lblSummary.Text =
                L.T("ImportFromFolderWizard_D022", _importedNew) +
                L.T("ImportFromFolderWizard_D023", _mergedCount) +
                L.T("ImportFromFolderWizard_D031", _titleMatchImported) +
                L.T("ImportFromFolderWizard_D024", _skippedExistingCount) +
                L.T("ImportFromFolderWizard_D025", _untaggedCount) +
                L.T("ImportFromFolderWizard_D026", apiFailed) +
                L.T("ImportFromFolderWizard_D027", _failedCount) +
                (string.IsNullOrEmpty(_lastErrorLogPath)
                    ? ""
                    : L.T("ImportFromFolderWizard_D028", _lastErrorLogPath));

            lblDupNotice.Text = "";

            // バックグラウンド実行で最小化されていた場合は復元してサマリを見せる + バルーン通知
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();

            try
            {
                var msg = L.T("ImportFromFolderWizard_ToastSummary",
                    _importedNew, _mergedCount, _titleMatchImported, _skippedExistingCount);
                Services.NotificationService.Instance.ShowNotification(L.T("ImportFromFolderWizard_D030"), msg);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"通知失敗: {ex.Message}");
            }

            // WPF側MainViewModelにチャンネル一覧 + 動画リスト更新を通知
            // (Phase8c: WinFormsのApplication.OpenForms経由MainForm探索から、
            //  MainViewModel.Currentホルダー経由に置き換え)
            try
            {
                var refreshStopwatch = Stopwatch.StartNew();
                TraceImport("SUMMARY_MAIN_REFRESH_BEGIN");
                Wpf.ViewModels.MainViewModel.Current?.RefreshAfterImport();
                refreshStopwatch.Stop();
                TraceImport($"SUMMARY_MAIN_REFRESH_END elapsedMs={refreshStopwatch.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                TraceImport($"SUMMARY_MAIN_REFRESH_FAILED exception={ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"MainForm refresh 通知失敗: {ex.Message}");
            }

            summaryStopwatch.Stop();
            TraceImport($"SUMMARY_END elapsedMs={summaryStopwatch.ElapsedMilliseconds}");
        }

        /// <summary>
        /// 同じ videoId を持つ他の走査結果へ、解決済みのメタデータをコピーする。
        /// API は videoId ごとに1回しか叩かないため、代表以外はこの伝播でしか値を得られない。
        /// FileUuid だけはファイルのタグから読めている場合があるので、既存値を優先する。
        /// </summary>
        private static void PropagateToDuplicates(ScannedVideo item, IEnumerable<ScannedVideo> group)
        {
            foreach (var s in group)
            {
                if (ReferenceEquals(s, item)) continue;
                s.Title = item.Title;
                s.Url = item.Url;
                s.ThumbnailUrl = item.ThumbnailUrl;
                s.DurationSeconds = item.DurationSeconds;
                s.PostedAt = item.PostedAt;
                s.Rating = item.Rating;
                s.EmbedUrl = item.EmbedUrl;
                s.ApiRawJson = item.ApiRawJson;
                s.AuthorUsername = item.AuthorUsername;
                s.AuthorName = item.AuthorName;
                s.FileUuid = string.IsNullOrEmpty(s.FileUuid) ? item.FileUuid : s.FileUuid;
                s.Site = item.Site;
                s.ApiOk = item.ApiOk;
                s.ApiError = item.ApiError;
            }
        }

        private void BeginImportTrace()
        {
            _importTraceId = Guid.NewGuid().ToString("N")[..8];
            _importTraceStopwatch = Stopwatch.StartNew();
            LoggingService.Instance.Info($"FolderImport[{_importTraceId}] TRACE_CREATED");
        }

        private void TraceImport(string message)
        {
            var traceId = _importTraceId;
            var stopwatch = _importTraceStopwatch;
            if (traceId == null || stopwatch == null) return;

            LoggingService.Instance.Info(
                $"FolderImport[{traceId}] +{stopwatch.ElapsedMilliseconds}ms {message}");
        }

        private void EndImportTrace()
        {
            _importTraceStopwatch?.Stop();
            _importTraceId = null;
            _importTraceStopwatch = null;
        }

        /// <summary>
        /// インポートで発生したエラーを永続ログファイルに書き出す。
        /// エラーが1件も無ければファイルは作成せず null を返す。
        /// </summary>
        private string? WriteImportErrorLog()
        {
            int totalErrors = _untaggedFiles.Count + _apiFailedItems.Count + _dbFailedItems.Count + _alreadyOwnedFiles.Count;
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
                if (_alreadyOwnedFiles.Count > 0)
                {
                    sb.AppendLine("--- 重複 (ファイル名/フォルダ名のvideoIdが既にDL済みの動画と一致) ---");
                    foreach (var f in _alreadyOwnedFiles) sb.AppendLine(f);
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

            // ワーカースレッドからはUIを直接触らず、最新値だけを保持する。
            // 反映は200ms間隔のWinForms Timer (UIスレッド) と完了時の明示flushで行う。
            lock (_importProgressLock)
            {
                _pendingImportStatus = status;
                _pendingImportProcessed = processed;
                _pendingImportTotal = total;
                _hasPendingImportProgress = true;
            }
        }

        private void FlushPendingImportProgress()
        {
            if (IsDisposed)
            {
                lock (_importProgressLock) _hasPendingImportProgress = false;
                return;
            }

            string status;
            int processed;
            int total;
            lock (_importProgressLock)
            {
                if (!_hasPendingImportProgress) return;
                status = _pendingImportStatus;
                processed = _pendingImportProcessed;
                total = _pendingImportTotal;
                _hasPendingImportProgress = false;
            }

            lblImportStatus.Text = status;
            progressImport.Maximum = Math.Max(1, total);
            progressImport.Value = Math.Min(progressImport.Maximum, processed);
        }

        // ログを大量出力するとBeginInvokeのコストでUIが詰まるのでバッファリング。
        // 200ms ごと or バッファに50行たまったら一括フラッシュ。
        private readonly StringBuilder _importLogBuffer = new();
        private int _importLogBufferedLines;
        private bool _importLogFlushScheduled;
        private DateTime _lastImportLogFlush = DateTime.MinValue;
        private const int ImportLogFlushIntervalMs = 200;
        private const int ImportLogFlushBatchLines = 50;

        private void AppendImportLog(string msg)
        {
            if (IsDisposed) return;
            bool requestFlush;
            lock (_importLogBuffer)
            {
                _importLogBuffer.AppendLine(msg);
                _importLogBufferedLines++;

                var elapsed = (DateTime.Now - _lastImportLogFlush).TotalMilliseconds;
                requestFlush = (elapsed >= ImportLogFlushIntervalMs
                    || _importLogBufferedLines >= ImportLogFlushBatchLines)
                    && !_importLogFlushScheduled;
                if (requestFlush)
                {
                    _importLogFlushScheduled = true;
                    _lastImportLogFlush = DateTime.Now;
                }
            }

            if (!requestFlush) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action)FlushImportLog); }
                catch
                {
                    lock (_importLogBuffer) _importLogFlushScheduled = false;
                }
                return;
            }
            FlushImportLog();
        }

        private void FlushImportLog()
        {
            if (IsDisposed) return;
            string text;
            lock (_importLogBuffer)
            {
                if (_importLogBuffer.Length == 0)
                {
                    _importLogFlushScheduled = false;
                    return;
                }
                text = _importLogBuffer.ToString();
                _importLogBuffer.Clear();
                _importLogBufferedLines = 0;
                _importLogFlushScheduled = false;
            }
            txtImportLog.AppendText(text);
        }

        #endregion

        private void ImportFromFolderWizard_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy)
            {
                var r = MessageBox.Show(this, L.T("ImportFromFolderWizard_D029"),
                    L.T("ImportFromFolderWizard_D016"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
            public string Url = "";
            public string ThumbnailUrl = "";
            public int DurationSeconds;
            public DateTime? PostedAt;
            public string Rating = "";
            public string EmbedUrl = "";
            public string ApiRawJson = "";
            public string? AuthorUsername;
            public string? AuthorName;
            public string Site = "";  // 自動 site フォールバックで判明した場合に格納
            public bool ApiOk;
            public string? ApiError;
        }

        /// <summary>
        /// clbTitleMatches に表示する1行分のラッパー。
        /// ファイル名照合は UUID/サイズ照合より確度が低いため、既定でチェックが入るのは
        /// 「曖昧でない かつ Prefix tier でない かつ Duration が閾値内で一致」の高確度な場合のみ。
        /// </summary>
        private class TitleMatchDisplayItem
        {
            public Utils.TitleMatchCandidate Candidate = null!;
            public double? DurationDiffSeconds;
            public bool DurationOk;
            /// <summary>
            /// アーティストフォルダ選択検索で、対象アーティストが既に購読済みだった場合の SubscribedUser。
            /// 取り込み時に新規動画の SubscribedUserId 補完へ渡す。null (未購読) でも
            /// TitleMatchImporter 側が作者名から自動チェックOFFのチャンネルを起こして紐付ける。
            /// </summary>
            public Models.SubscribedUser? SubUser;

            private Models.VideoInfo? _selectedVideo;

            /// <summary>
            /// 実際に取り込む動画。既定は Candidate.Video だが、Candidate.AlternativeCandidates が
            /// ある行 (1ファイルに複数候補で自動確定できなかった) は、グリッドのドロップダウンで
            /// ユーザーが選び直せる。取り込み処理はこちらを見る (Candidate.Video ではなく)。
            /// </summary>
            public Models.VideoInfo SelectedVideo
            {
                get => _selectedVideo ?? Candidate.Video;
                set => _selectedVideo = value;
            }

            /// <summary>ドロップダウンで既定候補から選び直された行は true (確度表示を専用ラベルに切り替える)</summary>
            public bool ManuallySelected { get; set; }

            public bool HighConfidence
            {
                get
                {
                    if (Candidate.Ambiguous) return false;
                    if (Candidate.Tier == Utils.TitleMatchTier.Prefix) return false;
                    if (Candidate.Tier == Utils.TitleMatchTier.Id)
                    {
                        // videoId 直接一致はほぼ確実な一致なので、Duration が不明でも高確度扱いにする。
                        // ただし Duration が判明していて閾値を超えて食い違うなら (別マスターへの差し替え等)
                        // 要確認に落とす。
                        return !DurationDiffSeconds.HasValue || DurationDiffSeconds.Value <= 5.0;
                    }
                    return DurationOk; // Exact/Substring は Duration 一致が必須
                }
            }

            public string TierLabel => Candidate.Tier switch
            {
                Utils.TitleMatchTier.Id => L.T("ImportFromFolderWizard_TMTierId"),
                Utils.TitleMatchTier.Exact => L.T("ImportFromFolderWizard_TMTierExact"),
                Utils.TitleMatchTier.Substring => L.T("ImportFromFolderWizard_TMTierSubstring"),
                _ => L.T("ImportFromFolderWizard_TMTierPrefix"),
            };

            /// <summary>グリッドの「確度/安全性」列に出す文字列 (=このままマージしてよさそうかの判定)</summary>
            public string ConfidenceLabel
            {
                get
                {
                    if (ManuallySelected)
                    {
                        return DurationOk
                            ? L.T("ImportFromFolderWizard_TMManuallySelected")
                            : L.T("ImportFromFolderWizard_TMManuallySelectedReview");
                    }
                    if (HighConfidence) return L.T("ImportFromFolderWizard_TMHighConfidence");
                    if (Candidate.Ambiguous)
                    {
                        var reasonText = Candidate.AmbiguousReason == Utils.AmbiguousReason.MultipleFilesForCandidate
                            ? L.T("ImportFromFolderWizard_TMAmbiguousMultiFile")
                            : L.T("ImportFromFolderWizard_TMAmbiguousMultiCandidate");
                        return L.T("ImportFromFolderWizard_TMNeedsReviewWithReason", reasonText);
                    }
                    return L.T("ImportFromFolderWizard_TMNeedsReview");
                }
            }

            public string DurationLabel => DurationDiffSeconds.HasValue
                ? L.T("ImportFromFolderWizard_TMDurationDiff", DurationDiffSeconds.Value.ToString("F0"))
                : L.T("ImportFromFolderWizard_TMDurationUnknown");

            public string FileName => Path.GetFileName(Candidate.FilePath);

            public override string ToString() =>
                $"[{ConfidenceLabel}/{TierLabel}/{DurationLabel}] {FileName} → {SelectedVideo.Title}  ({Candidate.FilePath})";
        }

        /// <summary>
        /// colTMArtist の DataGridViewComboBoxCell に入れる選択肢1件のラッパー。
        /// ToString() がそのままドロップダウンの表示文字列になる。
        /// </summary>
        private class ArtistCandidateOption
        {
            public Models.VideoInfo Video { get; }
            public ArtistCandidateOption(Models.VideoInfo video) => Video = video;
            public override string ToString() => $"{Video.AuthorUsername} / {Video.Title}";
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
                    ? L.T("ImportFromFolderWizard_NodeRegistered", disp, VideoCount)
                    : L.T("ImportFromFolderWizard_NodeNormal", disp, VideoCount);
            }
        }
    }
}
