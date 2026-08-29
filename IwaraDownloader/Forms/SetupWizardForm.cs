using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 初回起動セットアップ・ウィザード
    /// </summary>
    public partial class SetupWizardForm : Form
    {
        private readonly EnvironmentSetupService _setup = new();
        private readonly string _appDir;
        private CancellationTokenSource? _cts;
        private int _step = 1;
        private bool _setupRunning;

        /// <summary>セットアップ完了で確定された python.exe のパス。失敗時は null。</summary>
        public string? ConfiguredPythonPath { get; private set; }

        public SetupWizardForm()
        {
            InitializeComponent();
            Utils.Localizer.Apply(this);
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            // Designer の Anchor=Top|Right + 絶対 Location だと AutoScaleMode=Font の
            // スケーリングや InitializeComponent 時の Panel サイズで Next ボタンが
            // 右にはみ出すケースがある。右下基準で明示的に配置し直す。
            this.Load += (_, _) => LayoutFooterButtons();
            this.pnlFooter.SizeChanged += (_, _) => LayoutFooterButtons();
            UpdateStepUi();
        }

        private void LayoutFooterButtons()
        {
            const int rightMargin = 15;
            const int gap = 5;
            int y = (pnlFooter.ClientSize.Height - btnCancel.Height) / 2;
            btnCancel.Location = new Point(pnlFooter.ClientSize.Width - rightMargin - btnCancel.Width, y);
            btnNext.Location = new Point(btnCancel.Left - gap - btnNext.Width, y);
            btnBack.Location = new Point(btnNext.Left - gap - btnBack.Width, y);
        }

        private void UpdateStepUi()
        {
            pnlStep1.Visible = _step == 1;
            pnlStep2.Visible = _step == 2;
            pnlStep3.Visible = _step == 3;
            pnlStep4.Visible = _step == 4;

            lblStep.Text = L.T("SetupWizardForm_D001", _step) + _step switch
            {
                1 => L.T("SetupWizardForm_D002"),
                2 => L.T("SetupWizardForm_D003"),
                3 => L.T("SetupWizardForm_D004"),
                4 => L.T("SetupWizardForm_D005"),
                _ => "",
            };

            btnBack.Enabled = _step is 2;
            btnNext.Enabled = !_setupRunning;
            btnCancel.Enabled = !_setupRunning;

            btnNext.Text = _step switch
            {
                3 => L.T("SetupWizardForm_D006"),
                4 => L.T("SetupWizardForm_D005"),
                _ => L.T("SetupWizardForm_D007"),
            };
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            if (_step > 1)
            {
                _step--;
                UpdateStepUi();
            }
        }

        private async void btnNext_Click(object sender, EventArgs e)
        {
            switch (_step)
            {
                case 1:
                    _step = 2;
                    UpdateStepUi();
                    break;

                case 2:
                    if (rbExistingPython.Checked)
                    {
                        var p = txtPythonPath.Text.Trim().Trim('"');
                        if (string.IsNullOrEmpty(p))
                        {
                            MessageBox.Show(L.T("SetupWizardForm_D008"), L.T("SetupWizardForm_D009"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        // 「python」のような相対名も許容する (パス探索でリゾルブされる)
                        if (Path.IsPathRooted(p) && !File.Exists(p))
                        {
                            MessageBox.Show(L.T("SetupWizardForm_D010", p), L.T("SetupWizardForm_D009"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    _step = 3;
                    UpdateStepUi();
                    await RunSetupAsync();
                    break;

                case 3:
                    // 実行中はNext無効化済み、ここには来ない想定
                    break;

                case 4:
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_setupRunning)
            {
                if (MessageBox.Show(L.T("SetupWizardForm_D011"), L.T("SetupWizardForm_D012"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _cts?.Cancel();
                return;
            }
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void rbExistingPython_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = rbExistingPython.Checked;
            txtPythonPath.Enabled = enabled;
            btnBrowse.Enabled = enabled;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = L.T("SetupWizardForm_D021"),
                Filter = L.T("SetupWizardForm_D013"),
                FileName = "python.exe",
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
                txtPythonPath.Text = ofd.FileName;
        }

        private async Task RunSetupAsync()
        {
            _setupRunning = true;
            _cts = new CancellationTokenSource();
            UpdateStepUi();
            txtLog.Clear();
            progressBar.Value = 0;
            lblProgressMsg.Text = L.T("SetupWizardForm_D014");

            string? pythonPath = rbExistingPython.Checked ? txtPythonPath.Text.Trim() : null;

            var progress = new Progress<SetupProgress>(p =>
            {
                if (p.Percent >= 0)
                {
                    progressBar.Value = Math.Min(100, Math.Max(0, p.Percent));
                    lblProgressMsg.Text = p.Message;
                }
                AppendLog(p.Message);
            });

            try
            {
                AppendLog(L.T("SetupWizardForm_D022"));
                AppendLog(L.T("SetupWizardForm_D023", _appDir));

                var resolvedPython = await _setup.RunFullSetupAsync(
                    pythonPath, _appDir, progress, _cts.Token);

                // 設定に保存
                SettingsManager.Instance.Settings.PythonPath = resolvedPython;
                SettingsManager.Instance.Save();

                ConfiguredPythonPath = resolvedPython;
                AppendLog(L.T("SetupWizardForm_D024"));
                _step = 4;
            }
            catch (OperationCanceledException)
            {
                AppendLog(L.T("SetupWizardForm_D025"));
                MessageBox.Show(L.T("SetupWizardForm_D015"), L.T("SetupWizardForm_D016"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog(L.T("SetupWizardForm_D026", ex.Message));
                MessageBox.Show(
                    L.T("SetupWizardForm_D017", ex.Message) +
                    L.T("SetupWizardForm_D018"),
                    L.T("SetupWizardForm_D019"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                _step = 2;
            }
            finally
            {
                _setupRunning = false;
                _cts?.Dispose();
                _cts = null;
                UpdateStepUi();
            }
        }

        private void AppendLog(string text)
        {
            if (IsDisposed) return;
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke((Action)(() => AppendLog(text)));
                return;
            }
            txtLog.AppendText(text + Environment.NewLine);
        }

        private void SetupWizardForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_setupRunning)
            {
                if (MessageBox.Show(L.T("SetupWizardForm_D020"),
                    L.T("SetupWizardForm_D012"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                _cts?.Cancel();
            }
        }
    }
}
