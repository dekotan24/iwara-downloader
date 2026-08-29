# 最終60キーの en/zh-Hans 翻訳マージ (1回限り)
import json
import os

TOOLS = os.path.dirname(os.path.abspath(__file__))

en_add = {
"MainForm_D168": "Log in", "MainForm_D169": "Enter your iwara email address:", "MainForm_D170": "Enter your password:",
"MainForm_D171": "Add channel", "MainForm_D172": "Enter a username or profile URL:",
"MainForm_D173": "Add video", "MainForm_D174": "Enter a video URL:",
"MainForm_D175": "{0} finished", "MainForm_D176": "{0} finished (some failed)",
"MainForm_D177": "\U0001F4CA All videos [{0}/{1}]", "MainForm_D178": "⭐ Favorites [{0}]",
"MainForm_D179": "\U0001F4E5 Download queue", "MainForm_D180": "⏳ Not downloaded [{0}]",
"MainForm_D181": "✅ Downloaded [{0}]", "MainForm_D182": "⏭️ Skipped [{0}]",
"MainForm_D183": "❌ Errors [{0}]", "MainForm_D184": "\U0001F4C1 Standalone [{0}]",
"MainForm_D185": "\U0001F50D Search (title/tags)...", "MainForm_D186": "\U0001F50D Search (title/artist/tags)...",
"SettingsForm_D088": "Select the Python executable", "SettingsForm_D089": "Select the yt-dlp executable",
"SettingsForm_D090": "Export settings", "SettingsForm_D091": "Export subscriptions",
"SettingsForm_D092": "Import settings", "SettingsForm_D093": "Import subscriptions",
"SettingsForm_D094": "Select a sound file", "SettingsForm_D095": "Select an error sound file",
"SetupWizardForm_D021": "Select python.exe",
"ImportFromFolderWizard_D030": "Import complete",
"DuplicateCheckForm_D024": "Title", "DuplicateCheckForm_D025": "Channels", "DuplicateCheckForm_D026": "Channel",
"DuplicateCheckForm_D027": "Status",
"BulkImportForm_D014": "Open URL list file", "BulkImportForm_D015": "[Not fetched] {0}",
"StatisticsForm_D010": "Channel", "StatisticsForm_D011": "Total videos", "StatisticsForm_D012": "Completed",
"StatisticsForm_D013": "Failed", "StatisticsForm_D014": "Size", "StatisticsForm_D015": "Date",
"StatisticsForm_D016": "Downloads", "StatisticsForm_D017": "Error type", "StatisticsForm_D018": "Count",
"StatisticsForm_D019": "Ratio", "StatisticsForm_D020": "Sample message", "StatisticsForm_D021": "Retries",
"StatisticsForm_D022": "Failed videos", "StatisticsForm_D023": "Month", "StatisticsForm_D024": "Cumulative DLs",
"StatisticsForm_D025": "Cumulative size", "StatisticsForm_D026": "Size range", "StatisticsForm_D027": "Videos",
"StatisticsForm_D028": "Total size", "StatisticsForm_D029": "Duration range", "StatisticsForm_D030": "Total duration",
"StatisticsForm_D031": "Tag", "StatisticsForm_D032": "Site", "StatisticsForm_D033": "Author",
"StatisticsForm_D034": "Export statistics",
}

zh_add = {
"MainForm_D168": "登录", "MainForm_D169": "请输入 iwara 邮箱地址：", "MainForm_D170": "请输入密码：",
"MainForm_D171": "添加频道", "MainForm_D172": "请输入用户名或个人资料URL：",
"MainForm_D173": "添加视频", "MainForm_D174": "请输入视频URL：",
"MainForm_D175": "{0} 完成", "MainForm_D176": "{0} 结束（部分失败）",
"MainForm_D177": "\U0001F4CA 全部视频 [{0}/{1}]", "MainForm_D178": "⭐ 收藏 [{0}]",
"MainForm_D179": "\U0001F4E5 下载队列", "MainForm_D180": "⏳ 未下载 [{0}]",
"MainForm_D181": "✅ 已下载 [{0}]", "MainForm_D182": "⏭️ 已跳过 [{0}]",
"MainForm_D183": "❌ 错误 [{0}]", "MainForm_D184": "\U0001F4C1 单个视频 [{0}]",
"MainForm_D185": "\U0001F50D 搜索（标题/标签）...", "MainForm_D186": "\U0001F50D 搜索（标题/作者/标签）...",
"SettingsForm_D088": "选择 Python 可执行文件", "SettingsForm_D089": "选择 yt-dlp 可执行文件",
"SettingsForm_D090": "导出设置", "SettingsForm_D091": "导出订阅列表",
"SettingsForm_D092": "导入设置", "SettingsForm_D093": "导入订阅列表",
"SettingsForm_D094": "选择音频文件", "SettingsForm_D095": "选择错误提示音文件",
"SetupWizardForm_D021": "选择 python.exe",
"ImportFromFolderWizard_D030": "导入完成",
"DuplicateCheckForm_D024": "标题", "DuplicateCheckForm_D025": "频道数", "DuplicateCheckForm_D026": "频道",
"DuplicateCheckForm_D027": "状态",
"BulkImportForm_D014": "打开URL列表文件", "BulkImportForm_D015": "[未获取] {0}",
"StatisticsForm_D010": "频道", "StatisticsForm_D011": "视频总数", "StatisticsForm_D012": "完成",
"StatisticsForm_D013": "失败", "StatisticsForm_D014": "大小", "StatisticsForm_D015": "日期",
"StatisticsForm_D016": "下载数", "StatisticsForm_D017": "错误类型", "StatisticsForm_D018": "数量",
"StatisticsForm_D019": "比例", "StatisticsForm_D020": "代表消息", "StatisticsForm_D021": "重试次数",
"StatisticsForm_D022": "失败视频数", "StatisticsForm_D023": "年月", "StatisticsForm_D024": "累计下载数",
"StatisticsForm_D025": "累计大小", "StatisticsForm_D026": "大小区间", "StatisticsForm_D027": "视频数",
"StatisticsForm_D028": "总大小", "StatisticsForm_D029": "时长区间", "StatisticsForm_D030": "总时长",
"StatisticsForm_D031": "标签", "StatisticsForm_D032": "站点", "StatisticsForm_D033": "作者",
"StatisticsForm_D034": "导出统计",
}

for fname, add in (("strings_en.json", en_add), ("strings_zh-Hans.json", zh_add)):
    path = os.path.join(TOOLS, fname)
    d = json.load(open(path, encoding="utf-8"))
    d.update(add)
    json.dump(d, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"{fname}: +{len(add)} -> {len(d)}")
