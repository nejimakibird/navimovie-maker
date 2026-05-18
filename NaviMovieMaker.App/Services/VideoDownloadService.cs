using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NaviMovieMaker.App.Services;

public sealed class VideoDownloadService
{
    public string YtDlpPath { get; set; } = "yt-dlp";

    public async Task<VideoDownloadResult> DownloadAsync(
        VideoListItem video,
        string workingFolder,
        int downloadOrder,
        Action<string> log,
        DownloadProfileOption downloadProfile,
        bool addNumberPrefix = true,
        Action<DownloadProgressInfo>? progress = null,
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
                FileName = YtDlpPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        var formatExpression = downloadProfile.FormatExpression
            ?? DownloadProfileCatalog.GetProfile(DownloadProfileCatalog.Mp4VideoAudio720pId).FormatExpression
            ?? "bv*[height<=720][ext=mp4]+ba[ext=m4a]/b[height<=720][ext=mp4]/b[height<=720]";
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add(formatExpression);
        process.StartInfo.ArgumentList.Add("--merge-output-format");
        process.StartInfo.ArgumentList.Add("mp4");
        process.StartInfo.ArgumentList.Add("--no-playlist");
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
        log($"Resolved yt-dlp download profile: {downloadProfile.DisplayName}");
        log($"yt-dlp format: {formatExpression}");
        log($"yt-dlp executable: {process.StartInfo.FileName}");
        log($"yt-dlp command: {ProcessLogHelper.FormatCommand(process.StartInfo.FileName, process.StartInfo.ArgumentList)}");

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

            var outputTask = ReadStreamAsync(process.StandardOutput, standardOutput, log, progress, cancellationToken);
            var errorTask = ReadStreamAsync(process.StandardError, standardError, log, progress, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            var downloadedFilePath = FindDownloadedFilePath(standardOutput.ToString(), workingFolder, outputStem);

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
                FindDownloadedFilePath(standardOutput.ToString(), workingFolder, outputStem),
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
        Action<DownloadProgressInfo>? progress,
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
            if (TryParseDownloadProgress(line, out var progressInfo))
            {
                progress?.Invoke(progressInfo);
            }
        }
    }

    private static bool TryParseDownloadProgress(string line, out DownloadProgressInfo progress)
    {
        progress = new DownloadProgressInfo(null, string.Empty, string.Empty, string.Empty);
        if (!line.Contains("[download]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var percent = TryMatchDouble(line, @"(?<value>\d+(?:\.\d+)?)%");
        var speed = TryMatchString(line, @"at\s+(?<value>\S+/s)");
        var eta = TryMatchString(line, @"ETA\s+(?<value>\S+)");
        var detail = string.Empty;
        var ofMatch = Regex.Match(line, @"of\s+(?<value>\S+(?:\s+\S+)?)", RegexOptions.IgnoreCase);
        if (ofMatch.Success)
        {
            detail = ofMatch.Groups["value"].Value.Trim();
        }

        if (percent is null && string.IsNullOrWhiteSpace(speed) && string.IsNullOrWhiteSpace(eta))
        {
            return false;
        }

        progress = new DownloadProgressInfo(percent, speed, eta, detail);
        return true;
    }

    private static double? TryMatchDouble(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success
            && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
    }

    private static string TryMatchString(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
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
