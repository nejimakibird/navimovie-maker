namespace NaviMovieMaker.App.Services;

public sealed record ConversionPreset(
    string Id,
    string DisplayName,
    string ContainerExtension,
    string VideoCodec,
    string AudioCodec,
    int Width,
    int Height,
    int? FrameRate,
    int VideoBitrateKbps,
    int MaxRateKbps,
    int BufSizeKbps,
    int AudioBitrateKbps,
    string? VideoProfile,
    string? VideoLevel,
    bool EnableFastStart,
    string? FormatName,
    bool SetDisplayAspectRatio,
    bool SupportsAspectMode,
    bool IsAudioOnlyPreset = false);
