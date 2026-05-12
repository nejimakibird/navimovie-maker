namespace NaviMovieMaker.App.Services;

public enum LogLevel
{
    Info,
    Warn,
    Error,
    Success,
    Debug,
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }

    public LogLevel Level { get; init; }

    public string Message { get; init; } = string.Empty;
}
