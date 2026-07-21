namespace NaviMovieMaker.App;

public sealed class ConversionPlaylist
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string AppVersion { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string OutputFolder { get; set; } = string.Empty;

    public string OutputPresetId { get; set; } = string.Empty;

    public bool SimpleModeEnabled { get; set; }

    public string OperationMode { get; set; } = string.Empty;

    public string AspectMode { get; set; } = string.Empty;

    public bool KeepOriginalDownloadedFiles { get; set; }

    public bool PeakBoost { get; set; }

    public AudioNormalizationMode AudioNormalizationMode { get; set; } = AudioNormalizationMode.Peak;

    public double TargetPeakDb { get; set; } = -1.0;

    public double TargetReplayGainVolumeDb { get; set; } = 89.0;

    public double PeakLimitDb { get; set; } = -1.0;

    public double NormalizationToleranceDb { get; set; } = 0.5;

    public double MaximumNormalizationGainDb { get; set; } = 20.0;

    public int? NumberPrefixStart { get; set; }

    public List<ConversionPlaylistItem> Items { get; set; } = [];
}

public sealed class ConversionPlaylistItem
{
    public string ItemId { get; set; } = string.Empty;

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

    public PlaylistResultRecord? Result { get; set; }
}

public sealed class PlaylistResultRecord
{
    public PlaylistSourceIdentity SourceIdentity { get; set; } = new();
    public string OperationMode { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ResultFilePath { get; set; } = string.Empty;
    public string ResultFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public int? SequenceNumber { get; set; }
}

public sealed class PlaylistSourceIdentity
{
    public string Kind { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public DateTime? LastWriteTimeUtc { get; set; }
}

public enum PlaylistResultState
{
    Unprocessed,
    Available,
    SequenceOutOfSync,
    Missing,
    Modified,
    NeedsReprocess,
    NameConflict,
}
