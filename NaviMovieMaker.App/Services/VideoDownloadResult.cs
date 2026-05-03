namespace NaviMovieMaker.App.Services;

public sealed record VideoDownloadResult(
    bool IsSuccess,
    bool IsCanceled,
    string? DownloadedFilePath,
    string StandardOutput,
    string StandardError,
    int? ExitCode);
