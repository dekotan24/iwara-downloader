using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// タグの無いファイルを、ファイル/フォルダ単位で別々の方法により照合する設定画面。
    /// 一覧で対象ファイルを確認してから、アーティスト検索・ファイル名検索・スキップを
    /// 任意の範囲へ順番に割り当てる。実際の照合ロジックは FilenameMatcher に委譲する。
    /// </summary>
    public partial class UntaggedFileMatchForm : Form
    {
        public enum MatchMethod
        {
            Artist,
            Filename,
            Skip,
        }

        /// <summary>1つの設定で照合した結果。ImportFromFolderWizard が結果をまとめて表示する。</summary>
        public sealed class UntaggedMatchPlan
        {
            public MatchMethod Method { get; init; }
            public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
            public FilenameMatchResult Result { get; init; } = new();
            public SubscribedUser? ResolvedSubUser { get; init; }
        }

        private sealed class FileAssignment
        {
            public MatchMethod Method { get; init; }
            public string ScanFolder { get; init; } = "";
            public string Template { get; init; } = "";
            public string ArtistUsername { get; init; } = "";
            public IReadOnlyList<VideoInfo>? ArtistVideos { get; init; }
            public SubscribedUser? ResolvedSubUser { get; init; }
        }

        private readonly List<string> _untaggedFiles;
        private readonly DatabaseService _database;
        private readonly DownloadManager _downloadManager;
        private readonly string _defaultFolder;
        private readonly Dictionary<string, FileAssignment> _assignments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataGridViewRow> _rowByPath = new(StringComparer.OrdinalIgnoreCase);

        private string? _resolvedArtistUsername;
        private List<VideoInfo>? _resolvedArtistVideos;
        private SubscribedUser? _resolvedSubUser;
        private bool _busy;
        private bool _suppressGridStatus;

        /// <summary>検索方法を適用して確定した照合計画。</summary>
        public IReadOnlyList<UntaggedMatchPlan> Plans { get; private set; } = Array.Empty<UntaggedMatchPlan>();

        public UntaggedFileMatchForm(
            List<string> untaggedFiles, DatabaseService database, DownloadManager downloadManager, string defaultFolder)
        {
            InitializeComponent();
            _untaggedFiles = untaggedFiles
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _database = database;
            _downloadManager = downloadManager;
            _defaultFolder = defaultFolder;

            Utils.Localizer.Apply(this);
            lblIntro.Text = L.T("UntaggedFileMatchForm_D001", _untaggedFiles.Count);

            txtScopeFolder.Text = defaultFolder;
            txtArtistTemplate.Text = "{id}_{title}.mp4";
            txtFilenameTemplate.Text = "";
            cmbMethod.SelectedIndex = 0;
            PopulateFileGrid();
            UpdateMethodUi();
            UpdateSelectionStatus();
        }

        private void PopulateFileGrid()
        {
            _suppressGridStatus = true;
            _rowByPath.Clear();
            dgvFiles.SuspendLayout();
            try
            {
                dgvFiles.Rows.Clear();
                foreach (var filePath in _untaggedFiles)
                {
                    string relativePath;
                    try { relativePath = Path.GetRelativePath(_defaultFolder, filePath); }
                    catch { relativePath = filePath; }

                    var rowIndex = dgvFiles.Rows.Add(
                        false,
                        Path.GetFileName(filePath),
                        relativePath,
                        filePath,
                        L.T("UntaggedFileMatchForm_D026"),
                        "");
                    var row = dgvFiles.Rows[rowIndex];
                    row.Tag = filePath;
                    _rowByPath[filePath] = row;
                    row.Cells[colRelativePath.Index].ToolTipText = filePath;
                    row.Cells[colFullPath.Index].ToolTipText = filePath;
                }
            }
            finally
            {
                dgvFiles.ResumeLayout();
                _suppressGridStatus = false;
            }
        }

        private void cmbMethod_SelectedIndexChanged(object? sender, EventArgs e) => UpdateMethodUi();

        private void UpdateMethodUi()
        {
            var method = GetSelectedMethod();
            bool artist = method == MatchMethod.Artist;
            bool filename = method == MatchMethod.Filename;

            grpArtist.Visible = artist;
            grpFilename.Visible = filename;
            lblRuleHelp.Text = method switch
            {
                MatchMethod.Artist => L.T("UntaggedFileMatchForm_D027"),
                MatchMethod.Filename => L.T("UntaggedFileMatchForm_D028"),
                _ => L.T("UntaggedFileMatchForm_D029"),
            };

            if (!_busy)
            {
                grpArtist.Enabled = artist;
                grpFilename.Enabled = filename;
            }
        }

        private MatchMethod GetSelectedMethod()
        {
            return cmbMethod.SelectedIndex switch
            {
                1 => MatchMethod.Filename,
                2 => MatchMethod.Skip,
                _ => MatchMethod.Artist,
            };
        }

        private void btnSelectAllFiles_Click(object? sender, EventArgs e)
        {
            SetAllFileChecks(true);
        }

        private void btnClearFileSelection_Click(object? sender, EventArgs e)
        {
            SetAllFileChecks(false);
        }

        private void SetAllFileChecks(bool value)
        {
            _suppressGridStatus = true;
            try
            {
                foreach (DataGridViewRow row in dgvFiles.Rows)
                    row.Cells[colSelected.Index].Value = value;
            }
            finally
            {
                _suppressGridStatus = false;
            }
            UpdateSelectionStatus();
        }

        private void btnBrowseScopeFolder_Click(object? sender, EventArgs e)
        {
            using var d = new FolderBrowserDialog
            {
                Description = L.T("UntaggedFileMatchForm_D003"),
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(txtScopeFolder.Text) ? txtScopeFolder.Text : _defaultFolder,
            };
            if (d.ShowDialog(this) == DialogResult.OK)
                txtScopeFolder.Text = d.SelectedPath;
        }

        private void btnSelectScope_Click(object? sender, EventArgs e)
        {
            var folder = txtScopeFolder.Text.Trim();
            if (!Directory.Exists(folder) || !IsFolderUnderScanRoot(folder))
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D022"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selected = 0;
            _suppressGridStatus = true;
            try
            {
                foreach (DataGridViewRow row in dgvFiles.Rows)
                {
                    if (row.Tag is not string filePath) continue;
                    bool under = FileMoveHelper.IsPathUnder(filePath, folder);
                    row.Cells[colSelected.Index].Value = under;
                    if (under) selected++;
                }
            }
            finally
            {
                _suppressGridStatus = false;
            }

            UpdateSelectionStatus();
            lblStatus.Text = L.T("UntaggedFileMatchForm_D023", selected);
        }

        private void dgvFiles_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvFiles.CurrentCell is DataGridViewCheckBoxCell && dgvFiles.IsCurrentCellDirty)
                dgvFiles.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgvFiles_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (!_suppressGridStatus && e.RowIndex >= 0 && e.ColumnIndex == colSelected.Index)
                UpdateSelectionStatus();
        }

        private List<string> GetCheckedFiles()
        {
            dgvFiles.EndEdit();
            return dgvFiles.Rows
                .Cast<DataGridViewRow>()
                .Where(row => row.Cells[colSelected.Index].Value is bool checkedValue && checkedValue)
                .Select(row => row.Tag as string)
                .Where(path => !string.IsNullOrEmpty(path))
                .Cast<string>()
                .ToList();
        }

        private void btnApplyRule_Click(object? sender, EventArgs e)
        {
            if (_busy) return;

            var files = GetCheckedFiles();
            if (files.Count == 0)
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D025"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var method = GetSelectedMethod();
            FileAssignment assignment;
            if (method == MatchMethod.Artist)
            {
                if (_resolvedArtistVideos == null || string.IsNullOrEmpty(_resolvedArtistUsername))
                {
                    MessageBox.Show(this, L.T("UntaggedFileMatchForm_D013"), L.T("UntaggedFileMatchForm_D005"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var scopeFolder = txtScopeFolder.Text.Trim();
                if (!Directory.Exists(scopeFolder) || !IsFolderUnderScanRoot(scopeFolder)
                    || files.Any(file => !FileMoveHelper.IsPathUnder(file, scopeFolder)))
                {
                    MessageBox.Show(this, L.T("UntaggedFileMatchForm_D022"), L.T("UntaggedFileMatchForm_D005"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                assignment = new FileAssignment
                {
                    Method = MatchMethod.Artist,
                    ScanFolder = scopeFolder,
                    Template = txtArtistTemplate.Text.Trim(),
                    ArtistUsername = _resolvedArtistUsername,
                    ArtistVideos = _resolvedArtistVideos,
                    ResolvedSubUser = _resolvedSubUser,
                };
            }
            else if (method == MatchMethod.Filename)
            {
                assignment = new FileAssignment
                {
                    Method = MatchMethod.Filename,
                    ScanFolder = _defaultFolder,
                    Template = txtFilenameTemplate.Text.Trim(),
                };
            }
            else
            {
                assignment = new FileAssignment { Method = MatchMethod.Skip };
            }

            _suppressGridStatus = true;
            try
            {
                foreach (var file in files)
                {
                    _assignments[file] = assignment;
                    if (_rowByPath.TryGetValue(file, out var row))
                    {
                        row.Cells[colSelected.Index].Value = false;
                        UpdateAssignmentCells(row, assignment);
                    }
                }
            }
            finally
            {
                _suppressGridStatus = false;
            }

            UpdateSelectionStatus();
            lblStatus.Text = L.T("UntaggedFileMatchForm_D024", files.Count, GetMethodLabel(method));
        }

        private void UpdateAssignmentCells(DataGridViewRow row, FileAssignment? assignment)
        {
            var method = assignment?.Method ?? MatchMethod.Skip;
            row.Cells[colAssignedMethod.Index].Value = assignment == null
                ? L.T("UntaggedFileMatchForm_D026")
                : GetMethodLabel(method);
            row.Cells[colAssignedDetail.Index].Value = assignment == null
                ? ""
                : method switch
                {
                    MatchMethod.Artist => $"@{assignment.ArtistUsername}",
                    MatchMethod.Filename => string.IsNullOrEmpty(assignment.Template)
                        ? L.T("UntaggedFileMatchForm_D031")
                        : assignment.Template,
                    _ => "",
                };
        }

        private string GetMethodLabel(MatchMethod method)
        {
            return method switch
            {
                MatchMethod.Artist => L.T("UntaggedFileMatchForm_D027_Short"),
                MatchMethod.Filename => L.T("UntaggedFileMatchForm_D028_Short"),
                _ => L.T("UntaggedFileMatchForm_D029_Short"),
            };
        }

        private void UpdateSelectionStatus()
        {
            int selected = dgvFiles.Rows.Cast<DataGridViewRow>()
                .Count(row => row.Cells[colSelected.Index].Value is bool value && value);
            int assigned = _assignments.Count;
            int unassigned = Math.Max(0, _untaggedFiles.Count - assigned);
            lblSelectionStatus.Text = L.T("UntaggedFileMatchForm_D030", selected, assigned, unassigned);
        }

        private void txtArtistInput_TextChanged(object? sender, EventArgs e)
        {
            _resolvedArtistUsername = null;
            _resolvedArtistVideos = null;
            _resolvedSubUser = null;
            lblArtistResolved.Text = "";
            btnRefetchArtist.Visible = false;
        }

        private async void btnResolveArtist_Click(object? sender, EventArgs e)
            => await ResolveArtistAsync(forceRefetch: false);

        private async void btnRefetchArtist_Click(object? sender, EventArgs e)
            => await ResolveArtistAsync(forceRefetch: true);

        private async Task ResolveArtistAsync(bool forceRefetch)
        {
            if (_busy) return;

            var raw = txtArtistInput.Text.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D004"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var username = (Helpers.ExtractUsernameFromUrl(raw) ?? raw).Trim().TrimStart('@');
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D004"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true, L.T("UntaggedFileMatchForm_D006", username));
            try
            {
                var subUser = _database.GetAllSubscribedUsers()
                    .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

                List<VideoInfo> videos;
                if (_downloadManager.IsLoggedIn)
                {
                    // DBにある動画だけでは未登録動画を拾えないため、ログイン済みなら常に最新一覧を取得する。
                    var site = Helpers.IsUserProfileUrl(raw)
                        ? Helpers.ExtractSiteFromUrl(raw)
                        : !string.IsNullOrEmpty(subUser?.Site)
                            ? subUser.Site
                            : Helpers.SiteTv;
                    var (fetched, status) = await _downloadManager.IwaraApi.GetUserVideosAsync(username, site: site);
                    if (status == ChannelFetchStatus.Failed && subUser != null)
                    {
                        // 一時的なAPI失敗時は既存DBを候補にして、ユーザーが作業を継続できるようにする。
                        videos = _database.GetAllVideos().Where(v => v.SubscribedUserId == subUser.Id).ToList();
                        lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D007", username, videos.Count);
                    }
                    else if (fetched.Count == 0)
                    {
                        lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D010", username);
                        _resolvedArtistUsername = null;
                        _resolvedArtistVideos = null;
                        return;
                    }
                    else
                    {
                        // 既存行は最新のDB実体を使い、新規動画はAPIのId=0行をそのまま候補にする。
                        // 1件ずつ接続を開くと動画数に比例して遅くなるため、まとめて引く。
                        var existingByVideoId = _database.GetVideosByVideoIds(fetched.Select(v => v.VideoId));
                        videos = fetched
                            .Select(v => existingByVideoId.TryGetValue(v.VideoId ?? "", out var existing) ? existing : v)
                            .ToList();
                        lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D011", username, videos.Count);
                    }
                    btnRefetchArtist.Visible = subUser != null;
                }
                else if (subUser != null)
                {
                    // オフライン時は既存購読分だけを候補にできる。新規動画の取得にはログインが必要。
                    videos = _database.GetAllVideos().Where(v => v.SubscribedUserId == subUser.Id).ToList();
                    lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D007", username, videos.Count);
                    btnRefetchArtist.Visible = true;
                }
                else
                {
                    MessageBox.Show(this, L.T("UntaggedFileMatchForm_D008"), L.T("UntaggedFileMatchForm_D009"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _resolvedArtistUsername = username;
                _resolvedArtistVideos = videos;
                _resolvedSubUser = subUser;
            }
            catch (Exception ex)
            {
                lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D012", ex.Message);
                _resolvedArtistUsername = null;
                _resolvedArtistVideos = null;
                _resolvedSubUser = null;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnRun_Click(object? sender, EventArgs e)
        {
            if (_busy) return;

            dgvFiles.EndEdit();
            var unassigned = _untaggedFiles.Where(f => !_assignments.ContainsKey(f)).ToList();
            if (unassigned.Count > 0)
            {
                var confirm = MessageBox.Show(
                    this,
                    L.T("UntaggedFileMatchForm_D021", unassigned.Count),
                    L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                foreach (var file in unassigned)
                    _assignments[file] = new FileAssignment { Method = MatchMethod.Skip };
                foreach (DataGridViewRow row in dgvFiles.Rows)
                {
                    if (row.Tag is string file && _assignments[file].Method == MatchMethod.Skip)
                        UpdateAssignmentCells(row, _assignments[file]);
                }
                UpdateSelectionStatus();
            }

            var groups = BuildAssignmentGroups();
            if (groups.Count == 0)
            {
                Plans = Array.Empty<UntaggedMatchPlan>();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            SetBusy(true, L.T("UntaggedFileMatchForm_D032", groups.Sum(g => g.Files.Count)));
            try
            {
                Plans = await Task.Run(() => BuildPlans(groups));
                SetBusy(false);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D018", ex.Message), L.T("UntaggedFileMatchForm_D019"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private List<(FileAssignment Assignment, List<string> Files)> BuildAssignmentGroups()
        {
            var groups = new Dictionary<FileAssignment, List<string>>();
            foreach (var file in _untaggedFiles)
            {
                if (!_assignments.TryGetValue(file, out var assignment)
                    || assignment.Method == MatchMethod.Skip)
                    continue;

                if (!groups.TryGetValue(assignment, out var files))
                {
                    files = new List<string>();
                    groups[assignment] = files;
                }
                files.Add(file);
            }

            return groups.Select(pair => (pair.Key, pair.Value)).ToList();
        }

        private List<UntaggedMatchPlan> BuildPlans(
            List<(FileAssignment Assignment, List<string> Files)> groups)
        {
            var plans = new List<UntaggedMatchPlan>();
            List<VideoInfo>? allVideos = null;

            foreach (var (assignment, files) in groups)
            {
                if (assignment.Method == MatchMethod.Artist)
                {
                    var videos = assignment.ArtistVideos ?? Array.Empty<VideoInfo>();
                    var knownIds = videos
                        .Where(v => !string.IsNullOrEmpty(v.VideoId))
                        .Select(v => v.VideoId)
                        .ToHashSet(StringComparer.Ordinal);
                    var hints = BuildTemplateHints(
                        files,
                        assignment.Template,
                        assignment.ScanFolder,
                        new[] { assignment.ArtistUsername },
                        knownIds);
                    var result = FilenameMatcher.Match(files, videos, assignment.ScanFolder, hints);
                    plans.Add(new UntaggedMatchPlan
                    {
                        Method = assignment.Method,
                        Files = files,
                        Result = result,
                        ResolvedSubUser = assignment.ResolvedSubUser,
                    });
                }
                else if (assignment.Method == MatchMethod.Filename)
                {
                    allVideos ??= _database.GetAllVideos();
                    var knownIds = allVideos
                        .Where(v => !string.IsNullOrEmpty(v.VideoId))
                        .Select(v => v.VideoId)
                        .ToHashSet(StringComparer.Ordinal);
                    var knownArtists = allVideos
                        .Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                        .Select(v => v.AuthorUsername)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var hints = BuildTemplateHints(
                        files,
                        assignment.Template,
                        _defaultFolder,
                        knownArtists,
                        knownIds);
                    var result = FilenameMatcher.Match(files, allVideos, _defaultFolder, hints);
                    plans.Add(new UntaggedMatchPlan
                    {
                        Method = assignment.Method,
                        Files = files,
                        Result = result,
                    });
                }
            }

            return plans;
        }

        private static Dictionary<string, PathTemplate.ExtractResult>? BuildTemplateHints(
            IEnumerable<string> files,
            string template,
            string scanFolder,
            IEnumerable<string> knownArtists,
            IReadOnlySet<string> knownIds)
        {
            if (string.IsNullOrEmpty(template)) return null;

            var hints = new Dictionary<string, PathTemplate.ExtractResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var ext = PathTemplate.Extract(template, file, scanFolder, knownArtists, knownIds);
                if (ext != null) hints[file] = ext;
            }
            return hints;
        }

        private bool IsFolderUnderScanRoot(string folder)
        {
            try
            {
                var root = NormalizeDirectory(_defaultFolder);
                var candidate = NormalizeDirectory(folder);
                return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDirectory(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private void SetBusy(bool busy, string? status = null)
        {
            _busy = busy;
            UseWaitCursor = busy;
            progressBar.Visible = busy;
            lblStatus.Text = status ?? "";
            dgvFiles.Enabled = !busy;
            btnSelectAllFiles.Enabled = !busy;
            btnClearFileSelection.Enabled = !busy;
            txtScopeFolder.Enabled = !busy;
            btnBrowseScopeFolder.Enabled = !busy;
            btnSelectScope.Enabled = !busy;
            cmbMethod.Enabled = !busy;
            btnApplyRule.Enabled = !busy;
            btnRun.Enabled = !busy;
            btnCancel.Enabled = !busy;
            btnResolveArtist.Enabled = !busy;
            btnRefetchArtist.Enabled = !busy && btnRefetchArtist.Visible;
            UpdateMethodUi();
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            if (_busy) return;
            Plans = Array.Empty<UntaggedMatchPlan>();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void UntaggedFileMatchForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_busy) e.Cancel = true;
        }
    }
}
