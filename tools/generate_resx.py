# tools/strings_ja.json / strings_en.json / strings_zh-Hans.json から
# IwaraDownloader/Resources/Strings*.resx を生成する。
# 文言を追加・修正するときは JSON を編集してこのスクリプトを再実行する。
import json
import os
from xml.sax.saxutils import escape

TOOLS = os.path.dirname(__file__)
OUT_DIR = os.path.join(TOOLS, "..", "IwaraDownloader", "Resources")

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""

def load(name):
    with open(os.path.join(TOOLS, name), encoding="utf-8") as f:
        return json.load(f)

def write_resx(filename, entries):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, filename)
    with open(path, "w", encoding="utf-8") as f:
        f.write(HEADER)
        for key in sorted(entries):
            f.write(f'  <data name="{escape(key)}" xml:space="preserve">\n')
            f.write(f"    <value>{escape(entries[key])}</value>\n")
            f.write("  </data>\n")
        f.write("</root>\n")
    print(f"{filename}: {len(entries)} entries")

ja = load("strings_ja.json")
en = load("strings_en.json")
zh = load("strings_zh-Hans.json")

# 翻訳漏れの検出 (ja が正、en/zh に無いキーは警告して ja のまま出力しない=ニュートラルにフォールバック)
for name, d in (("en", en), ("zh-Hans", zh)):
    missing = [k for k in ja if k not in d]
    extra = [k for k in d if k not in ja]
    if missing:
        print(f"WARN [{name}] missing {len(missing)} keys: {missing[:5]}...")
    if extra:
        print(f"WARN [{name}] extra {len(extra)} keys: {extra[:5]}...")

write_resx("Strings.resx", ja)
write_resx("Strings.en.resx", en)
write_resx("Strings.zh-Hans.resx", zh)
