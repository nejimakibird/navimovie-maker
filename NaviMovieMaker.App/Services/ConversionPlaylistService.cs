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
        File.WriteAllText(filePath, json);
    }

    public ConversionPlaylist Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ConversionPlaylist>(json, SerializerOptions)
            ?? throw new InvalidDataException("プレイリストの内容を読み取れませんでした。");
    }
}
