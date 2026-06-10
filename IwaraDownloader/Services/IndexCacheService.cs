using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// アーティストフォルダごとに `.iwara_index.json` を置き、
    /// {ファイル名 → FileUuid} のマップをキャッシュするサービス。
    ///
    /// mp4 タグを読む TagLib# 呼び出しは 1 ファイルあたり数 ms だが、
    /// 数百ファイル規模になると都度スキャンはもったいない。ファイル単位の
    /// mtime + size 比較で差分だけ読み直す事で日常運用の I/O をほぼゼロにする。
    /// </summary>
    public static class IndexCacheService
    {
        private const string CacheFileName = ".iwara_index.json";
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public class IndexEntry
        {
            [JsonPropertyName("uuid")] public string FileUuid { get; set; } = "";
            [JsonPropertyName("size")] public long Size { get; set; }
            [JsonPropertyName("mtime")] public long MtimeTicks { get; set; }
        }

        public class IndexFile
        {
            [JsonPropertyName("version")] public int Version { get; set; } = 1;
            [JsonPropertyName("scanned_at")] public string ScannedAt { get; set; } = "";
            [JsonPropertyName("entries")] public Dictionary<string, IndexEntry> Entries { get; set; } = new();
        }

        /// <summary>
        /// 指定フォルダの *.mp4 を走査し、FileUuid → フルパスの辞書を返す。
        /// キャッシュが有効な箇所は TagLib# を呼ばずに済ませる。
        /// </summary>
        public static Dictionary<string, string> GetOrScan(string folderPath)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return result;

            var cachePath = Path.Combine(folderPath, CacheFileName);
            var cache = LoadCache(cachePath) ?? new IndexFile();

            // 現在のファイル一覧 (名前 → FileInfo)
            var current = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(folderPath, "*.mp4", SearchOption.TopDirectoryOnly))
            {
                var fi = new FileInfo(path);
                current[fi.Name] = fi;
            }

            var newEntries = new Dictionary<string, IndexEntry>();
            bool cacheChanged = false;

            foreach (var (name, info) in current)
            {
                if (cache.Entries.TryGetValue(name, out var cached)
                    && cached.Size == info.Length
                    && cached.MtimeTicks == info.LastWriteTimeUtc.Ticks)
                {
                    // キャッシュ有効 - TagLib# を呼ばない
                    newEntries[name] = cached;
                }
                else
                {
                    // 新規 or 変更 → タグ読み取り
                    var (_, uuid) = MetadataService.ReadIwaraTags(info.FullName);
                    newEntries[name] = new IndexEntry
                    {
                        FileUuid = uuid ?? "",
                        Size = info.Length,
                        MtimeTicks = info.LastWriteTimeUtc.Ticks,
                    };
                    cacheChanged = true;
                }
            }

            // 消えたファイル分を検知
            if (cache.Entries.Count != newEntries.Count
                || cache.Entries.Keys.Any(k => !newEntries.ContainsKey(k)))
            {
                cacheChanged = true;
            }

            if (cacheChanged)
            {
                cache.Entries = newEntries;
                cache.ScannedAt = DateTime.UtcNow.ToString("o");
                SaveCache(cachePath, cache);
            }

            // uuid → フルパス で返す
            foreach (var (name, entry) in newEntries)
            {
                if (!string.IsNullOrEmpty(entry.FileUuid))
                {
                    result[entry.FileUuid] = Path.Combine(folderPath, name);
                }
            }

            return result;
        }

        /// <summary>
        /// フォルダのインデックスキャッシュを強制的に作り直す。
        /// マイグレーション完了後などに呼ぶ。
        /// </summary>
        public static void Invalidate(string folderPath)
        {
            var cachePath = Path.Combine(folderPath, CacheFileName);
            try
            {
                if (System.IO.File.Exists(cachePath))
                    System.IO.File.Delete(cachePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IndexCacheService.Invalidate failed: {ex.Message}");
            }
        }

        private static IndexFile? LoadCache(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                var json = System.IO.File.ReadAllText(path);
                return JsonSerializer.Deserialize<IndexFile>(json, s_jsonOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IndexCacheService.LoadCache failed: {ex.Message}");
                return null;
            }
        }

        private static void SaveCache(string path, IndexFile cache)
        {
            try
            {
                var json = JsonSerializer.Serialize(cache, s_jsonOptions);
                // 既存キャッシュは隠し属性付きのため、直接 WriteAllText (FileMode.Create) すると
                // Windows では UnauthorizedAccessException になる。一時ファイルへ書いてから
                // 差し替えることで上書き失敗と書き込み途中の破損を両方防ぐ。
                var tmpPath = path + ".tmp";
                System.IO.File.WriteAllText(tmpPath, json);
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
                System.IO.File.Move(tmpPath, path);
                // ユーザーから見えにくくするため隠しファイル属性を付与
                try
                {
                    var attrs = System.IO.File.GetAttributes(path);
                    System.IO.File.SetAttributes(path, attrs | FileAttributes.Hidden);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IndexCacheService.SaveCache failed: {ex.Message}");
            }
        }
    }
}
