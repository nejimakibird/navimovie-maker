using System.Text.Json;
using System.IO;

namespace NaviMovieMaker.App.Services;

public sealed class ConversionPlaylistService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public void Save(string filePath, ConversionPlaylist playlist)
    {
        var json = JsonSerializer.Serialize(playlist, SerializerOptions);
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("プレイリストの保存先フォルダーを特定できません。");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ConversionPlaylist Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ConversionPlaylist>(json, SerializerOptions)
            ?? throw new InvalidDataException("プレイリストの内容を読み取れませんでした。");
    }
}
