namespace NaviMovieMaker.App.Services;

public sealed record FfmpegProgressInfo(
    TimeSpan? ConvertedTime,
    TimeSpan? TotalDuration,
    string Speed,
    bool IsComplete);
