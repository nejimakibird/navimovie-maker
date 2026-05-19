namespace NaviMovieMaker.App;

public sealed class AppSettings
{
    public string WorkingFolder { get; set; } = string.Empty;

    public string TemporaryFolder { get; set; } = string.Empty;

    public string ConvertedFolder { get; set; } = string.Empty;

    public string LocalVideoFolder { get; set; } = string.Empty;

    public bool CreateSubfolderPerOutputPreset { get; set; } = true;

    public string DownloadProfile { get; set; } = Services.DownloadProfileCatalog.AutoId;

    public string RunMode { get; set; } = "Download & Convert";

    public string OutputPresetId { get; set; } = Services.ConversionPresetCatalog.GetDefault().Id;

    public string AspectMode { get; set; } = "Keep aspect ratio + padding";

    public bool KeepOriginalDownloadedFiles { get; set; }

    public bool PeakBoost { get; set; }

    public bool SimpleModeEnabled { get; set; }

    public double TargetPeakDb { get; set; } = -1.0;

    public string StartupLayout { get; set; } = "QueueFocus";

    public bool LastCandidatesExpanded { get; set; }

    public bool LastLogExpanded { get; set; } = true;

    public double LastWindowWidth { get; set; }

    public double LastWindowHeight { get; set; }

    public double LastVideoListRowHeight { get; set; }

    public double LastQueueRowHeight { get; set; }

    public double LastLogRowHeight { get; set; }

    public string YtDlpPath { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = string.Empty;

    public string FfprobePath { get; set; } = string.Empty;

    public string YtDlpDownloadUrl { get; set; } = string.Empty;

    public string FfmpegDownloadUrl { get; set; } = string.Empty;

    public List<string> VisibleOutputPresetIds { get; set; } =
        Services.ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            WorkingFolder = WorkingFolder,
            TemporaryFolder = TemporaryFolder,
            ConvertedFolder = ConvertedFolder,
            LocalVideoFolder = LocalVideoFolder,
            CreateSubfolderPerOutputPreset = CreateSubfolderPerOutputPreset,
            DownloadProfile = DownloadProfile,
            RunMode = RunMode,
            OutputPresetId = OutputPresetId,
            AspectMode = AspectMode,
            KeepOriginalDownloadedFiles = KeepOriginalDownloadedFiles,
            PeakBoost = PeakBoost,
            SimpleModeEnabled = SimpleModeEnabled,
            TargetPeakDb = TargetPeakDb,
            StartupLayout = StartupLayout,
            LastCandidatesExpanded = LastCandidatesExpanded,
            LastLogExpanded = LastLogExpanded,
            LastWindowWidth = LastWindowWidth,
            LastWindowHeight = LastWindowHeight,
            LastVideoListRowHeight = LastVideoListRowHeight,
            LastQueueRowHeight = LastQueueRowHeight,
            LastLogRowHeight = LastLogRowHeight,
            YtDlpPath = YtDlpPath,
            FfmpegPath = FfmpegPath,
            FfprobePath = FfprobePath,
            YtDlpDownloadUrl = YtDlpDownloadUrl,
            FfmpegDownloadUrl = FfmpegDownloadUrl,
            VisibleOutputPresetIds = VisibleOutputPresetIds.ToList(),
        };
    }
}
