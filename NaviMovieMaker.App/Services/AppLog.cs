namespace NaviMovieMaker.App.Services;

public sealed class AppLog
{
    public event EventHandler<string>? EntryAdded;

    public void Info(string message)
    {
        Add("INFO", message);
    }

    public void Error(string message)
    {
        Add("ERROR", message);
    }

    private void Add(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";
        EntryAdded?.Invoke(this, entry);
    }
}
