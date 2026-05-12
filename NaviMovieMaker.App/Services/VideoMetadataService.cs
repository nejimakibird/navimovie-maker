using System.Diagnostics;
using System.Text.Json;

namespace NaviMovieMaker.App.Services;

public sealed class VideoMetadataService
{
    public async Task<VideoFetchResult> FetchVideoListAsync(
        string url,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            process.StartInfo.ArgumentList.Add("--flat-playlist");
            process.StartInfo.ArgumentList.Add("--dump-json");
            process.StartInfo.ArgumentList.Add(url);

            log?.Invoke($"yt-dlp executable: {process.StartInfo.FileName}");
            log?.Invoke($"yt-dlp command: {ProcessLogHelper.FormatCommand(process.StartInfo.FileName, process.StartInfo.ArgumentList)}");
            process.Start();

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            var videos = process.ExitCode == 0
                ? ParseVideos(standardOutput)
                : new List<VideoListItem>();

            return new VideoFetchResult(process.ExitCode == 0, videos, standardOutput, standardError, process.ExitCode);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new VideoFetchResult(
                false,
                Array.Empty<VideoListItem>(),
                string.Empty,
                $"yt-dlp was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    private static List<VideoListItem> ParseVideos(string standardOutput)
    {
        var videos = new List<VideoListItem>();

        foreach (var line in standardOutput.SplitLines())
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var order = TryGetInt(root, "playlist_index") ?? videos.Count + 1;
            var id = TryGetString(root, "id") ?? string.Empty;
            var title = TryGetString(root, "title") ?? id;
            var videoUrl = TryGetString(root, "webpage_url")
                ?? TryGetString(root, "url")
                ?? string.Empty;
            var thumbnailUrl = TryGetString(root, "thumbnail")
                ?? TryGetThumbnailFromArray(root)
                ?? string.Empty;

            videos.Add(new VideoListItem
            {
                IsSelected = true,
                Order = order,
                Title = title,
                VideoId = id,
                Url = videoUrl,
                ThumbnailUrl = thumbnailUrl,
                SourceType = "YouTube",
                SourcePath = videoUrl,
                DurationText = FormatDuration(TryGetDouble(root, "duration")),
                Status = "Pending",
            });
        }

        return videos;
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds is null or < 0)
        {
            return string.Empty;
        }

        var duration = TimeSpan.FromSeconds(seconds.Value);
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static string? TryGetThumbnailFromArray(JsonElement root)
    {
        if (!root.TryGetProperty("thumbnails", out var thumbnails)
            || thumbnails.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? thumbnailUrl = null;
        foreach (var thumbnail in thumbnails.EnumerateArray())
        {
            thumbnailUrl = TryGetString(thumbnail, "url") ?? thumbnailUrl;
        }

        return thumbnailUrl;
    }

    private static int? TryGetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var value) => value,
            _ => null,
        };
    }

    private static double? TryGetDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(property.GetString(), out var value) => value,
            _ => null,
        };
    }
}
