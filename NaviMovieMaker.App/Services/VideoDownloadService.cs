using System.Diagnostics;
using System.IO;
using System.Text;

namespace NaviMovieMaker.App.Services;

public sealed class VideoDownloadService
{
    public async Task<VideoDownloadResult> DownloadAsync(
        VideoListItem video,
        string workingFolder,
        int downloadOrder,
        Action<string> log,
        bool addNumberPrefix = true,
        CancellationToken cancellationToken = default)
    {
        var videoUrl = ResolveVideoUrl(video);
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            return new VideoDownloadResult(false, false, null, string.Empty, "Video URL is empty.", null);
        }

        var safeTitle = SafeFileName.Create(video.Title, video.VideoId);
        var desiredStem = addNumberPrefix
            ? $"{downloadOrder:000}_{safeTitle}"
            : safeTitle;
        var outputStem = GetUniqueOutputStem(workingFolder, desiredStem);
        var outputTemplate = Path.Combine(workingFolder, $"{outputStem}.%(ext)s");

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

        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("bv*[height<=720]+ba/b[height<=720]");
        process.StartInfo.ArgumentList.Add("--merge-output-format");
        process.StartInfo.ArgumentList.Add("mp4");
        process.StartInfo.ArgumentList.Add("--retries");
        process.StartInfo.ArgumentList.Add("5");
        process.StartInfo.ArgumentList.Add("--fragment-retries");
        process.StartInfo.ArgumentList.Add("10");
        process.StartInfo.ArgumentList.Add("--file-access-retries");
        process.StartInfo.ArgumentList.Add("5");
        process.StartInfo.ArgumentList.Add("--retry-sleep");
        process.StartInfo.ArgumentList.Add("linear=2:10:2");
        process.StartInfo.ArgumentList.Add("--print");
        process.StartInfo.ArgumentList.Add("after_move:filepath");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(outputTemplate);
        process.StartInfo.ArgumentList.Add(videoUrl);

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            process.Start();
            log($"yt-dlp output template: {outputTemplate}");

            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            var outputTask = ReadStreamAsync(process.StandardOutput, standardOutput, log, cancellationToken);
            var errorTask = ReadStreamAsync(process.StandardError, standardError, log, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            var downloadedFilePath = process.ExitCode == 0
                ? FindDownloadedFilePath(standardOutput.ToString(), workingFolder, outputStem)
                : null;

            return new VideoDownloadResult(
                process.ExitCode == 0,
                false,
                downloadedFilePath,
                standardOutput.ToString(),
                standardError.ToString(),
                process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new VideoDownloadResult(
                false,
                true,
                null,
                standardOutput.ToString(),
                standardError.ToString(),
                null);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new VideoDownloadResult(
                false,
                false,
                null,
                standardOutput.ToString(),
                $"yt-dlp was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder output,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            output.AppendLine(line);
            log(line);
        }
    }

    private static string ResolveVideoUrl(VideoListItem video)
    {
        if (Uri.TryCreate(video.Url, UriKind.Absolute, out _))
        {
            return video.Url;
        }

        if (!string.IsNullOrWhiteSpace(video.VideoId))
        {
            return $"https://www.youtube.com/watch?v={video.VideoId}";
        }

        return video.Url;
    }

    private static string GetUniqueOutputStem(string folder, string desiredStem)
    {
        var candidate = desiredStem;
        var suffix = 2;
        while (Directory.EnumerateFiles(folder, $"{candidate}.*").Any())
        {
            candidate = $"{desiredStem}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string? FindDownloadedFilePath(string standardOutput, string workingFolder, string expectedStem)
    {
        foreach (var line in standardOutput.SplitLines().Reverse())
        {
            var candidate = line.Trim().Trim('"');
            if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory
            .EnumerateFiles(workingFolder, $"{expectedStem}.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
