# Designer.cs から静的UI文言を抽出して一覧化する (多言語対応の下準備)
# キー規約: {FormName}_{ControlName} / フォームタイトル={FormName}_Title / ToolTipText={FormName}_{Name}_Tip
import glob
import json
import os
import re
import sys

FORMS_DIR = os.path.join(os.path.dirname(__file__), "..", "IwaraDownloader", "Forms")

def has_jp(s):
    return re.search(r"[ぁ-んァ-ヶ一-龠々ー]", s) is not None

def unescape(s):
    return s.replace('\\r\\n', '\n').replace('\\n', '\n').replace('\\"', '"').replace('\\\\', '\\')

entries = {}  # key -> text
for path in sorted(glob.glob(os.path.join(FORMS_DIR, "*.Designer.cs"))):
    form = os.path.basename(path).replace(".Designer.cs", "")
    src = open(path, encoding="utf-8-sig").read()

    # this.<name>.Text = "..." / this.Text = "..." / ToolTipText / HeaderText
    # "this.ctrl.Text" / "this.Text" / "ctrl.Text" (this.無しのローカル変数形式) すべてに対応
    for m in re.finditer(r'(?:this\.(\w+)|this|(?<![\w.])([a-z]\w*))\.(Text|ToolTipText|HeaderText|PlaceholderText)\s*=\s*"((?:[^"\\]|\\.)*)"', src):
        name, prop, raw = m.group(1) or m.group(2), m.group(3), m.group(4)
        text = unescape(raw)
        if not has_jp(text):
            continue  # 英数字のみ(アプリ名等)は翻訳不要
        if name is None:
            key = f"{form}_Title"
        elif prop == "ToolTipText":
            key = f"{form}_{name}_Tip"
        elif prop == "PlaceholderText":
            key = f"{form}_{name}_Placeholder"
        else:
            key = f"{form}_{name}"
        if key in entries and entries[key] != text:
            print(f"WARN: duplicate key {key}", file=sys.stderr)
        entries[key] = text

out = os.path.join(os.path.dirname(__file__), "extracted_strings.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(entries, f, ensure_ascii=False, indent=1)
print(f"{len(entries)} strings -> {out}")
