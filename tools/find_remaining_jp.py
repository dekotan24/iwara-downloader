# UI表示に残っている日本語リテラルを洗い出す(ログ/コメント除外)
import glob
import io
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "IwaraDownloader")

def has_jp(s):
    return re.search(r"[ぁ-んァ-ヶ一-龠々ー]", s) is not None

LIT = re.compile(r'"(?:[^"\\]|\\.)*"')
LOG_MARKERS = ("_logger.", "LoggingService", "Debug.WriteLine", "logger.")

total = 0
for sub in ("Forms", "Services", "Utils"):
    for path in sorted(glob.glob(os.path.join(ROOT, sub, "*.cs"))):
        hits = []
        for i, line in enumerate(open(path, encoding="utf-8-sig").read().split("\n")):
            st = line.strip()
            if st.startswith("//") or st.startswith("*") or st.startswith("///"):
                continue
            lits = LIT.findall(line)
            if not any(has_jp(l) for l in lits):
                continue
            if any(mk in line for mk in LOG_MARKERS):
                continue
            hits.append((i + 1, st[:110]))
        if hits:
            rel = os.path.relpath(path, ROOT)
            print(f"\n### {rel} ({len(hits)})")
            for n, t in hits:
                print(f"  L{n}: {t}")
            total += len(hits)
print(f"\nTOTAL: {total}")
