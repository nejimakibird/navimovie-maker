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

            var normalized = false;
            normalized |= NormalizeDownloadProfile(settings);
            normalized |= NormalizeVisibleOutputPresets(settings);
            normalized |= NormalizeUiOptions(settings);
            normalized |= NormalizeStartupLayout(settings);
            if (normalized)
            {
                try
                {
                    Save(settings);
                }
                catch (Exception ex)
                {
                    warningMessage = $"Settings were normalized but could not be saved. {ex.Message}";
                }
            }

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
            DownloadProfile = DownloadProfileCatalog.AutoId,
            RunMode = "Download & Convert",
            OutputPresetId = ConversionPresetCatalog.GetDefault().Id,
            AspectMode = "Keep aspect ratio + padding",
            KeepOriginalDownloadedFiles = false,
            PeakBoost = false,
            AudioNormalizationMode = AudioNormalizationMode.Peak,
            SimpleModeEnabled = false,
            TargetPeakDb = -1.0,
            TargetReplayGainVolumeDb = ReplayGainNormalizationOptions.ReplayGainReferenceVolumeDb,
            PeakLimitDb = -1.0,
            NormalizationToleranceDb = 0.5,
            MaximumNormalizationGainDb = 20.0,
            StartupLayout = "QueueFocus",
            LastCandidatesExpanded = false,
            LastLogExpanded = false,
            YtDlpDownloadUrl = ExternalToolService.DefaultYtDlpDownloadUrl,
            FfmpegDownloadUrl = ExternalToolService.DefaultFfmpegDownloadUrl,
            VisibleOutputPresetIds = ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList(),
            KnownOutputPresetIds = ConversionPresetCatalog.GetPresets().Select(static preset => preset.Id).ToList(),
        };
    }

    private static bool HasRequiredValues(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.WorkingFolder)
            && !string.IsNullOrWhiteSpace(settings.TemporaryFolder)
            && !string.IsNullOrWhiteSpace(settings.ConvertedFolder)
            && !string.IsNullOrWhiteSpace(settings.LocalVideoFolder);
    }

    private static bool NormalizeVisibleOutputPresets(AppSettings settings)
    {
        var originalVisibleIds = (settings.VisibleOutputPresetIds ?? []).ToList();
        var originalKnownIds = (settings.KnownOutputPresetIds ?? []).ToList();
        var catalogIds = ConversionPresetCatalog.GetPresets()
            .Select(static preset => preset.Id)
            .ToList();
        var catalogIdSet = catalogIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        settings.VisibleOutputPresetIds = (settings.VisibleOutputPresetIds ?? [])
            .Where(catalogIdSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var previouslyKnownIds = (settings.KnownOutputPresetIds ?? [])
            .Where(catalogIdSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (settings.VisibleOutputPresetIds.Count == 0)
        {
            settings.VisibleOutputPresetIds = ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList();
        }

        var defaultVisibleIdsToMerge = previouslyKnownIds.Count == 0
            ? ConversionPresetCatalog.GetTabletPresetIds()
            : ConversionPresetCatalog.GetDefaultVisiblePresetIds();

        foreach (var presetId in defaultVisibleIdsToMerge)
        {
            if (!catalogIdSet.Contains(presetId))
            {
                continue;
            }

            if (!previouslyKnownIds.Contains(presetId, StringComparer.OrdinalIgnoreCase)
                && !settings.VisibleOutputPresetIds.Contains(presetId, StringComparer.OrdinalIgnoreCase))
            {
                settings.VisibleOutputPresetIds.Add(presetId);
            }
        }

        settings.KnownOutputPresetIds = catalogIds;
        return !SequenceEqualIgnoreCase(originalVisibleIds, settings.VisibleOutputPresetIds)
            || !SequenceEqualIgnoreCase(originalKnownIds, settings.KnownOutputPresetIds);
    }

    private static bool NormalizeDownloadProfile(AppSettings settings)
    {
        if (!DownloadProfileCatalog.IsKnownProfile(settings.DownloadProfile))
        {
            settings.DownloadProfile = DownloadProfileCatalog.AutoId;
            return true;
        }

        return false;
    }

    private static bool NormalizeUiOptions(AppSettings settings)
    {
        var changed = false;
        string[] knownRunModes =
        [
            "Download Only",
            "Download & Convert",
            "Convert Only",
            "Copy Files",
        ];

        if (!knownRunModes.Contains(settings.RunMode, StringComparer.OrdinalIgnoreCase))
        {
            settings.RunMode = "Download & Convert";
            changed = true;
        }

        var knownPresetIds = ConversionPresetCatalog.GetPresets()
            .Select(static preset => preset.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!knownPresetIds.Contains(settings.OutputPresetId))
        {
            settings.OutputPresetId = settings.VisibleOutputPresetIds.FirstOrDefault(knownPresetIds.Contains)
                ?? (knownPresetIds.Contains(ConversionPresetCatalog.CarNaviStandardId)
                    ? ConversionPresetCatalog.CarNaviStandardId
                    : ConversionPresetCatalog.GetDefault().Id);
            changed = true;
        }

        string[] knownAspectModes =
        [
            "Keep aspect ratio + padding",
            "Stretch to fit",
        ];

        if (!knownAspectModes.Contains(settings.AspectMode, StringComparer.OrdinalIgnoreCase))
        {
            settings.AspectMode = "Keep aspect ratio + padding";
            changed = true;
        }

        double[] knownTargetPeaks = [-1.0, -3.0, -6.0];
        if (!knownTargetPeaks.Contains(settings.TargetPeakDb))
        {
            settings.TargetPeakDb = -1.0;
            changed = true;
        }

        if (!Enum.IsDefined(settings.AudioNormalizationMode))
        {
            settings.AudioNormalizationMode = AudioNormalizationMode.Peak;
            changed = true;
        }

        var normalizedReplayGainOptions = new ReplayGainNormalizationOptions(
            settings.TargetReplayGainVolumeDb,
            settings.PeakLimitDb,
            settings.NormalizationToleranceDb,
            settings.MaximumNormalizationGainDb).Normalize();
        if (settings.TargetReplayGainVolumeDb != normalizedReplayGainOptions.TargetReplayGainVolumeDb
            || settings.PeakLimitDb != normalizedReplayGainOptions.PeakLimitDb
            || settings.NormalizationToleranceDb != normalizedReplayGainOptions.ToleranceDb
            || settings.MaximumNormalizationGainDb != normalizedReplayGainOptions.MaximumGainDb)
        {
            settings.TargetReplayGainVolumeDb = normalizedReplayGainOptions.TargetReplayGainVolumeDb;
            settings.PeakLimitDb = normalizedReplayGainOptions.PeakLimitDb;
            settings.NormalizationToleranceDb = normalizedReplayGainOptions.ToleranceDb;
            settings.MaximumNormalizationGainDb = normalizedReplayGainOptions.MaximumGainDb;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeStartupLayout(AppSettings settings)
    {
        string[] knownStartupLayouts =
        [
            "QueueFocus",
            "Standard",
            "BrowserFocus",
            "LastUsed",
        ];

        var normalizedLayout = knownStartupLayouts.FirstOrDefault(
            layout => string.Equals(layout, settings.StartupLayout, StringComparison.OrdinalIgnoreCase));
        if (normalizedLayout is null)
        {
            settings.StartupLayout = "QueueFocus";
            return true;
        }

        var changed = !string.Equals(settings.StartupLayout, normalizedLayout, StringComparison.Ordinal);
        settings.StartupLayout = normalizedLayout;
        return changed;
    }

    private static bool SequenceEqualIgnoreCase(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == right.Count
            && left.Zip(right).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
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
