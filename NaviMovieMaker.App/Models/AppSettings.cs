namespace NaviMovieMaker.App;

public sealed class AppSettings
{
    public string WorkingFolder { get; set; } = string.Empty;

    public string TemporaryFolder { get; set; } = string.Empty;

    public string ConvertedFolder { get; set; } = string.Empty;

    public string LocalVideoFolder { get; set; } = string.Empty;

    public bool CreateSubfolderPerOutputPreset { get; set; } = true;

    public string DownloadProfile { get; set; } = Services.DownloadProfileCatalog.AutoId;

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
            VisibleOutputPresetIds = VisibleOutputPresetIds.ToList(),
        };
    }
}
