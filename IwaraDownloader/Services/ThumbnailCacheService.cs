using System.Collections.Concurrent;
using System.Net.Http;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 動画サムネイルを %APPDATA%\IwaraDownloader\thumbs にキャッシュして
    /// UI に Image を非同期で供給するサービス。
    ///
    /// 設計:
    ///   - キーは VideoId (.jpg として保存)
    ///   - メモリ LRU で直近 200 枚保持 (タイル切替で高速戻り)
    ///   - ダウンロードは並列度を制限 (HttpClient で 4 並列まで)
    ///   - DL 完了時 ThumbnailReady イベントで UI に通知 → 仮想 ListView の RedrawItems
    /// </summary>
    public class ThumbnailCacheService : IDisposable
    {
        private static ThumbnailCacheService? _instance;
        private static readonly object _lock = new();
        public static ThumbnailCacheService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock) { _instance ??= new ThumbnailCacheService(); }
                }
                return _instance;
            }
        }

        public event EventHandler<string>? ThumbnailReady;

        private readonly string _cacheDir;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, Image> _memCache = new();
        private readonly ConcurrentDictionary<string, byte> _inflight = new();
        private readonly SemaphoreSlim _gate = new(4, 4);
        private const int MaxMemCache = 200;

        private ThumbnailCacheService()
        {
            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IwaraDownloader", "thumbs");
            Directory.CreateDirectory(_cacheDir);

            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("IwaraDownloader/thumb");
            _http.Timeout = TimeSpan.FromSeconds(20);
        }

        public string GetCachePath(string videoId) => Path.Combine(_cacheDir, videoId + ".jpg");

        /// <summary>同期取得: メモリ or ディスクキャッシュにあれば即返す (毎回クローンを返す)。なければ null。
        /// 注意: Image.FromStream は stream の寿命を要求するため、必ず Bitmap にコピーして所有権を切る。
        /// 呼び出し側 (ImageList.Images.Add 等) は受け取った Image を自由に管理してよい。
        /// </summary>
        public Image? TryGetCached(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;

            if (_memCache.TryGetValue(videoId, out var cached))
            {
                // _memCache の所有権は ThumbnailCacheService、返り値は新規 Bitmap (呼び出し側で Dispose 安全)
                try { return new Bitmap(cached); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Mem clone failed for {videoId}: {ex.Message}");
                    _memCache.TryRemove(videoId, out _);
                }
            }

            var path = GetCachePath(videoId);
            if (File.Exists(path))
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    Bitmap stored;
                    using (var ms = new MemoryStream(bytes))
                    using (var loaded = Image.FromStream(ms))
                    {
                        // Image.FromStream は stream の寿命を要求する。必ず Bitmap コピーで切り離す。
                        stored = new Bitmap(loaded);
                    }
                    PutMem(videoId, stored);
                    return new Bitmap(stored); // 呼び出し側用に別コピー
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load cached thumb {path}: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>サムネ DL を非同期スケジュール (重複起動防止)</summary>
        public void RequestAsync(string videoId, string url)
        {
            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(url)) return;
            if (_memCache.ContainsKey(videoId)) return;
            if (File.Exists(GetCachePath(videoId))) return;
            if (!_inflight.TryAdd(videoId, 1)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _gate.WaitAsync();
                    try
                    {
                        var resp = await _http.GetAsync(url);
                        if (!resp.IsSuccessStatusCode) return;
                        var bytes = await resp.Content.ReadAsByteArrayAsync();
                        if (bytes.Length == 0) return;
                        var path = GetCachePath(videoId);
                        await File.WriteAllBytesAsync(path, bytes);

                        // メモリにもロード (stream を Image と切り離すため Bitmap でクローン)
                        Bitmap stored;
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                        {
                            stored = new Bitmap(img);
                        }
                        PutMem(videoId, stored);

                        ThumbnailReady?.Invoke(this, videoId);
                    }
                    finally { _gate.Release(); }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumb fetch fail {videoId}: {ex.Message}");
                }
                finally
                {
                    _inflight.TryRemove(videoId, out _);
                }
            });
        }

        private void PutMem(string videoId, Image img)
        {
            // 簡易 LRU: 上限超えたら 1 件 evict (順序保持なし、ザックリ FIFO)
            if (_memCache.Count >= MaxMemCache)
            {
                foreach (var k in _memCache.Keys)
                {
                    if (_memCache.TryRemove(k, out var old))
                    {
                        try { old.Dispose(); } catch { }
                    }
                    break;
                }
            }
            _memCache[videoId] = img;
        }

        public void Dispose()
        {
            foreach (var kv in _memCache) { try { kv.Value.Dispose(); } catch { } }
            _memCache.Clear();
            _http.Dispose();
            _gate.Dispose();
        }
    }
}
