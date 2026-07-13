using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace NaviMovieMaker.App.Services;

public sealed class ExternalToolService
{
    public const string DefaultYtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    public const string DefaultFfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private static readonly HttpClient HttpClient = new();

    public string ToolsFolder { get; } = Path.Combine(AppContext.BaseDirectory, "tools");

    public void EnsureToolsFolder()
    {
        Directory.CreateDirectory(ToolsFolder);
    }

    public string GetToolsPath(string executableName)
    {
        return Path.Combine(ToolsFolder, executableName);
    }

    public string? ResolveYtDlpExecutablePath(string? configuredPath)
    {
        return ResolveExecutablePath("yt-dlp.exe", configuredPath);
    }

    public Task<ExternalToolResult> CheckYtDlpAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        return CheckToolAsync(
            toolName: "yt-dlp",
            executableName: "yt-dlp.exe",
            configuredPath: settings.YtDlpPath,
            arguments: "--version",
            missingMessage: "yt-dlp が見つかりません。tools フォルダへ配置するか、設定でパスを指定してください。",
            versionParser: output => output.SplitLines().FirstOrDefault(),
            cancellationToken);
    }

    public Task<ExternalToolResult> CheckFfmpegAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        return CheckToolAsync(
            toolName: "ffmpeg",
            executableName: "ffmpeg.exe",
            configuredPath: settings.FfmpegPath,
            arguments: "-version",
            missingMessage: "ffmpeg が見つかりません。tools フォルダへ配置するか、設定でパスを指定してください。",
            versionParser: ParseFfmpegVersion,
            cancellationToken);
    }

    public Task<ExternalToolResult> CheckFfprobeAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        return CheckToolAsync(
            toolName: "ffprobe",
            executableName: "ffprobe.exe",
            configuredPath: settings.FfprobePath,
            arguments: "-version",
            missingMessage: "ffprobe が見つかりません。tools フォルダへ配置するか、設定でパスを指定してください。",
            versionParser: ParseFfmpegVersion,
            cancellationToken);
    }

    public async Task<ExternalToolCheckResult> CheckAllAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var ytDlpTask = CheckYtDlpAsync(settings, cancellationToken);
        var ffmpegTask = CheckFfmpegAsync(settings, cancellationToken);
        var ffprobeTask = CheckFfprobeAsync(settings, cancellationToken);

        return new ExternalToolCheckResult(
            await ytDlpTask,
            await ffmpegTask,
            await ffprobeTask);
    }

    public async Task InstallToolsAsync(
        AppSettings settings,
        Action<string> log,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureToolsFolder();
        var ytDlpUrl = string.IsNullOrWhiteSpace(settings.YtDlpDownloadUrl)
            ? DefaultYtDlpDownloadUrl
            : settings.YtDlpDownloadUrl.Trim();
        var ffmpegUrl = string.IsNullOrWhiteSpace(settings.FfmpegDownloadUrl)
            ? DefaultFfmpegDownloadUrl
            : settings.FfmpegDownloadUrl.Trim();

        log($"yt-dlp download URL: {ytDlpUrl}");
        log($"FFmpeg download URL: {ffmpegUrl}");

        progress?.Report("yt-dlp を取得しています...");
        await DownloadFileAsync(ytDlpUrl, GetToolsPath("yt-dlp.exe"), log, cancellationToken);

        progress?.Report("FFmpeg を取得しています...");
        var tempFolder = Path.Combine(Path.GetTempPath(), "NaviMovie-Maker", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        var zipPath = Path.Combine(tempFolder, "ffmpeg.zip");

        try
        {
            await DownloadFileAsync(ffmpegUrl, zipPath, log, cancellationToken);
            progress?.Report("FFmpeg を展開しています...");
            ZipFile.ExtractToDirectory(zipPath, tempFolder, overwriteFiles: true);

            var ffmpegExe = Directory.EnumerateFiles(tempFolder, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
            var ffprobeExe = Directory.EnumerateFiles(tempFolder, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (ffmpegExe is null || ffprobeExe is null)
            {
                throw new InvalidOperationException("FFmpeg zip の中に ffmpeg.exe または ffprobe.exe が見つかりませんでした。");
            }

            SafeReplaceFile(ffmpegExe, GetToolsPath("ffmpeg.exe"));
            SafeReplaceFile(ffprobeExe, GetToolsPath("ffprobe.exe"));
        }
        finally
        {
            try
            {
                Directory.Delete(tempFolder, recursive: true);
            }
            catch
            {
            }
        }
    }

    private async Task<ExternalToolResult> CheckToolAsync(
        string toolName,
        string executableName,
        string? configuredPath,
        string arguments,
        string missingMessage,
        Func<string, string?> versionParser,
        CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath(executableName, configuredPath);
        if (executablePath is null)
        {
            return new ExternalToolResult(toolName, false, null, null, missingMessage, string.Empty, string.Empty, null);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            var combinedOutput = string.Join(
                Environment.NewLine,
                new[] { standardOutput.Trim(), standardError.Trim() }.Where(static value => !string.IsNullOrWhiteSpace(value)));
            var version = versionParser(combinedOutput)?.Trim();
            var isAvailable = process.ExitCode == 0;
            var message = isAvailable
                ? BuildFoundMessage(toolName, version, executablePath)
                : $"{toolName} returned exit code {process.ExitCode}. {FirstNonEmptyLine(combinedOutput) ?? "No output was returned."}";

            return new ExternalToolResult(toolName, isAvailable, version, executablePath, message, standardOutput, standardError, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExternalToolResult(
                toolName,
                false,
                null,
                executablePath,
                $"{toolName} could not be checked. {ex.Message}",
                string.Empty,
                ex.ToString(),
                null);
        }
    }

    private string? ResolveExecutablePath(string executableName, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var toolsPath = GetToolsPath(executableName);
        if (File.Exists(toolsPath))
        {
            return toolsPath;
        }

        return FindOnPath(executableName);
    }

    private static string? FindOnPath(string executableName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var path in paths)
        {
            var candidate = Path.Combine(path, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = $"{destinationPath}.download";
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var fileInfo = new FileInfo(tempPath);
        if (!fileInfo.Exists || fileInfo.Length <= 0)
        {
            throw new InvalidOperationException($"Downloaded file is empty: {url}");
        }

        log($"Downloaded {fileInfo.Length:N0} bytes from {url}");
        SafeReplaceFile(tempPath, destinationPath);
        File.Delete(tempPath);
    }

    private static void SafeReplaceFile(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var backupPath = $"{destinationPath}.bak";
        if (File.Exists(destinationPath))
        {
            File.Copy(destinationPath, backupPath, overwrite: true);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string? ParseFfmpegVersion(string output)
    {
        var firstLine = output.SplitLines().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        const string ffmpegPrefix = "ffmpeg version ";
        const string ffprobePrefix = "ffprobe version ";
        var prefix = firstLine.StartsWith(ffprobePrefix, StringComparison.OrdinalIgnoreCase)
            ? ffprobePrefix
            : ffmpegPrefix;
        if (!firstLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return firstLine;
        }

        var remainder = firstLine[prefix.Length..];
        var version = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(version) ? firstLine : version;
    }

    private static string? FirstNonEmptyLine(string value)
    {
        return value.SplitLines().FirstOrDefault();
    }

    private static string BuildFoundMessage(string toolName, string? version, string executablePath)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"{toolName} detected.",
                $"Version: {(string.IsNullOrWhiteSpace(version) ? "Unknown" : version)}",
                $"Path: {executablePath}",
            });
    }
}

internal static class StringExtensions
{
    public static IEnumerable<string> SplitLines(this string value)
    {
        return value
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line));
    }
}
