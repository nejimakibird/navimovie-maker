namespace NaviMovieMaker.App.Services;

public sealed record ExternalToolResult(
    string ToolName,
    bool IsAvailable,
    string? Version,
    string? ExecutablePath,
    string Message,
    string StandardOutput,
    string StandardError,
    int? ExitCode);

public sealed record ExternalToolCheckResult(
    ExternalToolResult YtDlp,
    ExternalToolResult Ffmpeg,
    ExternalToolResult Ffprobe)
{
    public bool IsReady => YtDlp.IsAvailable && Ffmpeg.IsAvailable && Ffprobe.IsAvailable;

    public IEnumerable<ExternalToolResult> Results
    {
        get
        {
            yield return YtDlp;
            yield return Ffmpeg;
            yield return Ffprobe;
        }
    }
}
