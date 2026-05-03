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
