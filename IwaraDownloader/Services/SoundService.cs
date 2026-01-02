using NAudio.Wave;
using IwaraDownloader.Models;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 音声再生サービス
    /// </summary>
    public class SoundService : IDisposable
    {
        private static SoundService? _instance;
        private static readonly object _lock = new();
        private static readonly object _playLock = new();

        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;
        private bool _disposed;
        private ManualResetEventSlim? _playbackCompleted;

        /// <summary>シングルトンインスタンス</summary>
        public static SoundService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SoundService();
                    }
                }
                return _instance;
            }
        }

        private SoundService() { }

        /// <summary>
        /// ダウンロード完了音を再生
        /// </summary>
        public void PlayCompletionSound()
        {
            var settings = SettingsManager.Instance.Settings;
            
            if (!settings.EnableCompletionSound)
                return;

            try
            {
                var soundPath = settings.CompletionSoundPath;
                
                // カスタム音声ファイルが設定されている場合
                if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    PlaySoundAsync(soundPath);
                }
                else
                {
                    // デフォルトのシステムサウンド
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn("Failed to play completion sound", ex);
            }
        }

        /// <summary>
        /// ダウンロードエラー音を再生
        /// </summary>
        public void PlayErrorSound()
        {
            var settings = SettingsManager.Instance.Settings;
            
            LoggingService.Instance.Debug($"PlayErrorSound called - EnableErrorSound={settings.EnableErrorSound}, ErrorSoundPath={settings.ErrorSoundPath}");
            
            if (!settings.EnableErrorSound)
            {
                LoggingService.Instance.Debug("Error sound is disabled, skipping");
                return;
            }

            try
            {
                var soundPath = settings.ErrorSoundPath;
                
                // カスタム音声ファイルが設定されている場合
                if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    LoggingService.Instance.Debug($"Playing custom error sound: {soundPath}");
                    PlaySoundAsync(soundPath);
                }
                else
                {
                    // デフォルトのシステムエラー音
                    LoggingService.Instance.Debug("Playing system error sound");
                    System.Media.SystemSounds.Hand.Play();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn("Failed to play error sound", ex);
            }
        }

        /// <summary>
        /// 音声ファイルを非同期で再生（前の再生完了を待ってから再生）
        /// </summary>
        public void PlaySoundAsync(string filePath)
        {
            Task.Run(() =>
            {
                lock (_playLock)
                {
                    PlaySoundInternal(filePath, waitForCompletion: true);
                }
            });
        }

        /// <summary>
        /// 音声ファイルを再生（同期）
        /// </summary>
        public void PlaySound(string filePath)
        {
            lock (_playLock)
            {
                PlaySoundInternal(filePath, waitForCompletion: false);
            }
        }

        /// <summary>
        /// 音声ファイルを再生（内部実装）
        /// </summary>
        /// <param name="filePath">音声ファイルパス</param>
        /// <param name="waitForCompletion">再生完了まで待機するか</param>
        private void PlaySoundInternal(string filePath, bool waitForCompletion)
        {
            try
            {
                // 前の再生を停止して完全にクリーンアップ
                StopSoundInternal();

                if (!File.Exists(filePath))
                {
                    LoggingService.Instance.Warn($"Sound file not found: {filePath}");
                    return;
                }

                _playbackCompleted = new ManualResetEventSlim(false);
                _audioReader = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                
                // 再生完了イベントを設定
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                
                _waveOut.Init(_audioReader);
                _waveOut.Play();

                LoggingService.Instance.Debug($"Playing sound: {filePath}");

                // 再生完了まで待機（オプション）
                if (waitForCompletion)
                {
                    // 最大10秒待機（無限ループ防止）
                    _playbackCompleted.Wait(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Failed to play sound: {filePath}", ex);
                StopSoundInternal();
            }
        }

        /// <summary>
        /// 再生完了イベントハンドラ
        /// </summary>
        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                LoggingService.Instance.Warn($"Playback stopped with error: {e.Exception.Message}");
            }
            
            _playbackCompleted?.Set();
        }

        /// <summary>
        /// 再生を停止
        /// </summary>
        public void StopSound()
        {
            lock (_playLock)
            {
                StopSoundInternal();
            }
        }

        /// <summary>
        /// 再生を停止（内部実装、ロックなし）
        /// </summary>
        private void StopSoundInternal()
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioReader != null)
                {
                    _audioReader.Dispose();
                    _audioReader = null;
                }

                if (_playbackCompleted != null)
                {
                    _playbackCompleted.Dispose();
                    _playbackCompleted = null;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Error stopping sound: {ex.Message}");
            }
        }

        /// <summary>
        /// 音声ファイルが有効かテスト
        /// </summary>
        public static bool IsValidSoundFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            var validExtensions = new[] { ".wav", ".mp3", ".aiff", ".wma", ".aac", ".m4a" };
            var ext = Path.GetExtension(filePath).ToLower();
            
            return validExtensions.Contains(ext);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            lock (_playLock)
            {
                StopSoundInternal();
            }
        }
    }
}
