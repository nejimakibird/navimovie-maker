using System.IO;
using System.Text.Json;

namespace NaviMovieMaker.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NaviMovie-Maker",
        "settings.json");

    public AppSettings Load(out string? warningMessage)
    {
        warningMessage = null;

        if (!File.Exists(SettingsFilePath))
        {
            return CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null || !HasRequiredValues(settings))
            {
                warningMessage = "Settings file is missing required folder values. Defaults were loaded.";
                return CreateDefault();
            }

            NormalizeVisibleOutputPresets(settings);
            return settings;
        }
        catch (Exception ex)
        {
            warningMessage = $"Settings file could not be loaded. Defaults were loaded. {ex.Message}";
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        var settingsDirectory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    public void EnsureFolders(AppSettings settings)
    {
        Directory.CreateDirectory(settings.WorkingFolder);
        Directory.CreateDirectory(settings.TemporaryFolder);
        Directory.CreateDirectory(settings.ConvertedFolder);
        Directory.CreateDirectory(settings.LocalVideoFolder);
    }

    public AppSettings CreateDefault()
    {
        var baseFolder = Path.Combine(GetVideosFolder(), "NaviMovie-Maker");
        return new AppSettings
        {
            WorkingFolder = Path.Combine(baseFolder, "work"),
            TemporaryFolder = Path.Combine(baseFolder, "temp"),
            ConvertedFolder = Path.Combine(baseFolder, "converted"),
            LocalVideoFolder = Path.Combine(baseFolder, "local"),
            CreateSubfolderPerOutputPreset = true,
            VisibleOutputPresetIds = ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList(),
        };
    }

    private static bool HasRequiredValues(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.WorkingFolder)
            && !string.IsNullOrWhiteSpace(settings.TemporaryFolder)
            && !string.IsNullOrWhiteSpace(settings.ConvertedFolder)
            && !string.IsNullOrWhiteSpace(settings.LocalVideoFolder);
    }

    private static void NormalizeVisibleOutputPresets(AppSettings settings)
    {
        var knownIds = ConversionPresetCatalog.GetPresets()
            .Select(static preset => preset.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        settings.VisibleOutputPresetIds = settings.VisibleOutputPresetIds
            .Where(knownIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (settings.VisibleOutputPresetIds.Count == 0)
        {
            settings.VisibleOutputPresetIds = ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList();
        }
    }

    private static string GetVideosFolder()
    {
        var videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (!string.IsNullOrWhiteSpace(videosFolder))
        {
            return videosFolder;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Videos");
    }
}
