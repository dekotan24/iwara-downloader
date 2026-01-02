using System.Drawing.Drawing2D;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// スプラッシュスクリーン（スタイリッシュなデザイン）
    /// </summary>
    public partial class SplashForm : Form
    {
        private static SplashForm? _instance;
        private static readonly object _lock = new();

        // グラデーションカラー
        private readonly Color _gradientStart = Color.FromArgb(45, 55, 85);
        private readonly Color _gradientEnd = Color.FromArgb(25, 30, 45);
        private readonly Color _accentColor = Color.FromArgb(100, 149, 237); // コーンフラワーブルー

        public SplashForm()
        {
            InitializeComponent();
            
            // ダブルバッファリングを有効化（ちらつき防止）
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.UserPaint, true);
        }

        /// <summary>
        /// スプラッシュを表示
        /// </summary>
        public static void ShowSplash()
        {
            lock (_lock)
            {
                if (_instance == null || _instance.IsDisposed)
                {
                    _instance = new SplashForm();
                    _instance.Show();
                    Application.DoEvents();
                }
            }
        }

        /// <summary>
        /// スプラッシュを閉じる
        /// </summary>
        public static void CloseSplash()
        {
            lock (_lock)
            {
                if (_instance != null && !_instance.IsDisposed)
                {
                    if (_instance.InvokeRequired)
                    {
                        _instance.Invoke(new Action(() =>
                        {
                            _instance.Close();
                            _instance.Dispose();
                            _instance = null;
                        }));
                    }
                    else
                    {
                        _instance.Close();
                        _instance.Dispose();
                        _instance = null;
                    }
                }
            }
        }

        /// <summary>
        /// ステータスを更新（プログレスバーは常にMarqueeスタイル）
        /// </summary>
        public static void UpdateStatus(string message, int? progress = null)
        {
            lock (_lock)
            {
                if (_instance != null && !_instance.IsDisposed)
                {
                    if (_instance.InvokeRequired)
                    {
                        _instance.Invoke(new Action(() => _instance.SetStatus(message, progress)));
                    }
                    else
                    {
                        _instance.SetStatus(message, progress);
                    }
                }
            }
        }

        private void SetStatus(string message, int? progress)
        {
            // ステータスメッセージを更新
            if (progress.HasValue && progress.Value > 0)
            {
                lblStatus.Text = $"{message} ({progress.Value}%)";
            }
            else
            {
                lblStatus.Text = message;
            }
            
            // プログレスバーは常にMarqueeスタイル（動いている感を出す）
            progressBar.Style = ProgressBarStyle.Marquee;

            Application.DoEvents();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // グラデーション背景を描画
            using var brush = new LinearGradientBrush(
                this.ClientRectangle,
                _gradientStart,
                _gradientEnd,
                LinearGradientMode.ForwardDiagonal);
            
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // 外枠（アクセントカラーのグロー効果）
            using var glowPen = new Pen(Color.FromArgb(60, _accentColor), 3);
            e.Graphics.DrawRectangle(glowPen, 1, 1, this.Width - 3, this.Height - 3);

            // 細い枠線
            using var borderPen = new Pen(Color.FromArgb(100, 120, 150), 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);

            // 装飾ライン（アクセント）
            using var accentPen = new Pen(_accentColor, 2);
            e.Graphics.DrawLine(accentPen, 30, 95, 370, 95);

            // 角のアクセント（左上）
            using var cornerBrush = new SolidBrush(Color.FromArgb(80, _accentColor));
            var cornerPoints = new Point[] 
            { 
                new Point(0, 0), 
                new Point(40, 0), 
                new Point(0, 40) 
            };
            e.Graphics.FillPolygon(cornerBrush, cornerPoints);

            // 角のアクセント（右下）
            var cornerPoints2 = new Point[] 
            { 
                new Point(this.Width, this.Height), 
                new Point(this.Width - 40, this.Height), 
                new Point(this.Width, this.Height - 40) 
            };
            e.Graphics.FillPolygon(cornerBrush, cornerPoints2);
        }
    }
}
