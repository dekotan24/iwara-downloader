using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 保存先変更に伴う既存ファイル移動の共通ロジック。
    /// チャンネル個別の保存先変更 (MainForm) と全体DL先変更 (SettingsForm) の両方から使う。
    /// </summary>
    public static class FileMoveHelper
    {
        /// <summary>移動確認ダイアログの結果</summary>
        public enum MoveDecision
        {
            /// <summary>何もしない (保存先変更自体を中止)</summary>
            Cancel,
            /// <summary>ファイルは移動せず設定だけ変更</summary>
            SettingsOnly,
            /// <summary>ファイルを移動して設定を変更</summary>
            Move,
        }

        /// <summary>
        /// oldBase 配下に実在する移動対象ファイルを列挙する。
        /// excludeBases (チャンネル個別保存先など) 配下のファイルは対象外。
        /// </summary>
        public static List<VideoInfo> GetMovableFiles(
            IEnumerable<VideoInfo> videos, string oldBase, IEnumerable<string>? excludeBases = null)
        {
            var excludes = (excludeBases ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            return videos
                .Where(v => !string.IsNullOrEmpty(v.LocalFilePath)
                            && File.Exists(v.LocalFilePath)
                            && IsPathUnder(v.LocalFilePath, oldBase)
                            && !excludes.Any(ex => IsPathUnder(v.LocalFilePath, ex)))
                .ToList();
        }

        /// <summary>
        /// oldBase からの相対パスを保ったまま newBase 配下への移動計画を作る。
        /// </summary>
        public static List<(VideoInfo Video, string NewPath)> BuildMovePlan(
            List<VideoInfo> files, string oldBase, string newBase)
        {
            var oldFull = Path.GetFullPath(oldBase).TrimEnd('\\');
            return files.Select(v =>
            {
                var full = Path.GetFullPath(v.LocalFilePath);
                var rel = full.Substring(oldFull.Length).TrimStart('\\', '/');
                return (Video: v, NewPath: Path.Combine(newBase, rel));
            }).ToList();
        }

        /// <summary>
        /// 移動先ドライブの空き容量チェック + 確認ダイアログを表示する。
        /// 容量不足の場合は警告を出して Cancel を返す。
        /// </summary>
        /// <param name="owner">ダイアログの親ウィンドウ</param>
        /// <param name="subject">確認文の主語 (例: "ユーザー名" や "ダウンロード保存先")</param>
        public static MoveDecision ConfirmMove(
            IWin32Window owner, string subject,
            List<VideoInfo> movableFiles, string oldBase, string newBase)
        {
            if (movableFiles.Count == 0) return MoveDecision.SettingsOnly;

            long totalBytes = 0;
            foreach (var v in movableFiles)
            {
                try { totalBytes += new FileInfo(v.LocalFilePath).Length; } catch { }
            }

            // ドライブ判定 & 空き容量チェック
            var driveOld = Path.GetPathRoot(Path.GetFullPath(oldBase))?.ToUpperInvariant();
            var driveNew = Path.GetPathRoot(Path.GetFullPath(newBase))?.ToUpperInvariant();
            bool sameDrive = string.Equals(driveOld, driveNew, StringComparison.OrdinalIgnoreCase);

            string freeSpaceLine = "";
            if (sameDrive)
            {
                freeSpaceLine = "\n同ドライブのため瞬時に完了します。";
            }
            else if (!string.IsNullOrEmpty(driveNew))
            {
                try
                {
                    var di = new DriveInfo(driveNew);
                    freeSpaceLine =
                        $"\n移動先空き容量: {FormatSize(di.AvailableFreeSpace)}" +
                        $" / 必要量: {FormatSize(totalBytes)}";
                    if (di.AvailableFreeSpace < totalBytes)
                    {
                        MessageBox.Show(owner,
                            $"移動先ドライブ ({driveNew}) の空き容量が不足しています。\n\n" +
                            $"必要: {FormatSize(totalBytes)}\n" +
                            $"空き: {FormatSize(di.AvailableFreeSpace)}\n\n" +
                            "保存先変更を中止します。",
                            "容量不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return MoveDecision.Cancel;
                    }
                }
                catch (Exception ex)
                {
                    freeSpaceLine = $"\n(空き容量取得失敗: {ex.Message})";
                }
            }

            var confirm = MessageBox.Show(owner,
                $"{subject}を変更します。\n\n" +
                $"既存DL済みファイル: {movableFiles.Count} 個 ({FormatSize(totalBytes)})\n" +
                $"移動元: {oldBase}\n" +
                $"移動先: {newBase}" + freeSpaceLine + "\n\n" +
                "これらのファイルを移動先に移しますか?\n" +
                "(メタデータ .json も一緒に移動します)\n\n" +
                "[はい]   ファイルを移動して保存先を変更\n" +
                "[いいえ] ファイルは移動せず保存先設定だけ変更\n" +
                "[キャンセル] 何もしない",
                "ファイル移動の確認",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            return confirm switch
            {
                DialogResult.Yes => MoveDecision.Move,
                DialogResult.No => MoveDecision.SettingsOnly,
                _ => MoveDecision.Cancel,
            };
        }

        /// <summary>
        /// 現在の保存先設定 (チャンネル個別 or 全体DL先) の配下に無いファイルを検出し、
        /// あるべき場所への移動計画を作る。保存先変更時に容量不足などで移動に失敗した
        /// ファイルを、後からまとめて再移動するために使う。
        /// </summary>
        public static List<(VideoInfo Video, string NewPath)> BuildRelocationPlan(
            IEnumerable<VideoInfo> videos,
            IEnumerable<SubscribedUser> users,
            string downloadFolder)
        {
            var plan = new List<(VideoInfo Video, string NewPath)>();
            if (string.IsNullOrWhiteSpace(downloadFolder)) return plan;

            var userById = users.ToDictionary(u => u.Id);

            foreach (var v in videos)
            {
                if (string.IsNullOrEmpty(v.LocalFilePath) || !File.Exists(v.LocalFilePath)) continue;

                try
                {
                    string expectedBase;
                    if (v.SubscribedUserId.HasValue
                        && userById.TryGetValue(v.SubscribedUserId.Value, out var user))
                    {
                        expectedBase = user.GetSavePath(downloadFolder);
                    }
                    else
                    {
                        // 購読外の動画は全体DL先配下にあれば OK (サブフォルダ整理は崩さない)
                        expectedBase = downloadFolder;
                    }

                    if (IsPathUnder(v.LocalFilePath, expectedBase)) continue;

                    var destDir = expectedBase;
                    if (!v.SubscribedUserId.HasValue && !string.IsNullOrEmpty(v.AuthorUsername))
                    {
                        // DL 時の規則と同じく、作者名フォルダに入っていたファイルはその構造を保つ
                        var parentName = Path.GetFileName(Path.GetDirectoryName(v.LocalFilePath) ?? "");
                        var author = Helpers.SanitizeFileName(v.AuthorUsername);
                        if (string.Equals(parentName, author, StringComparison.OrdinalIgnoreCase))
                            destDir = Path.Combine(downloadFolder, author);
                    }

                    plan.Add((v, Path.Combine(destDir, Path.GetFileName(v.LocalFilePath))));
                }
                catch { /* 不正パスはスキップ */ }
            }
            return plan;
        }

        /// <summary>外部ツールで移動済みのファイルを DB に再リンクするための走査結果</summary>
        public class RelinkResult
        {
            /// <summary>再リンク可能 (検証済み)。OldFileStillExists=true は移動でなくコピーだったケース</summary>
            public List<(VideoInfo Video, string NewPath, bool OldFileStillExists)> Items { get; } = new();
            /// <summary>旧パスに実体が無く、移動先でも見つからなかった件数</summary>
            public int MissingCount { get; set; }
            /// <summary>同名ファイルは見つかったが検証 (サイズ/UUID) で一致しなかった件数</summary>
            public int UnverifiedCount { get; set; }
        }

        /// <summary>
        /// FastCopy などの外部ツールでファイルを移動した後、DB のパスだけを追従させるための計画を作る。
        /// 現在の保存先設定の配下に無い (またはリンク切れの) 動画について、保存先配下から
        /// 同名ファイルを探し、サイズ → UUID タグの順で同一性を検証する。ファイルは一切動かさない。
        /// </summary>
        public static RelinkResult BuildRelinkPlan(
            IEnumerable<VideoInfo> videos,
            IEnumerable<SubscribedUser> users,
            string downloadFolder)
        {
            var result = new RelinkResult();
            if (string.IsNullOrWhiteSpace(downloadFolder)) return result;

            var userById = users.ToDictionary(u => u.Id);
            // 期待ベースごとの {ファイル名 → パス一覧}。再帰列挙は 1 ベースにつき 1 回で済ませる
            var lookupCache = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var v in videos)
            {
                if (string.IsNullOrEmpty(v.LocalFilePath)) continue;

                try
                {
                    string expectedBase;
                    if (v.SubscribedUserId.HasValue
                        && userById.TryGetValue(v.SubscribedUserId.Value, out var user))
                    {
                        expectedBase = user.GetSavePath(downloadFolder);
                    }
                    else
                    {
                        expectedBase = downloadFolder;
                    }

                    bool oldExists = File.Exists(v.LocalFilePath);
                    if (oldExists && IsPathUnder(v.LocalFilePath, expectedBase)) continue; // 正常

                    var lookup = GetFileLookup(lookupCache, expectedBase);
                    var fileName = Path.GetFileName(v.LocalFilePath);
                    if (!lookup.TryGetValue(fileName, out var candidates))
                    {
                        if (!oldExists) result.MissingCount++;
                        continue; // 旧位置に実体があり移動先に候補が無いだけなら「未移動」(一括移動の領分)
                    }

                    string? match = null;
                    foreach (var cand in candidates)
                    {
                        if (string.Equals(Path.GetFullPath(cand), Path.GetFullPath(v.LocalFilePath),
                                StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (VerifyRelinkCandidate(v, cand))
                        {
                            match = cand;
                            break;
                        }
                    }

                    if (match == null)
                    {
                        result.UnverifiedCount++;
                        continue;
                    }
                    result.Items.Add((v, match, oldExists));
                }
                catch { /* 不正パスはスキップ */ }
            }
            return result;
        }

        /// <summary>
        /// 候補ファイルが DB レコードと同一かを検証する。
        /// 1. DB の FileSize と一致すれば OK (I/O ほぼゼロ)
        /// 2. サイズで判定できない場合 (FileSize 未記録、またはタグ書き込みでサイズが変わった等) は
        ///    mp4 埋め込みの FileUuid タグを読んで照合
        /// どちらの材料も無いものは誤リンク防止のため不採用。
        /// </summary>
        private static bool VerifyRelinkCandidate(VideoInfo video, string candidatePath)
        {
            try
            {
                var candidateSize = new FileInfo(candidatePath).Length;
                if (video.FileSize > 0 && candidateSize == video.FileSize) return true;

                if (!string.IsNullOrEmpty(video.FileUuid))
                {
                    var (_, uuid) = Services.MetadataService.ReadIwaraTags(candidatePath);
                    return string.Equals(uuid, video.FileUuid, StringComparison.OrdinalIgnoreCase);
                }

                // サイズも UUID も無い: 旧ファイルが実在するなら実サイズ同士で比較
                if (File.Exists(video.LocalFilePath))
                    return candidateSize == new FileInfo(video.LocalFilePath).Length;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, List<string>> GetFileLookup(
            Dictionary<string, Dictionary<string, List<string>>> cache, string baseDir)
        {
            var key = Path.GetFullPath(baseDir).TrimEnd('\\');
            if (cache.TryGetValue(key, out var cached)) return cached;

            var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Directory.Exists(key))
                {
                    foreach (var f in Directory.EnumerateFiles(key, "*", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileName(f);
                        if (!lookup.TryGetValue(name, out var list))
                            lookup[name] = list = new List<string>();
                        list.Add(f);
                    }
                }
            }
            catch { /* アクセス不能フォルダは空扱い */ }
            cache[key] = lookup;
            return lookup;
        }

        /// <summary>
        /// 移動計画のドライブ別必要量と空き容量のサマリ行を作る。
        /// 同一ドライブ内の移動 (rename) は容量を消費しないため集計対象外。
        /// </summary>
        /// <param name="insufficient">いずれかのドライブで空き容量が不足している場合 true</param>
        public static string BuildDriveSpaceSummary(
            List<(VideoInfo Video, string NewPath)> plan, out bool insufficient)
        {
            insufficient = false;
            var needed = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var (video, newPath) in plan)
            {
                try
                {
                    var driveOld = Path.GetPathRoot(Path.GetFullPath(video.LocalFilePath));
                    var driveNew = Path.GetPathRoot(Path.GetFullPath(newPath));
                    if (string.IsNullOrEmpty(driveNew)
                        || string.Equals(driveOld, driveNew, StringComparison.OrdinalIgnoreCase))
                        continue;
                    needed.TryGetValue(driveNew, out var sum);
                    needed[driveNew] = sum + new FileInfo(video.LocalFilePath).Length;
                }
                catch { }
            }

            if (needed.Count == 0) return "";

            var lines = new List<string>();
            foreach (var pair in needed)
            {
                try
                {
                    var free = new DriveInfo(pair.Key).AvailableFreeSpace;
                    bool ng = free < pair.Value;
                    if (ng) insufficient = true;
                    lines.Add($"{pair.Key.TrimEnd('\\')} 必要 {FormatSize(pair.Value)}" +
                              $" / 空き {FormatSize(free)}{(ng ? " ⚠不足" : "")}");
                }
                catch
                {
                    lines.Add($"{pair.Key.TrimEnd('\\')} 必要 {FormatSize(pair.Value)} (空き容量取得失敗)");
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 移動後に空になったサブフォルダを削除する (ベストエフォート)。
        /// baseFolder 自体は削除しない。隠しインデックスファイルしか残っていないフォルダも空とみなす。
        /// </summary>
        public static void CleanupEmptyDirectories(string baseFolder)
        {
            try
            {
                if (!Directory.Exists(baseFolder)) return;
                foreach (var dir in Directory.EnumerateDirectories(baseFolder, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.Length)) // 深い階層から処理
                {
                    TryDeleteDirectoryIfEmpty(dir);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"空フォルダ掃除に失敗: {baseFolder}: {ex.Message}");
            }
        }

        /// <summary>
        /// フォルダが空 (または隠しインデックスファイルのみ) なら削除する (ベストエフォート)。
        /// </summary>
        public static void TryDeleteDirectoryIfEmpty(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir);
                // インデックスキャッシュ (.iwara_index.json) だけが残っている場合も空とみなす
                if (Directory.GetDirectories(dir).Length == 0
                    && files.All(f => Path.GetFileName(f).Equals(".iwara_index.json", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var f in files)
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    Directory.Delete(dir);
                }
            }
            catch { /* 使用中などは残す */ }
        }

        /// <summary>クロスドライブ移動時の一時ファイル拡張子</summary>
        public const string PartSuffix = ".iwdlpart";

        /// <summary>
        /// 中断耐性のあるファイル移動。
        /// 同一ドライブ: File.Move (アトミックな rename)。
        /// 別ドライブ: File.Move 内部のコピー+削除は中断で移動先に不完全な本名ファイルを
        /// 残すため、一時名 (.iwdlpart) にコピー → rename → 元を削除、の順で行う。
        /// どの時点で強制終了されても「移動先の本名ファイルは常に完全」が保たれる。
        /// </summary>
        public static void MoveFileSafe(string oldPath, string newPath)
        {
            var rootOld = Path.GetPathRoot(Path.GetFullPath(oldPath));
            var rootNew = Path.GetPathRoot(Path.GetFullPath(newPath));
            if (string.Equals(rootOld, rootNew, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(oldPath, newPath);
                return;
            }

            var partPath = newPath + PartSuffix;
            try
            {
                File.Copy(oldPath, partPath, overwrite: true);
                File.Move(partPath, newPath); // 同一ボリューム内 rename = アトミック
            }
            catch
            {
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
                throw;
            }
            File.Delete(oldPath);
        }

        public static bool IsPathUnder(string filePath, string folderPath)
        {
            try
            {
                var fileFull = Path.GetFullPath(filePath);
                var folderFull = Path.GetFullPath(folderPath).TrimEnd('\\') + '\\';
                return fileFull.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
            return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
        }
    }
}
