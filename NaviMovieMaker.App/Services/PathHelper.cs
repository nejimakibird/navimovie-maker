using System.IO;

namespace NaviMovieMaker.App.Services;

public static class PathHelper
{
    public static string BuildFilePath(string folder, string desiredStem, string extension)
    {
        return Path.Combine(folder, $"{desiredStem}{NormalizeExtension(extension)}");
    }

    public static string GetUniqueFilePath(string folder, string desiredStem, string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var candidate = Path.Combine(folder, $"{desiredStem}{normalizedExtension}");
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folder, $"{desiredStem}_{suffix}{normalizedExtension}");
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith('.')
            ? extension
            : $".{extension}";
    }
}
