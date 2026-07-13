using System.IO;

namespace NaviMovieMaker.App.Services;

public sealed class PlaybackPlaylistBuilder
{
    public PlaybackPlaylistReport BuildSource(IEnumerable<ConversionQueueItem> queue)
    {
        var entries = new List<string>();
        var exclusions = new Dictionary<string, int>();
        foreach (var item in queue.OrderBy(static item => item.Order))
        {
            if (string.Equals(item.SourceType, "LocalFile", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(item.SourcePathOrUrl))
                {
                    entries.Add(Path.GetFullPath(item.SourcePathOrUrl));
                }
                else
                {
                    AddExclusion(exclusions, "元ファイルが見つからない");
                }
            }
            else if (string.Equals(item.SourceType, "OnlineVideo", StringComparison.OrdinalIgnoreCase)
                && !ContainsLineBreak(item.SourcePathOrUrl)
                && Uri.TryCreate(item.SourcePathOrUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            {
                entries.Add(item.SourcePathOrUrl);
            }
            else
            {
                AddExclusion(exclusions, "無効または未対応である");
            }
        }

        return new PlaybackPlaylistReport(entries, exclusions);
    }

    public PlaybackPlaylistReport BuildConverted(IEnumerable<ConversionQueueItem> queue)
    {
        var entries = new List<string>();
        var exclusions = new Dictionary<string, int>();
        foreach (var item in queue.OrderBy(static item => item.Order))
        {
            if (item.ResultState is not PlaylistResultState.Available || item.Result is null)
            {
                AddExclusion(exclusions, "有効な処理結果がない");
            }
            else if (!File.Exists(item.Result.ResultFilePath))
            {
                AddExclusion(exclusions, "処理結果ファイルが見つからない");
            }
            else
            {
                entries.Add(Path.GetFullPath(item.Result.ResultFilePath));
            }
        }

        return new PlaybackPlaylistReport(entries, exclusions);
    }

    private static bool ContainsLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

    private static void AddExclusion(IDictionary<string, int> exclusions, string reason)
    {
        exclusions.TryGetValue(reason, out var count);
        exclusions[reason] = count + 1;
    }
}

public sealed record PlaybackPlaylistReport(
    IReadOnlyList<string> Entries,
    IReadOnlyDictionary<string, int> Exclusions)
{
    public int ExcludedCount => Exclusions.Values.Sum();

    public bool ContainsUrl => Entries.Any(static entry =>
        Uri.TryCreate(entry, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
}
