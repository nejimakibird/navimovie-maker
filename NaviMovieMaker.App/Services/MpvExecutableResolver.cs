using System.IO;

namespace NaviMovieMaker.App.Services;

public sealed class MpvExecutableResolver
{
    private readonly string _toolsFolder;

    public MpvExecutableResolver(string toolsFolder)
    {
        _toolsFolder = toolsFolder;
    }

    public string? Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && IsMpvExecutable(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var managedPath = Path.Combine(_toolsFolder, "mpv.exe");
        if (IsMpvExecutable(managedPath))
        {
            return managedPath;
        }

        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(folder, "mpv.exe");
            if (IsMpvExecutable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsMpvExecutable(string path) =>
        File.Exists(path) && string.Equals(Path.GetFileName(path), "mpv.exe", StringComparison.OrdinalIgnoreCase);
}
