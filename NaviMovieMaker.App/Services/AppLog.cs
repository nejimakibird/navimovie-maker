namespace NaviMovieMaker.App.Services;

public sealed class AppLog
{
    public event EventHandler<LogEntry>? EntryAdded;

    public void Info(string message)
    {
        Add(LogLevel.Info, message);
    }

    public void Warn(string message)
    {
        Add(LogLevel.Warn, message);
    }

    public void Error(string message)
    {
        Add(LogLevel.Error, message);
    }

    public void Success(string message)
    {
        Add(LogLevel.Success, message);
    }

    public void Debug(string message)
    {
        Add(LogLevel.Debug, message);
    }

    private void Add(LogLevel level, string message)
    {
        EntryAdded?.Invoke(
            this,
            new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
            });
    }
}
