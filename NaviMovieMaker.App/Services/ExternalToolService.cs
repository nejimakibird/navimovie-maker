using System.Diagnostics;

namespace NaviMovieMaker.App.Services;

public sealed class ExternalToolService
{
    public Task<ExternalToolResult> CheckYtDlpAsync(CancellationToken cancellationToken = default)
    {
        return CheckToolAsync(
            toolName: "yt-dlp",
            arguments: "--version",
            missingMessage: "yt-dlp was not found in PATH. Install it or configure the path later.",
            versionParser: output => output.SplitLines().FirstOrDefault(),
            cancellationToken);
    }

    public Task<ExternalToolResult> CheckFfmpegAsync(CancellationToken cancellationToken = default)
    {
        return CheckToolAsync(
            toolName: "ffmpeg",
            arguments: "-version",
            missingMessage: "ffmpeg was not found in PATH. Install it or configure the path later.",
            versionParser: ParseFfmpegVersion,
            cancellationToken);
    }

    private static async Task<ExternalToolResult> CheckToolAsync(
        string toolName,
        string arguments,
        string missingMessage,
        Func<string, string?> versionParser,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = toolName,
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
            var executablePath = isAvailable ? await FindExecutablePathAsync(toolName, cancellationToken) : null;
            var message = isAvailable
                ? BuildFoundMessage(toolName, version, executablePath)
                : $"{toolName} returned exit code {process.ExitCode}. {FirstNonEmptyLine(combinedOutput) ?? "No output was returned."}";

            return new ExternalToolResult(toolName, isAvailable, version, executablePath, message, standardOutput, standardError, process.ExitCode);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ExternalToolResult(toolName, false, null, null, missingMessage, string.Empty, string.Empty, null);
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
                null,
                $"{toolName} could not be checked. {ex.Message}",
                string.Empty,
                ex.ToString(),
                null);
        }
    }

    private static string? ParseFfmpegVersion(string output)
    {
        var firstLine = output.SplitLines().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        const string prefix = "ffmpeg version ";
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

    private static string BuildFoundMessage(string toolName, string? version, string? executablePath)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"{toolName} detected.",
                $"Version: {(string.IsNullOrWhiteSpace(version) ? "Unknown" : version)}",
                $"Path: {(string.IsNullOrWhiteSpace(executablePath) ? "Could not resolve executable path." : executablePath)}",
            });
    }

    private static async Task<string?> FindExecutablePathAsync(string toolName, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = toolName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await standardOutputTask;

            return process.ExitCode == 0
                ? standardOutput.SplitLines().FirstOrDefault()
                : null;
        }
        catch
        {
            return null;
        }
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
