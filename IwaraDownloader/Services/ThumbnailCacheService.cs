using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 動画サムネイルを %APPDATA%\IwaraDownloader\thumbs にキャッシュして UI に供給。
    ///
    /// 取得順:
    ///   1. メモリ/ディスクキャッシュ (即時返却)
    ///   2. iwara からネット DL (レート制限あり: SettingsManager の ApiRequestDelayMs)
    ///
    /// 設計:
    ///   - キーは VideoId (.jpg として保存)
    ///   - メモリ LRU で直近 200 枚保持
    ///   - ネット DL は並列度 2 まで + 直前リクエストから ApiRequestDelayMs ms 空ける
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

        // 保存先は設定で切替可能なため固定フィールドにせず GetCachePath で都度解決する
        private string? _lastCacheDir;
        private readonly HttpClient _http;
        // メモリ LRU: 同一 lock 配下で操作。Image 所有権はキャッシュ側。
        // 外部に渡すのは必ず Bitmap clone (呼び出し側で Dispose 可能)。
        private readonly object _memLock = new();
        private readonly Dictionary<string, LinkedListNode<(string Key, Image Img)>> _memCache = new();
        private readonly LinkedList<(string Key, Image Img)> _lruList = new();
        private readonly ConcurrentDictionary<string, byte> _inflight = new();
        private readonly SemaphoreSlim _netGate = new(2, 2);   // ネット DL 並列度
        private long _lastNetRequestTick = 0;
        private readonly object _netGapLock = new();
        private const int MaxMemCache = 200;
        private volatile bool _disposed;

        private ThumbnailCacheService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("IwaraDownloader/thumb");
            _http.Timeout = TimeSpan.FromSeconds(20);
        }

        /// <summary>Roaming 配下のデフォルトキャッシュフォルダ</summary>
        public static string DefaultCacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IwaraDownloader", "thumbs");

        /// <summary>
        /// 設定 (ThumbnailCacheLocation) に従って現在のキャッシュフォルダを解決する。
        /// 1=ダウンロード先フォルダ配下の thumbs / それ以外=Roaming。
        /// </summary>
        public static string ResolveCacheDir()
        {
            var settings = Utils.SettingsManager.Instance.Settings;
            if (settings.ThumbnailCacheLocation == 1 && !string.IsNullOrWhiteSpace(settings.DownloadFolder))
                return Path.Combine(settings.DownloadFolder, "thumbs");
            return DefaultCacheDir;
        }

        public string GetCachePath(string videoId)
        {
            var dir = ResolveCacheDir();
            // 設定変更を即座に反映するため毎回解決するが、CreateDirectory はフォルダが
            // 変わった時だけにする (競合しても CreateDirectory は冪等なので問題なし)
            if (dir != _lastCacheDir)
            {
                try { Directory.CreateDirectory(dir); } catch { }
                _lastCacheDir = dir;
            }
            return Path.Combine(dir, videoId + ".jpg");
        }

        /// <summary>
        /// キャッシュフォルダ変更時に既存のサムネイルを新フォルダへ移動する (ベストエフォート)。
        /// 使用中などで移動できないファイルはスキップ (キャッシュミス扱いで再DLされる)。
        /// </summary>
        /// <returns>移動したファイル数</returns>
        public static int MigrateCacheDir(string oldDir, string newDir)
        {
            if (string.IsNullOrWhiteSpace(oldDir) || string.IsNullOrWhiteSpace(newDir)) return 0;
            if (string.Equals(Path.GetFullPath(oldDir), Path.GetFullPath(newDir), StringComparison.OrdinalIgnoreCase)) return 0;
            if (!Directory.Exists(oldDir)) return 0;

            int moved = 0;
            try
            {
                Directory.CreateDirectory(newDir);
                foreach (var src in Directory.EnumerateFiles(oldDir, "*.jpg", SearchOption.TopDirectoryOnly))
                {
                    var dst = Path.Combine(newDir, Path.GetFileName(src));
                    try
                    {
                        if (File.Exists(dst))
                            File.Delete(src); // 移動先に既にあれば旧側を削除するだけでよい
                        else
                        {
                            File.Move(src, dst);
                            moved++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Thumb migrate skip {src}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Thumb migrate failed: {ex.Message}");
            }
            return moved;
        }

        /// <summary>
        /// メモリキャッシュのみから取得 (I/O ゼロ、UI スレッド安全)。返り値はクローン。
        /// ディスクキャッシュは EnsureLoadedAsync で非同期に読み込む。
        /// </summary>
        public Image? TryGetMemoryCached(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || _disposed) return null;
            // lock 内では「LRU 先頭へ移動」+ Image 参照取得のみ。
            // Bitmap clone (ピクセルコピーで重い) は lock 外で実施して UI フリーズを防ぐ。
            // LRU 先頭にしてあるので eviction 対象外 = clone 中に Dispose されるレースは起きない。
            Image? source = null;
            lock (_memLock)
            {
                if (_memCache.TryGetValue(videoId, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    source = node.Value.Img;
                }
            }
            if (source == null) return null;
            try { return new Bitmap(source); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mem clone failed for {videoId}: {ex.Message}");
                lock (_memLock)
                {
                    if (_memCache.TryGetValue(videoId, out var node))
                    {
                        _memCache.Remove(videoId);
                        _lruList.Remove(node);
                        try { node.Value.Img.Dispose(); } catch { }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 同期取得: メモリ → ディスク の順で探す (毎回クローン)。
        /// ディスクから読み込んだ場合はメモリにも昇格させる。
        /// バックフィル等の重い処理向け。UI スレッドでは TryGetMemoryCached を使う。
        /// </summary>
        public Image? TryGetCached(string videoId)
        {
            var mem = TryGetMemoryCached(videoId);
            if (mem != null) return mem;

            var path = GetCachePath(videoId);
            if (!File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) return null;
                Bitmap stored;
                using (var ms = new MemoryStream(bytes))
                using (var loaded = Image.FromStream(ms))
                {
                    stored = new Bitmap(loaded);
                }
                PutMem(videoId, stored);
                return new Bitmap(stored);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load cached thumb {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 「メモリに無いがディスクには有る or URL から取得すべき」状態をバックグラウンドで解消する。
        /// 完了で ThumbnailReady イベント発火。
        /// UI スレッドから ImageList 更新のために呼ぶエントリポイント。
        /// </summary>
        public void EnsureLoadedAsync(string videoId, string? url)
        {
            if (string.IsNullOrEmpty(videoId) || _disposed) return;
            lock (_memLock)
            {
                if (_memCache.ContainsKey(videoId)) return; // 既にメモリにあるなら呼ばれないはずだが念のため
            }
            if (!_inflight.TryAdd(videoId, 1)) return; // 二重スケジュール防止

            _ = Task.Run(async () =>
            {
                try
                {
                    // 1. ディスクキャッシュ
                    var path = GetCachePath(videoId);
                    if (File.Exists(path))
                    {
                        try
                        {
                            var bytes = await File.ReadAllBytesAsync(path);
                            if (bytes.Length > 0)
                            {
                                Bitmap stored;
                                using (var ms = new MemoryStream(bytes))
                                using (var img = Image.FromStream(ms))
                                {
                                    stored = new Bitmap(img);
                                }
                                PutMem(videoId, stored);
                                if (!_disposed) ThumbnailReady?.Invoke(this, videoId);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Disk thumb load fail {videoId}: {ex.Message}");
                        }
                    }

                    // 2. ネット DL (URL あれば)
                    if (!string.IsNullOrEmpty(url))
                    {
                        await _netGate.WaitAsync();
                        try
                        {
                            await EnforceNetDelayAsync();
                            await DownloadFromUrlAsync(videoId, url);
                        }
                        finally { _netGate.Release(); }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EnsureLoadedAsync fail {videoId}: {ex.Message}");
                }
                finally
                {
                    _inflight.TryRemove(videoId, out _);
                }
            });
        }

        /// <summary>
        /// サムネ取得を非同期スケジュール (重複起動防止)。
        /// キャッシュにあれば何もしない。なければ iwara からネット DL。
        /// </summary>
        public void RequestAsync(string videoId, string? url)
        {
            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(url) || _disposed) return;
            lock (_memLock)
            {
                if (_memCache.ContainsKey(videoId)) return;
            }
            if (File.Exists(GetCachePath(videoId))) return;
            if (!_inflight.TryAdd(videoId, 1)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _netGate.WaitAsync();
                    try
                    {
                        await EnforceNetDelayAsync();
                        await DownloadFromUrlAsync(videoId, url);
                    }
                    finally { _netGate.Release(); }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Thumb request fail {videoId}: {ex.Message}");
                }
                finally
                {
                    _inflight.TryRemove(videoId, out _);
                }
            });
        }

        /// <summary>
        /// RequestAsync の同期版 (完了まで待つ)。バックフィル処理で順次キャッシュしたい時に使う。
        /// </summary>
        public async Task<bool> EnsureCachedAsync(string videoId, string? url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(videoId) || _disposed) return false;
            lock (_memLock)
            {
                if (_memCache.ContainsKey(videoId)) return true;
            }
            if (File.Exists(GetCachePath(videoId))) return true;
            if (string.IsNullOrEmpty(url)) return false;
            if (!_inflight.TryAdd(videoId, 1)) return false;

            try
            {
                await _netGate.WaitAsync(ct);
                try
                {
                    await EnforceNetDelayAsync();
                    await DownloadFromUrlAsync(videoId, url);
                    return File.Exists(GetCachePath(videoId));
                }
                finally { _netGate.Release(); }
            }
            finally
            {
                _inflight.TryRemove(videoId, out _);
            }
        }

        /// <summary>ApiRequestDelayMs に従って前回ネット DL からの間隔を空ける (累積方式で並列レート制限正確)</summary>
        private async Task EnforceNetDelayAsync()
        {
            int delayMs = Utils.SettingsManager.Instance.Settings.ApiRequestDelayMs;
            if (delayMs <= 0) return;
            long wait;
            lock (_netGapLock)
            {
                var now = Environment.TickCount64;
                // 「次に許可される時刻」を _lastNetRequestTick + delayMs に積み上げる
                var next = Math.Max(_lastNetRequestTick + delayMs, now);
                wait = next - now;
                _lastNetRequestTick = next;
            }
            if (wait > 0) await Task.Delay((int)wait);
        }

        /// <summary>iwara サムネ URL から DL してキャッシュ</summary>
        private async Task DownloadFromUrlAsync(string videoId, string url)
        {
            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    try { DatabaseService.Instance.UpdateThumbnailStatusByVideoId(videoId, 2); } catch { }
                    return;
                }
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0)
                {
                    try { DatabaseService.Instance.UpdateThumbnailStatusByVideoId(videoId, 2); } catch { }
                    return;
                }
                var path = GetCachePath(videoId);
                await File.WriteAllBytesAsync(path, bytes);

                Bitmap stored;
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                {
                    stored = new Bitmap(img);
                }
                PutMem(videoId, stored);

                try { DatabaseService.Instance.UpdateThumbnailStatusByVideoId(videoId, 1); } catch { }

                if (!_disposed) ThumbnailReady?.Invoke(this, videoId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Net thumb fetch fail {videoId}: {ex.Message}");
                try { DatabaseService.Instance.UpdateThumbnailStatusByVideoId(videoId, 2); } catch { }
            }
        }

        private void PutMem(string videoId, Image img)
        {
            // LRU: 既存キーなら置換、新規なら先頭追加、上限超なら末尾 evict
            lock (_memLock)
            {
                if (_disposed) { try { img.Dispose(); } catch { } return; }
                if (_memCache.TryGetValue(videoId, out var existing))
                {
                    // 旧 Bitmap を Dispose してから置換 (TryGetMemoryCached の Bitmap clone は完了済みのはず)
                    try { existing.Value.Img.Dispose(); } catch { }
                    _lruList.Remove(existing);
                    _memCache.Remove(videoId);
                }
                var node = new LinkedListNode<(string, Image)>((videoId, img));
                _lruList.AddFirst(node);
                _memCache[videoId] = node;

                while (_memCache.Count > MaxMemCache && _lruList.Last != null)
                {
                    var last = _lruList.Last;
                    _lruList.RemoveLast();
                    _memCache.Remove(last.Value.Key);
                    try { last.Value.Img.Dispose(); } catch { }
                }
            }
        }

        public void Dispose()
        {
            lock (_memLock)
            {
                _disposed = true;
                foreach (var node in _memCache.Values)
                {
                    try { node.Value.Img.Dispose(); } catch { }
                }
                _memCache.Clear();
                _lruList.Clear();
            }
            try { _http.Dispose(); } catch { }
            try { _netGate.Dispose(); } catch { }
        }
    }
}
