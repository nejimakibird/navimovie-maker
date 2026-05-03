namespace NaviMovieMaker.App.Services;

public sealed record VideoFetchResult(
    bool IsSuccess,
    IReadOnlyList<VideoListItem> Videos,
    string StandardOutput,
    string StandardError,
    int? ExitCode);
