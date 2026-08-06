using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// タグの無いファイルをどう照合するか、ユーザーに検索方法を選ばせるダイアログ。
    ///
    /// ・アーティストフォルダ選択検索: フォルダ1つ = アーティスト1人を明示指定し、
    ///   そのアーティストの動画一覧だけにスコープして逆引きする (最も安全)
    /// ・ファイル名による検索 (最終手段): ローカルDB全体を相手にした照合。
    ///   パス構成のヒント (テンプレート) を入力すれば、それを手がかりにして精度を上げられる
    ///
    /// このフォーム自体は照合ロジックを持たない。テンプレートからの抽出 (PathTemplate) と
    /// 照合の優先順位判定 (FilenameMatcher) を呼び出すだけで、結果 (FilenameMatchResult) を
    /// 呼び出し元 (ImportFromFolderWizard) にそのまま返す。レビュー画面は Step 3 を再利用する。
    /// </summary>
    public partial class UntaggedFileMatchForm : Form
    {
        private readonly List<string> _untaggedFiles;
        private readonly DatabaseService _database;
        private readonly DownloadManager _downloadManager;
        private readonly string _defaultFolder;

        private string? _resolvedArtistUsername;
        private List<VideoInfo>? _resolvedArtistVideos;
        private SubscribedUser? _resolvedSubUser;
        private bool _busy;

        /// <summary>実行結果。DialogResult.OK かつ null 以外なら呼び出し元は Step 3 の一覧に反映する</summary>
        public FilenameMatchResult? Result { get; private set; }

        /// <summary>
        /// アーティストモードで既に購読済みだった場合の SubscribedUser。
        /// 取り込み時、新規追加される動画の SubscribedUserId 補完に使う (未購読なら null のまま = 単発扱い)。
        /// </summary>
        public SubscribedUser? ResolvedSubUser { get; private set; }

        public UntaggedFileMatchForm(
            List<string> untaggedFiles, DatabaseService database, DownloadManager downloadManager, string defaultFolder)
        {
            InitializeComponent();
            _untaggedFiles = untaggedFiles;
            _database = database;
            _downloadManager = downloadManager;
            _defaultFolder = defaultFolder;

            Utils.Localizer.Apply(this);
            lblIntro.Text = L.T("UntaggedFileMatchForm_D001", _untaggedFiles.Count);

            txtArtistFolder.Text = defaultFolder;
            txtArtistTemplate.Text = "{id}_{title}.mp4";
            RadioChanged(this, EventArgs.Empty);
        }

        private void RadioChanged(object? sender, EventArgs e)
        {
            grpArtist.Enabled = rbArtist.Checked;
            grpFilename.Enabled = rbFilename.Checked;
        }

        private void btnBrowseArtistFolder_Click(object sender, EventArgs e)
        {
            using var d = new FolderBrowserDialog
            {
                Description = L.T("UntaggedFileMatchForm_D003"),
                UseDescriptionForTitle = true,
                SelectedPath = string.IsNullOrEmpty(txtArtistFolder.Text) ? _defaultFolder : txtArtistFolder.Text,
            };
            if (d.ShowDialog(this) == DialogResult.OK) txtArtistFolder.Text = d.SelectedPath;
        }

        private async void btnResolveArtist_Click(object sender, EventArgs e) => await ResolveArtistAsync(forceRefetch: false);
        private async void btnRefetchArtist_Click(object sender, EventArgs e) => await ResolveArtistAsync(forceRefetch: true);

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
            var username = Helpers.ExtractUsernameFromUrl(raw) ?? raw;

            SetBusy(true, L.T("UntaggedFileMatchForm_D006", username));
            try
            {
                var subUser = _database.GetAllSubscribedUsers()
                    .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

                List<VideoInfo> videos;
                if (subUser != null && !forceRefetch)
                {
                    videos = _database.GetAllVideos().Where(v => v.SubscribedUserId == subUser.Id).ToList();
                    lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D007", username, videos.Count);
                    btnRefetchArtist.Visible = true;
                }
                else
                {
                    if (!_downloadManager.IsLoggedIn)
                    {
                        MessageBox.Show(this, L.T("UntaggedFileMatchForm_D008"),
                            L.T("UntaggedFileMatchForm_D009"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var fetched = await _downloadManager.IwaraApi.GetUserVideosAsync(username);
                    if (fetched.Count == 0)
                    {
                        lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D010", username);
                        _resolvedArtistUsername = null;
                        _resolvedArtistVideos = null;
                        return;
                    }
                    // 単発追加等で既に DB にある分は実体 (Id!=0, LocalFilePath等が正しい) に差し替える
                    videos = fetched.Select(v => _database.GetVideoByVideoId(v.VideoId) ?? v).ToList();
                    lblArtistResolved.Text = L.T("UntaggedFileMatchForm_D011", username, videos.Count);
                    btnRefetchArtist.Visible = subUser != null;
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
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnRun_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            if (rbSkip.Checked)
            {
                Result = null;
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (rbArtist.Checked)
            {
                await RunArtistModeAsync();
            }
            else if (rbFilename.Checked)
            {
                await RunFilenameModeAsync();
            }
        }

        private async Task RunArtistModeAsync()
        {
            if (_resolvedArtistVideos == null || _resolvedArtistUsername == null)
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D013"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var folder = txtArtistFolder.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D014"), L.T("UntaggedFileMatchForm_D005"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var targetFiles = _untaggedFiles.Where(f => FileMoveHelper.IsPathUnder(f, folder)).ToList();
            if (targetFiles.Count == 0)
            {
                MessageBox.Show(this, L.T("UntaggedFileMatchForm_D015"), L.T("UntaggedFileMatchForm_D016"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var template = txtArtistTemplate.Text.Trim();
            var username = _resolvedArtistUsername;
            var videos = _resolvedArtistVideos;

            SetBusy(true, L.T("UntaggedFileMatchForm_D017", targetFiles.Count));
            try
            {
                var result = await Task.Run(() =>
                {
                    var knownIds = videos!
                        .Where(v => !string.IsNullOrEmpty(v.VideoId))
                        .Select(v => v.VideoId)
                        .ToHashSet(StringComparer.Ordinal);

                    Dictionary<string, PathTemplate.ExtractResult>? hints = null;
                    if (!string.IsNullOrEmpty(template))
                    {
                        hints = new Dictionary<string, PathTemplate.ExtractResult>();
                        foreach (var f in targetFiles)
                        {
                            var ext = PathTemplate.Extract(template, f, folder, new[] { username! }, knownIds);
                            if (ext != null) hints[f] = ext;
                        }
                    }
                    return FilenameMatcher.Match(targetFiles, videos!, folder, hints);
                });

                Result = result;
                ResolvedSubUser = _resolvedSubUser;
                // Close() は FormClosing を同期的に発火し、_busy==true のままだと
                // UntaggedFileMatchForm_FormClosing が e.Cancel=true にしてしまい閉じない。
                // finally より前に明示的に解除しておく (finally 側は例外経路の保険として残す)。
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

        private async Task RunFilenameModeAsync()
        {
            var template = txtFilenameTemplate.Text.Trim();
            var folder = _defaultFolder;
            var files = _untaggedFiles;

            SetBusy(true, L.T("UntaggedFileMatchForm_D020", files.Count));
            try
            {
                var result = await Task.Run(() =>
                {
                    var allVideos = _database.GetAllVideos();

                    Dictionary<string, PathTemplate.ExtractResult>? hints = null;
                    if (!string.IsNullOrEmpty(template))
                    {
                        var knownIds = allVideos
                            .Where(v => !string.IsNullOrEmpty(v.VideoId))
                            .Select(v => v.VideoId)
                            .ToHashSet(StringComparer.Ordinal);
                        var knownArtists = allVideos
                            .Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                            .Select(v => v.AuthorUsername)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        hints = new Dictionary<string, PathTemplate.ExtractResult>();
                        foreach (var f in files)
                        {
                            var ext = PathTemplate.Extract(template, f, folder, knownArtists, knownIds);
                            if (ext != null) hints[f] = ext;
                        }
                    }
                    return FilenameMatcher.Match(files, allVideos, folder, hints);
                });

                Result = result;
                ResolvedSubUser = null; // Mode B は新規追加を一切行わないため不要
                SetBusy(false); // Close() 前に解除 (FormClosing の e.Cancel 回避、上と同じ理由)
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

        private void SetBusy(bool busy, string? status = null)
        {
            _busy = busy;
            UseWaitCursor = busy;
            progressBar.Visible = busy;
            lblStatus.Text = status ?? "";
            rbArtist.Enabled = rbFilename.Enabled = rbSkip.Enabled = !busy;
            grpArtist.Enabled = !busy && rbArtist.Checked;
            grpFilename.Enabled = !busy && rbFilename.Checked;
            btnRun.Enabled = !busy;
            btnCancel.Enabled = !busy;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            Result = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void UntaggedFileMatchForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy) e.Cancel = true;
        }
    }
}
