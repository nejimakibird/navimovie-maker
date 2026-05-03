using System.IO;
using System.Text;

namespace NaviMovieMaker.App.Services;

public static class SafeFileName
{
    private const int MaxSafeTitleLength = 80;

    public static string Create(string title, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(title) ? fallback : title;
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        var safeTitle = string.Join(" ", builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = "video";
        }

        return safeTitle.Length <= MaxSafeTitleLength
            ? safeTitle
            : safeTitle[..MaxSafeTitleLength].Trim();
    }
}
