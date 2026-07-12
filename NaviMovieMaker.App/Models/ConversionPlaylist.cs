namespace NaviMovieMaker.App;

public sealed class ConversionPlaylist
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string AppVersion { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string OutputPresetId { get; set; } = string.Empty;

    public bool SimpleModeEnabled { get; set; }

    public string OperationMode { get; set; } = string.Empty;

    public string AspectMode { get; set; } = string.Empty;

    public bool KeepOriginalDownloadedFiles { get; set; }

    public bool PeakBoost { get; set; }

    public double TargetPeakDb { get; set; } = -1.0;

    public int? NumberPrefixStart { get; set; }

    public List<ConversionPlaylistItem> Items { get; set; } = [];
}

public sealed class ConversionPlaylistItem
{
    public string SourceKind { get; set; } = string.Empty;

    public string SourcePathOrUrl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OutputBaseName { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public string LocalFilePath { get; set; } = string.Empty;

    public string? PresetId { get; set; }

    public string? Notes { get; set; }

    public bool IsSimpleModeItem { get; set; }

    public AudioAdjustmentMode AudioAdjustmentMode { get; set; }
}
