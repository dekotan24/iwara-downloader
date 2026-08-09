using IwaraDownloader.Models;
using IwaraDownloader.Utils;
using Xunit;

namespace IwaraDownloader.Tests.Utils;

public class PathTemplateTests
{
    private static readonly IReadOnlySet<string> NoKnownIds =
        new HashSet<string>(StringComparer.Ordinal);

    public static TheoryData<string, string> SupportedDateFileNames => new()
    {
        { "2026-07-14_独角兽5.mp4", "2026-07-14" },
        { "2026_07_14_独角兽5.mp4", "2026_07_14" },
        { "2026.07.14 - 独角兽5.mp4", "2026.07.14" },
        { "20260714_独角兽5.mp4", "20260714" },
    };

    [Theory]
    [MemberData(nameof(SupportedDateFileNames))]
    public void Extract_DateTitleTemplate_RecognizesSupportedDateFormats(
        string fileName, string expectedDate)
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, fileName);

        var result = PathTemplate.Extract(
            "{date}_{title}.mp4",
            filePath,
            root,
            Array.Empty<string>(),
            NoKnownIds);

        Assert.NotNull(result);
        Assert.Equal(expectedDate, result.DateValue);
        Assert.Equal("独角兽5", result.TitleValue);
    }

    [Fact]
    public void Extract_DateTitleTemplate_RejectsInvalidCalendarDate()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "2026-99-40_独角兽5.mp4");

        var result = PathTemplate.Extract(
            "{date}_{title}.mp4",
            filePath,
            root,
            Array.Empty<string>(),
            NoKnownIds);

        Assert.Null(result);
    }

    [Fact]
    public void Extract_ArtistFolderAndDateTitleTemplate_ExtractsAllHints()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "a1277945487", "2026-07-14_独角兽5.mp4");

        var result = PathTemplate.Extract(
            "{artist}\\{date}_{title}.mp4",
            filePath,
            root,
            new[] { "a1277945487" },
            NoKnownIds);

        Assert.NotNull(result);
        Assert.Equal("a1277945487", result.ArtistValue);
        Assert.Equal("2026-07-14", result.DateValue);
        Assert.Equal("独角兽5", result.TitleValue);
    }

    [Fact]
    public void Extract_IdTitleTemplate_RemainsCompatible()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "AbCd1234_既存タイトル.mp4");
        IReadOnlySet<string> knownIds = new HashSet<string>(new[] { "AbCd1234" }, StringComparer.Ordinal);

        var result = PathTemplate.Extract(
            "{id}_{title}.mp4",
            filePath,
            root,
            Array.Empty<string>(),
            knownIds);

        Assert.NotNull(result);
        Assert.Equal("AbCd1234", result.IdValue);
        Assert.Equal("既存タイトル", result.TitleValue);
        Assert.Null(result.DateValue);
    }

    [Fact]
    public void Issue20_DateHint_AllowsShortCjkTitleToMatchExactly()
    {
        var root = TestRoot();
        var filePath = Path.Combine(root, "2026-07-14_独角兽5.mp4");
        var video = new VideoInfo
        {
            VideoId = "issue20-video",
            Title = "独角兽5",
            AuthorUsername = "a1277945487",
        };

        var hint = PathTemplate.Extract(
            "{date}_{title}.mp4",
            filePath,
            root,
            new[] { video.AuthorUsername },
            new HashSet<string>(new[] { video.VideoId }, StringComparer.Ordinal));
        Assert.NotNull(hint);

        var hints = new Dictionary<string, PathTemplate.ExtractResult>
        {
            [filePath] = hint,
        };
        var match = FilenameMatcher.Match(new[] { filePath }, new[] { video }, root, hints);

        var candidate = Assert.Single(match.Matches);
        Assert.Equal(video.VideoId, candidate.Video.VideoId);
        Assert.Equal(TitleMatchTier.Exact, candidate.Tier);
        Assert.False(candidate.Ambiguous);
    }

    private static string TestRoot() =>
        Path.Combine(Path.GetTempPath(), "IwaraDownloader.Tests", "PathTemplate");
}
