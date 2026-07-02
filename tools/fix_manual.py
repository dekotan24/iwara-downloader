# 自動変換で拾えなかったUI文言の手作業修正 (1回限り)
import json
import os

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(TOOLS, "..", "IwaraDownloader")

def patch(rel, reps):
    p = os.path.join(ROOT, rel)
    s = open(p, encoding="utf-8-sig").read()
    for old, new in reps:
        assert old in s, f"{rel}: NOT FOUND: {old[:70]}"
        s = s.replace(old, new)
    open(p, "w", encoding="utf-8").write(s)
    print(f"{rel}: {len(reps)} patched")

# --- SearchImportForm.cs ---
patch("Forms/SearchImportForm.cs", [
    ('var title = alreadyInDb ? $"[登録済] {item.Title}" : item.Title;',
     'var title = alreadyInDb ? L.T("SearchImportForm_Registered", item.Title) : item.Title;'),
    ('var msg = $"{checkedItems.Count}件の動画をダウンロードキューに追加します。続行しますか？";',
     'var msg = L.T("SearchImportForm_ConfirmImport", checkedItems.Count);'),
])

# --- FileMoveProgressForm.cs ---
patch("Forms/FileMoveProgressForm.cs", [
    ('AppendCurrent("[中止] ユーザー操作で中止されました");', 'AppendCurrent(L.T("FileMoveProgressForm_LogAborted"));'),
    ('AppendCurrent($"[エラー] {ex.Message}");', 'AppendCurrent(L.T("FileMoveProgressForm_LogError", ex.Message));'),
    ('AppendCurrent($"[失敗] {Path.GetFileName(oldPath)}: {ex.Message}");',
     'AppendCurrent(L.T("FileMoveProgressForm_LogFailed", Path.GetFileName(oldPath), ex.Message));'),
])

print("all patched")
