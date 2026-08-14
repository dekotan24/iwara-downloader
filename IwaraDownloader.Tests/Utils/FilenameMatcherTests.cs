using IwaraDownloader.Models;
using IwaraDownloader.Utils;
using Xunit;

namespace IwaraDownloader.Tests.Utils;

public class FilenameMatcherTests
{
    private static string TestRoot() =>
        Path.Combine(Path.GetTempPath(), "IwaraDownloader.Tests", "FilenameMatcher");

    /// <summary>
    /// Issue #20。UntaggedFileMatchForm の既定テンプレートは "{title}.mp4" のまま
    /// (ユーザーが明示的に "{date}_{title}.mp4" に変更しない限り {date} は抽出されない)。
    /// この既定テンプレートだと {title} セグメントに日付プレフィックスごと丸ごと
    /// 割り当てられてしまい (PathTemplate.AssignPlaceholder の単一プレースホルダ経路)、
    /// 20文字未満の短い CJK タイトルは MinFuzzyMatchLength の壁で Substring/Prefix も
    /// 通らず一致しなかった。FilenameMatcher.CollectTitleHits 呼び出し前に
    /// PathTemplate.TrySplitLeadingDate で日付プレフィックスを剥がした版でも
    /// 再照合するフォールバックを追加し、テンプレート未設定/既定のままでも
    /// 日付プレフィックス付きファイル名を救済する。
    /// </summary>
    [Fact]
    public void Match_DefaultTitleOnlyTemplate_MatchesShortCjkTitleWithDatePrefix()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "2024-12-15_弱音小姐的驱魔仪式.mp4");
        var video = new VideoInfo
        {
            VideoId = "issue20-short-title",
            Title = "弱音小姐的驱魔仪式", // 9文字、MinFuzzyMatchLength(20) 未満
            AuthorUsername = "Haku",
        };

        var hint = PathTemplate.Extract(
            "{title}.mp4",
            filePath,
            root,
            new[] { video.AuthorUsername },
            new HashSet<string>(StringComparer.Ordinal));
        Assert.NotNull(hint);
        // {title}.mp4 は単一プレースホルダなので、日付プレフィックスごと title に入る
        Assert.Equal("2024-12-15_弱音小姐的驱魔仪式", hint.TitleValue);

        var hints = new Dictionary<string, PathTemplate.ExtractResult> { [filePath] = hint };
        var match = FilenameMatcher.Match(new[] { filePath }, new[] { video }, root, hints);

        var candidate = Assert.Single(match.Matches);
        Assert.Equal(video.VideoId, candidate.Video.VideoId);
        Assert.Equal(TitleMatchTier.Exact, candidate.Tier);
    }

    /// <summary>
    /// 同じ動画・同じファイルでも、ユーザーが明示的に "{date}_{title}.mp4" を
    /// テンプレートに入力すれば (今回の {date} 対応で) 正しく Exact 一致する。
    /// </summary>
    [Fact]
    public void Match_ExplicitDateTitleTemplate_MatchesShortCjkTitle()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "2024-12-15_弱音小姐的驱魔仪式.mp4");
        var video = new VideoInfo
        {
            VideoId = "issue20-short-title",
            Title = "弱音小姐的驱魔仪式",
            AuthorUsername = "Haku",
        };

        var hint = PathTemplate.Extract(
            "{date}_{title}.mp4",
            filePath,
            root,
            new[] { video.AuthorUsername },
            new HashSet<string>(StringComparer.Ordinal));
        Assert.NotNull(hint);
        Assert.Equal("弱音小姐的驱魔仪式", hint.TitleValue);

        var hints = new Dictionary<string, PathTemplate.ExtractResult> { [filePath] = hint };
        var match = FilenameMatcher.Match(new[] { filePath }, new[] { video }, root, hints);

        var candidate = Assert.Single(match.Matches);
        Assert.Equal(TitleMatchTier.Exact, candidate.Tier);
    }

    /// <summary>
    /// Issue #20 の別枝。日付プレフィックスが無くても、DB 側と実ファイル側で
    /// 全角/半角の丸括弧が違うだけで (それ以外は同一の) 短いタイトルは一致しなかった。
    /// CollectTitleHits の比較を NormalizeWidth (全角英数記号→半角) してから行うようにして解消。
    /// </summary>
    [Fact]
    public void Match_FullWidthVsHalfWidthParens_MatchesShortTitle()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "Sparkle（花火）.mp4");
        var video = new VideoInfo
        {
            VideoId = "issue20-width-mismatch",
            Title = "Sparkle(花火)", // 半角括弧、11文字
            AuthorUsername = "user1572781",
        };

        var match = FilenameMatcher.Match(new[] { filePath }, new[] { video }, root);

        var candidate = Assert.Single(match.Matches);
        Assert.Equal(video.VideoId, candidate.Video.VideoId);
        Assert.Equal(TitleMatchTier.Exact, candidate.Tier);
    }

    /// <summary>
    /// 安全性の確認。NormalizeWidth で全角/半角を同一視するようになった結果、
    /// 全角括弧版と半角括弧版で「別のタイトル」を持つ2本の動画が同じファイルに
    /// 対して両方 Exact 一致してしまう場合、自動確定させず要確認(Ambiguous)に
    /// 倒れることを確認する (誤ってどちらか一方に決め打ちしないこと)。
    /// </summary>
    [Fact]
    public void Match_FullWidthVsHalfWidthParens_TwoDistinctVideosTie_IsAmbiguous()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "Sparkle（花火）.mp4");
        var videoFullWidth = new VideoInfo
        {
            VideoId = "issue20-width-fullwidth",
            Title = "Sparkle（花火）",
            AuthorUsername = "user1572781",
        };
        var videoHalfWidth = new VideoInfo
        {
            VideoId = "issue20-width-halfwidth",
            Title = "Sparkle(花火)",
            AuthorUsername = "user1572781",
        };

        var match = FilenameMatcher.Match(
            new[] { filePath }, new[] { videoFullWidth, videoHalfWidth }, root);

        var candidate = Assert.Single(match.Matches);
        Assert.True(candidate.Ambiguous);
        Assert.Equal(AmbiguousReason.MultipleCandidatesForFile, candidate.AmbiguousReason);
        Assert.NotNull(candidate.AlternativeCandidates);
        Assert.Equal(2, candidate.AlternativeCandidates!.Count);
    }

    /// <summary>
    /// 安全性の確認。日付プレフィックスを剥がしたフォールバック照合を追加すると、
    /// 「日付込みのタイトルそのままの動画」(生ファイル名と無加工で Exact 一致)と
    /// 「日付を除いた同タイトルの動画」(ファイル名を加工して初めて Exact 一致)の
    /// 両方が Exact tier で候補に挙がる。この2つは Phase 2 の「同Tier内はタイトルが
    /// 長い方を採用」で決着する。生ファイル名側の一致は必ず日付プレフィックス分
    /// タイトルが長くなる (剥がした側は必ずそれより短い) ため、無加工の Exact 一致が
    /// 常に優先される。つまりフォールバックの追加候補が無加工の一致を上書きすることは
    /// 構造的に起こり得ず、曖昧化ではなく「より確からしい方を安全に選ぶ」動きになる。
    /// </summary>
    [Fact]
    public void Match_DateStrippedFallback_NeverOutranksRawExactMatch()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "2024-06-15_短いタイトル.mp4");
        var videoWithDateInTitle = new VideoInfo
        {
            VideoId = "issue20-date-in-title",
            Title = "2024-06-15_短いタイトル", // 生ファイル名とそのまま一致 (無加工)
            AuthorUsername = "someone",
        };
        var videoWithoutDate = new VideoInfo
        {
            VideoId = "issue20-date-stripped",
            Title = "短いタイトル", // 日付を剥がして初めて一致 (フォールバック側)
            AuthorUsername = "someone",
        };

        var match = FilenameMatcher.Match(
            new[] { filePath }, new[] { videoWithDateInTitle, videoWithoutDate }, root);

        var candidate = Assert.Single(match.Matches);
        Assert.False(candidate.Ambiguous);
        Assert.Equal(TitleMatchTier.Exact, candidate.Tier);
        Assert.Equal(videoWithDateInTitle.VideoId, candidate.Video.VideoId);
    }
}
