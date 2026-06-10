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
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"空フォルダ掃除に失敗: {baseFolder}: {ex.Message}");
            }
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
