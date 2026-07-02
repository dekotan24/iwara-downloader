# コード内の日本語文字列リテラルを L.T("キー") 呼び出しに半自動変換する。
# - 通常リテラル "..." → L.T("key")
# - 補間リテラル $"...{expr}..." → L.T("key", expr, ...) + 値は {0},{1} 形式
# 変換した文言は strings_ja.json に追記し、キーと原文の一覧を出力する
# (en/zh の翻訳は別途 JSON に追記して generate_resx.py を再実行)。
# 複数行リテラル・verbatim(@")・エスケープの複雑なものは変換せずスキップして報告する。
import json
import os
import re
import sys

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(TOOLS, "..", "IwaraDownloader")

def has_jp(s):
    return re.search(r"[ぁ-んァ-ヶ一-龠々ー]", s) is not None

# "..." リテラル(エスケープ対応)と $"..." リテラルを検出
LIT = re.compile(r'(\$?)"((?:[^"\\]|\\.)*)"')

def key_base(form, text):
    # 内容の先頭から識別しやすい短いスラッグを作る (ASCII部分のみ) + 連番で一意化
    return f"{form}_D"

def convert_file(rel_path, form, ja, existing_rev, start_no):
    path = os.path.join(ROOT, rel_path)
    src = open(path, encoding="utf-8-sig").read()
    lines = src.split("\n")
    no = start_no
    converted = []
    skipped = []
    # UI出力行のみ変換する (フィルタ判定値・DB値・ログ文字列を誤変換しないため)。
    # ログ(_logger/LogService)は開発者向けなので日本語のままでよい。
    UI_MARKERS = (
        "MessageBox.Show", "UpdateStatusBar", "ShowBalloonTip", "SetStatus",
        ".Text =", ".Text=", "ToolTipText", "StatusText", "Description =",
        "UseDescriptionForTitle", "SplashForm.UpdateStatus", "progress?.Report",
        "Filter =",  # OpenFileDialog.Filter
    )
    in_call = False  # マーカー行からセミコロンまでの複数行呼び出しを継続扱いする
    for i, line in enumerate(lines):
        if "L.T(" in line:
            continue
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*"):
            continue
        is_target = any(mk in line for mk in UI_MARKERS) or in_call
        if any(mk in line for mk in UI_MARKERS) and ";" not in line:
            in_call = True
        elif ";" in line:
            in_call = False
        if not is_target:
            if has_jp(line) and LIT.search(line):
                skipped.append((i + 1, "non-ui", stripped[:60]))
            continue
        if '@"' in line:
            if has_jp(line):
                skipped.append((i + 1, "verbatim", stripped[:60]))
            continue
        # 補間リテラル内に入れ子の文字列リテラルがある行 ({x ?? "..."} 等) は
        # 正規表現が誤マッチするためスキップして手作業に回す
        if '$"' in line and re.search(r'\{[^{}]*"', line):
            if has_jp(line):
                skipped.append((i + 1, "nested-literal", stripped[:60]))
            continue

        def repl(m):
            nonlocal no
            dollar, raw = m.group(1), m.group(2)
            text = raw.replace('\\r\\n', '\n').replace('\\n', '\n').replace('\\"', '"').replace('\\\\', '\\')
            if not has_jp(text):
                return m.group(0)
            if dollar:
                # 補間式を {0},{1}... に置き換え、式を引数化
                exprs = []
                def isub(mm):
                    inner = mm.group(1)
                    # 書式指定 {expr:F1} は式と書式に分離
                    if ':' in inner and not inner.strip().startswith('"'):
                        e, fmt = inner.split(':', 1)
                        exprs.append(e.strip())
                        return '{' + str(len(exprs) - 1) + ':' + fmt + '}'
                    exprs.append(inner.strip())
                    return '{' + str(len(exprs) - 1) + '}'
                # 補間リテラル内の {expr} (ネスト無し前提。ネストは事前スキップ)
                if re.search(r'\{[^{}]*\{', text):
                    skipped.append((i + 1, "nested-interp", text[:60]))
                    return m.group(0)
                tmpl = re.sub(r'\{([^{}]+)\}', isub, text)
                key = f"{form}_D{no:03d}"
                no += 1
                ja[key] = tmpl
                converted.append((key, tmpl))
                if not exprs:
                    return f'L.T("{key}")'
                args = ", ".join(exprs)
                return f'L.T("{key}", {args})'
            else:
                # 同一文言は既存キーを再利用
                if text in existing_rev:
                    key = existing_rev[text]
                else:
                    key = f"{form}_D{no:03d}"
                    no += 1
                    ja[key] = text
                    existing_rev[text] = key
                converted.append((key, text))
                return f'L.T("{key}")'

        lines[i] = LIT.sub(repl, line)

    open(path, "w", encoding="utf-8").write("\n".join(lines))
    return converted, skipped, no

if __name__ == "__main__":
    target = sys.argv[1]            # 例: Forms/MainForm.cs
    form = sys.argv[2]              # 例: MainForm
    ja_path = os.path.join(TOOLS, "strings_ja.json")
    ja = json.load(open(ja_path, encoding="utf-8"))
    rev = {}
    conv, skip, _ = convert_file(target, form, ja, rev, 1)
    json.dump(ja, open(ja_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"converted {len(conv)}, skipped {len(skip)}")
    for lineno, why, txt in skip:
        print(f"  SKIP L{lineno} ({why}): {txt}")
    # 翻訳作業用の一覧
    out = os.path.join(TOOLS, f"pending_{form}.json")
    json.dump(dict(dict.fromkeys(conv)), open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"key list -> {out}")
