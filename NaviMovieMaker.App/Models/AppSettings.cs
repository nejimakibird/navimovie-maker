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

    public double TargetPeakDb { get; set; } = -1.0;

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
            TargetPeakDb = TargetPeakDb,
            VisibleOutputPresetIds = VisibleOutputPresetIds.ToList(),
        };
    }
}
