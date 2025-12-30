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

        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;
        private bool _disposed;

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
                    PlaySound(soundPath);
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
        /// 音声ファイルを再生
        /// </summary>
        public void PlaySound(string filePath)
        {
            try
            {
                // 前の再生を停止
                StopSound();

                if (!File.Exists(filePath))
                {
                    LoggingService.Instance.Warn($"Sound file not found: {filePath}");
                    return;
                }

                _audioReader = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioReader);
                _waveOut.Play();

                LoggingService.Instance.Debug($"Playing sound: {filePath}");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Failed to play sound: {filePath}", ex);
                StopSound();
            }
        }

        /// <summary>
        /// 再生を停止
        /// </summary>
        public void StopSound()
        {
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _audioReader?.Dispose();
                _audioReader = null;
            }
            catch { }
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
            StopSound();
        }
    }
}
