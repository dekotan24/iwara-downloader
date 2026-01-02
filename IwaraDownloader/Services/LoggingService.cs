using System.Collections.Concurrent;
using System.Text;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// ログ出力サービス
    /// </summary>
    public class LoggingService : IDisposable
    {
        private static LoggingService? _instance;
        private static readonly object _lock = new();

        private readonly string _logDirectory;
        private readonly string _currentLogPath;
        private readonly ConcurrentQueue<string> _logQueue;
        private readonly CancellationTokenSource _cts;
        private readonly Task _writerTask;
        private bool _disposed;

        /// <summary>ログファイルの最大保持数（デフォルト: 10）</summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>ログレベル</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>シングルトンインスタンス</summary>
        public static LoggingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LoggingService();
                    }
                }
                return _instance;
            }
        }

        private LoggingService()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IwaraDownloader",
                "logs");

            // ログディレクトリ作成
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 今回のログファイルパス
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentLogPath = Path.Combine(_logDirectory, $"IwaraDownloader_{timestamp}.log");

            _logQueue = new ConcurrentQueue<string>();
            _cts = new CancellationTokenSource();

            // 古いログファイルを削除
            CleanupOldLogs();

            // バックグラウンドでログ書き込み
            _writerTask = Task.Run(WriteLogsAsync);

            // 起動ログ
            Info("=== IwaraDownloader Started ===");
            Info($"Log file: {_currentLogPath}");
        }

        /// <summary>
        /// 古いログファイルを削除
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "IwaraDownloader_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(MaxLogFiles - 1) // 今回のファイル分を考慮して-1
                    .ToList();

                foreach (var file in logFiles)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// ログをキューから取り出して書き込み
        /// </summary>
        private async Task WriteLogsAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (_logQueue.TryDequeue(out var logEntry))
                    {
                        await File.AppendAllTextAsync(_currentLogPath, logEntry + Environment.NewLine, Encoding.UTF8);
                    }
                    else
                    {
                        await Task.Delay(100, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 書き込みエラーは無視
                }
            }

            // 終了時に残りのログを書き込み
            FlushRemaining();
        }

        /// <summary>
        /// 残りのログを同期的に書き込み
        /// </summary>
        private void FlushRemaining()
        {
            try
            {
                var sb = new StringBuilder();
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    sb.AppendLine(logEntry);
                }
                if (sb.Length > 0)
                {
                    File.AppendAllText(_currentLogPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch { }
        }

        /// <summary>
        /// ログを出力
        /// </summary>
        private void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level < MinimumLevel) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper().PadRight(5);
            var logEntry = $"[{timestamp}] [{levelStr}] {message}";

            if (exception != null)
            {
                logEntry += Environment.NewLine + $"  Exception: {exception.GetType().Name}: {exception.Message}";
                if (exception.StackTrace != null)
                {
                    logEntry += Environment.NewLine + $"  StackTrace: {exception.StackTrace}";
                }
            }

            _logQueue.Enqueue(logEntry);

            // デバッグ出力にも表示
            System.Diagnostics.Debug.WriteLine(logEntry);
        }

        /// <summary>デバッグログ</summary>
        public void Debug(string message) => Log(LogLevel.Debug, message);

        /// <summary>情報ログ</summary>
        public void Info(string message) => Log(LogLevel.Info, message);

        /// <summary>警告ログ</summary>
        public void Warn(string message, Exception? exception = null) => Log(LogLevel.Warn, message, exception);

        /// <summary>エラーログ</summary>
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        /// <summary>致命的エラーログ</summary>
        public void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);

        /// <summary>
        /// ログディレクトリを取得
        /// </summary>
        public string LogDirectory => _logDirectory;

        /// <summary>
        /// 現在のログファイルパスを取得
        /// </summary>
        public string CurrentLogPath => _currentLogPath;

        /// <summary>
        /// ログファイル一覧を取得
        /// </summary>
        public List<FileInfo> GetLogFiles()
        {
            try
            {
                return Directory.GetFiles(_logDirectory, "IwaraDownloader_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
            }
            catch
            {
                return new List<FileInfo>();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Info("=== IwaraDownloader Stopped ===");

            _cts.Cancel();
            try
            {
                _writerTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }

            FlushRemaining();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// ログレベル
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }
}
