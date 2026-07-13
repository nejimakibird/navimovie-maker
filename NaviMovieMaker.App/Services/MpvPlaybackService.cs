using System.Diagnostics;
using System.IO;
using System.Text;

namespace NaviMovieMaker.App.Services;

public sealed class MpvPlaybackService
{
    public string PlaybackFolder { get; } = Path.Combine(Path.GetTempPath(), "NaviMovie-Maker", "playback");

    public MpvPlaybackService()
    {
        CleanupStalePlaylists();
    }

    public async Task<MpvPlaybackResult> PlayAsync(
        string mpvPath,
        IReadOnlyList<string> entries,
        string? ytDlpPath,
        Action<MpvPlaybackDiagnostics>? recordDiagnostics = null)
    {
        Directory.CreateDirectory(PlaybackFolder);
        var playlistPath = Path.Combine(PlaybackFolder, $"playlist-{Guid.NewGuid():N}.m3u8");
        File.WriteAllLines(playlistPath, entries, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var resolvedMpvPath = Path.GetFullPath(mpvPath);
        var resolvedYtDlpPath = !string.IsNullOrWhiteSpace(ytDlpPath) && File.Exists(ytDlpPath)
            ? Path.GetFullPath(ytDlpPath)
            : null;
        var arguments = new List<string> { $"--playlist={playlistPath}" };

        var diagnostics = new MpvPlaybackDiagnostics(
            resolvedMpvPath,
            resolvedYtDlpPath,
            playlistPath,
            entries.ToArray(),
            arguments);
        recordDiagnostics?.Invoke(diagnostics);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedMpvPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            if (resolvedYtDlpPath is not null)
            {
                var ytDlpFolder = Path.GetDirectoryName(resolvedYtDlpPath);
                if (!string.IsNullOrWhiteSpace(ytDlpFolder))
                {
                    var inheritedPath = startInfo.Environment.TryGetValue("PATH", out var pathValue)
                        ? pathValue
                        : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(inheritedPath)
                        ? ytDlpFolder
                        : $"{ytDlpFolder}{Path.PathSeparator}{inheritedPath}";
                }
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new MpvPlaybackResult(false, null, string.Empty, "mpv プロセスを開始できませんでした。", diagnostics);
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            if (process.ExitCode == 0)
            {
                TryDelete(playlistPath);
                return new MpvPlaybackResult(true, process.ExitCode, standardOutput, standardError, diagnostics);
            }

            return new MpvPlaybackResult(false, process.ExitCode, standardOutput, standardError, diagnostics);
        }
        catch (Exception ex)
        {
            return new MpvPlaybackResult(false, null, string.Empty, ex.ToString(), diagnostics);
        }
    }

    public void CleanupStalePlaylists()
    {
        try
        {
            if (!Directory.Exists(PlaybackFolder)) return;
            foreach (var file in Directory.EnumerateFiles(PlaybackFolder, "*.m3u8")) TryDelete(file);
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}

public sealed record MpvPlaybackDiagnostics(
    string MpvPath,
    string? YtDlpPath,
    string PlaylistPath,
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> Arguments);

public sealed record MpvPlaybackResult(
    bool Succeeded,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    MpvPlaybackDiagnostics Diagnostics);
