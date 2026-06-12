using System.Text.Json;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// ファイル移動・リネームのクラッシュセーフ化用ジャーナル。
    ///
    /// プロトコル:
    ///   1. 移動前に start 行 (videoId / 旧パス / 新パス) を追記
    ///   2. ファイル移動 + DB 更新が完了したら done 行を追記
    ///   3. バッチ終了時、全 start に done が揃っていればジャーナルを削除
    ///
    /// プロセスが強制終了・シャットダウンで死んでも、次回起動時に
    /// RecoverIfNeeded が done の無い start (中断された移動) を検出し、
    /// ファイルの実在状態から完了/巻き戻しを判断して整合性を復旧する。
    /// </summary>
    public sealed class FileMoveJournal : IDisposable
    {
        public static string JournalPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IwaraDownloader", "move_journal.jsonl");

        private class Entry
        {
            public string Phase { get; set; } = "";
            public int VideoId { get; set; }
            public string? OldPath { get; set; }
            public string? NewPath { get; set; }
        }

        private readonly StreamWriter _writer;

        private FileMoveJournal(StreamWriter writer) => _writer = writer;

        /// <summary>ジャーナルへの追記を開始する (既存があれば末尾に追記)</summary>
        public static FileMoveJournal Begin()
        {
            var dir = Path.GetDirectoryName(JournalPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // WriteThrough: OS キャッシュを介さず書く (強制終了・電源断でも行が残るように)
            var fs = new FileStream(JournalPath, FileMode.Append, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.WriteThrough);
            return new FileMoveJournal(new StreamWriter(fs) { AutoFlush = true });
        }

        /// <summary>移動の開始を記録する。newPath は連番付与後の実際の移動先を渡すこと。</summary>
        public void RecordStart(int videoId, string oldPath, string newPath)
            => WriteLineSafe(new Entry { Phase = "start", VideoId = videoId, OldPath = oldPath, NewPath = newPath });

        /// <summary>ファイル移動 + DB 更新の完了を記録する</summary>
        public void RecordDone(int videoId)
            => WriteLineSafe(new Entry { Phase = "done", VideoId = videoId });

        private void WriteLineSafe(Entry entry)
        {
            try { _writer.WriteLine(JsonSerializer.Serialize(entry)); }
            catch (Exception ex)
            {
                // ジャーナルが書けなくても移動自体は続行する (保護が外れるだけ)
                LoggingService.Instance.Warn($"移動ジャーナルの書き込みに失敗: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _writer.Dispose(); } catch { }
            // 未完了エントリが無ければジャーナルは役目を終えたので消す。
            // 残っている場合 (途中失敗など) は次回起動時の復旧に委ねる。
            try
            {
                if (ReadPendingEntries().Count == 0)
                    File.Delete(JournalPath);
            }
            catch { }
        }

        /// <summary>
        /// 前回プロセスが移動中に死んでいた場合の復旧。アプリ起動時に一度呼ぶ。
        /// </summary>
        /// <returns>復旧を行った場合はユーザー向けサマリ、何も無ければ null</returns>
        public static string? RecoverIfNeeded(DatabaseService database)
        {
            try
            {
                if (!File.Exists(JournalPath)) return null;

                var pending = ReadPendingEntries();
                int completed = 0, reverted = 0;

                foreach (var entry in pending)
                {
                    try
                    {
                        var (didComplete, didRevert) = RecoverEntry(database, entry);
                        if (didComplete) completed++;
                        if (didRevert) reverted++;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Warn(
                            $"移動ジャーナル復旧失敗: {entry.OldPath} -> {entry.NewPath}: {ex.Message}");
                    }
                }

                File.Delete(JournalPath);

                if (pending.Count == 0) return null;
                var msg = $"中断されたファイル移動を復旧しました (完了扱い: {completed} 件 / 未移動のまま: {reverted} 件)";
                LoggingService.Instance.Info(msg);
                return msg;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"移動ジャーナルの復旧処理に失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 中断された 1 エントリを、ファイルの実在状態に基づいて復旧する。
        /// 戻り値: (移動完了として処理した, 未移動として処理した)
        /// </summary>
        private static (bool, bool) RecoverEntry(DatabaseService database, Entry entry)
        {
            var oldPath = entry.OldPath!;
            var newPath = entry.NewPath!;

            // コピー途中の一時ファイルはどの状態でも不要
            try
            {
                var part = newPath + FileMoveHelper.PartSuffix;
                if (File.Exists(part)) File.Delete(part);
            }
            catch { }

            bool oldExists = File.Exists(oldPath);
            bool newExists = File.Exists(newPath);

            if (newExists && oldExists)
            {
                // クロスドライブ移動で「コピー完了 → 元削除」の間に中断したケース。
                // サイズ一致なら移動完了とみなして元を削除、不一致なら新側を不完全として破棄。
                long oldSize = new FileInfo(oldPath).Length;
                long newSize = new FileInfo(newPath).Length;
                if (oldSize == newSize)
                {
                    File.Delete(oldPath);
                    FinishMove(database, entry);
                    return (true, false);
                }
                File.Delete(newPath);
                return (false, true);
            }

            if (newExists) // old は無い → 移動は完了していたが DB 更新前に死んだ
            {
                FinishMove(database, entry);
                return (true, false);
            }

            // new が無い → 移動は始まっていない。DB も旧パスのままなので整合している。
            return (false, true);
        }

        /// <summary>移動完了の後始末: json サイドカーの追従 + DB の LocalFilePath 更新</summary>
        private static void FinishMove(DatabaseService database, Entry entry)
        {
            try
            {
                var oldJson = Path.ChangeExtension(entry.OldPath!, ".json");
                if (File.Exists(oldJson))
                {
                    var newJson = Path.ChangeExtension(entry.NewPath!, ".json");
                    if (File.Exists(newJson)) File.Delete(newJson);
                    File.Move(oldJson, newJson);
                }
            }
            catch { }

            var video = database.GetVideoById(entry.VideoId);
            if (video != null
                && !string.Equals(video.LocalFilePath, entry.NewPath, StringComparison.OrdinalIgnoreCase))
            {
                video.LocalFilePath = entry.NewPath!;
                database.UpdateVideo(video);
            }
        }

        /// <summary>done の無い start エントリを読み出す。壊れた行 (書き込み途中で死んだ末尾など) はスキップ。</summary>
        private static List<Entry> ReadPendingEntries()
        {
            var pending = new Dictionary<int, Entry>();
            if (!File.Exists(JournalPath)) return new List<Entry>();

            foreach (var line in File.ReadLines(JournalPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Entry? entry;
                try { entry = JsonSerializer.Deserialize<Entry>(line); }
                catch { continue; }
                if (entry == null) continue;

                if (entry.Phase == "start"
                    && !string.IsNullOrEmpty(entry.OldPath) && !string.IsNullOrEmpty(entry.NewPath))
                {
                    pending[entry.VideoId] = entry;
                }
                else if (entry.Phase == "done")
                {
                    pending.Remove(entry.VideoId);
                }
            }
            return pending.Values.ToList();
        }
    }
}
