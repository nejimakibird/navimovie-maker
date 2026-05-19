namespace NaviMovieMaker.App.Services;

public static class ConversionPresetCatalog
{
    public const string CurrentCompatibilityId = "car-navi-current";
    public const string CarNaviStandardId = "car-navi-standard";
    public const string AudioMp4AacOnlyHighId = "audio-mp4-aac-only-high";
    public const string IpadTabletMp41080pStandardId = "ipad-tablet-mp4-1080p-standard";
    public const string IpadTabletMp4720pCompatibleId = "ipad-tablet-mp4-720p-compatible";
    public const string IpadTabletHevc1080pHighCompressionId = "ipad-tablet-hevc-1080p-high-compression";
    public const string AndroidTabletMp41080pStandardId = "android-tablet-mp4-1080p-standard";
    public const string AndroidTabletMp4720pCompatibleId = "android-tablet-mp4-720p-compatible";
    public const string AndroidTabletHevc1080pHighCompressionId = "android-tablet-hevc-1080p-high-compression";

    public static IReadOnlyList<ConversionPreset> GetPresets()
    {
        return
        [
            // TODO: If 720x480 Car Navi MP4 presets are confirmed on the real device,
            // consider changing the default and deprecating Current Compatibility.
            new ConversionPreset(
                CurrentCompatibilityId,
                "Car Navi MP4 - Current Compatibility",
                ".mp4",
                "libx264",
                "aac",
                720,
                406,
                null,
                4000,
                4000,
                8000,
                256,
                "main",
                "3.0",
                true,
                null,
                true,
                false),
            CreateCarNaviMp4("car-navi-small", "Car Navi MP4 - Small Size", 640, 360, 2000, 3000, 4000),
            CreateCarNaviMp4(CarNaviStandardId, "Car Navi MP4 - Standard", 720, 480, 4000, 5000, 8000),
            CreateCarNaviMp4("car-navi-high", "Car Navi MP4 - High Quality", 720, 480, 5000, 6000, 10000),
            CreateTabletMp4(IpadTabletMp41080pStandardId, "iPad / タブレット MP4 1080p 標準", 1920, 1080, 6000, 8000, 12000, 192, "high", "4.1"),
            CreateTabletMp4(IpadTabletMp4720pCompatibleId, "iPad / タブレット MP4 720p 互換", 1280, 720, 3000, 4000, 8000, 128, "main", "3.1"),
            CreateTabletHevc(IpadTabletHevc1080pHighCompressionId, "iPad / タブレット HEVC 1080p 高圧縮", 1920, 1080, 3500, 4500, 9000, 160),
            CreateTabletMp4(AndroidTabletMp41080pStandardId, "Androidタブレット MP4 1080p 標準", 1920, 1080, 6000, 8000, 12000, 192, "high", "4.1"),
            CreateTabletMp4(AndroidTabletMp4720pCompatibleId, "Androidタブレット MP4 720p 互換", 1280, 720, 3000, 4000, 8000, 128, "main", "3.1"),
            CreateTabletHevc(AndroidTabletHevc1080pHighCompressionId, "Androidタブレット HEVC 1080p 高圧縮", 1920, 1080, 3500, 4500, 9000, 160),
            CreatePortableDvdMpg("dvd-small", "Portable DVD Player MPG - Small Size (MP2 audio)", 4000, 5000),
            CreatePortableDvdMpg("dvd-standard", "Portable DVD Player MPG - Standard (MP2 audio)", 5000, 6000),
            CreatePortableDvdMpg("dvd-high", "Portable DVD Player MPG - High Quality (MP2 audio)", 6000, 7000),
            CreateAudioPreset(AudioMp4AacOnlyHighId, "Audio MP4 AAC Only - High (256 kbps)", ".mp4", "aac", 256, true),
            CreateAudioPreset("audio-mp4-aac-only-medium", "Audio MP4 AAC Only - Medium (192 kbps)", ".mp4", "aac", 192, true),
            CreateAudioPreset("audio-mp4-aac-only-low", "Audio MP4 AAC Only - Low (128 kbps)", ".mp4", "aac", 128, true),
            CreateAudioPreset("audio-mp3-high", "MP3 - High (320 kbps)", ".mp3", "libmp3lame", 320),
            CreateAudioPreset("audio-mp3-medium", "MP3 - Medium (192 kbps)", ".mp3", "libmp3lame", 192),
            CreateAudioPreset("audio-mp3-low", "MP3 - Low (128 kbps)", ".mp3", "libmp3lame", 128),
            CreateAudioPreset("audio-m4a-aac-high", "M4A AAC - High (256 kbps)", ".m4a", "aac", 256),
            CreateAudioPreset("audio-m4a-aac-medium", "M4A AAC - Medium (192 kbps)", ".m4a", "aac", 192),
            CreateAudioPreset("audio-m4a-aac-low", "M4A AAC - Low (128 kbps)", ".m4a", "aac", 128),
            CreateAudioPreset("audio-wav-pcm-16bit", "WAV PCM 16bit", ".wav", "pcm_s16le", 0),
            CreateAudioPreset("audio-flac-lossless", "FLAC Lossless", ".flac", "flac", 0),
            CreateAudioPreset("audio-ogg-high", "OGG Vorbis - High", ".ogg", "libvorbis", 7),
            CreateAudioPreset("audio-ogg-medium", "OGG Vorbis - Medium", ".ogg", "libvorbis", 5),
            CreateAudioPreset("audio-ogg-low", "OGG Vorbis - Low", ".ogg", "libvorbis", 3),
            CreateAudioPreset("audio-wma-high", "WMA - High (256 kbps)", ".wma", "wmav2", 256),
            CreateAudioPreset("audio-wma-medium", "WMA - Medium (192 kbps)", ".wma", "wmav2", 192),
            CreateAudioPreset("audio-wma-low", "WMA - Low (128 kbps)", ".wma", "wmav2", 128),
        ];
    }

    public static IReadOnlyList<string> GetDefaultVisiblePresetIds()
    {
        return
        [
            CurrentCompatibilityId,
            CarNaviStandardId,
            "car-navi-small",
            IpadTabletMp41080pStandardId,
            IpadTabletMp4720pCompatibleId,
            IpadTabletHevc1080pHighCompressionId,
            AndroidTabletMp41080pStandardId,
            AndroidTabletMp4720pCompatibleId,
            AndroidTabletHevc1080pHighCompressionId,
            "dvd-standard",
            AudioMp4AacOnlyHighId,
            "audio-mp3-high",
            "audio-mp3-medium",
            "audio-m4a-aac-high",
        ];
    }

    public static ConversionPreset GetDefault()
    {
        return GetPresets()[0];
    }

    public static IReadOnlyList<string> GetTabletPresetIds()
    {
        return
        [
            IpadTabletMp41080pStandardId,
            IpadTabletMp4720pCompatibleId,
            IpadTabletHevc1080pHighCompressionId,
            AndroidTabletMp41080pStandardId,
            AndroidTabletMp4720pCompatibleId,
            AndroidTabletHevc1080pHighCompressionId,
        ];
    }

    private static ConversionPreset CreateCarNaviMp4(
        string id,
        string displayName,
        int width,
        int height,
        int videoBitrateKbps,
        int maxRateKbps,
        int bufSizeKbps)
    {
        return new ConversionPreset(
            id,
            displayName,
            ".mp4",
            "libx264",
            "aac",
            width,
            height,
            30,
            videoBitrateKbps,
            maxRateKbps,
            bufSizeKbps,
            128,
            "main",
            "3.0",
            true,
            null,
            false,
            true);
    }

    private static ConversionPreset CreatePortableDvdMpg(
        string id,
        string displayName,
        int videoBitrateKbps,
        int maxRateKbps)
    {
        return new ConversionPreset(
            id,
            displayName,
            ".mpg",
            "mpeg2video",
            "mp2",
            720,
            480,
            30,
            videoBitrateKbps,
            maxRateKbps,
            1835,
            192,
            null,
            null,
            false,
            "mpeg",
            false,
            true);
    }

    private static ConversionPreset CreateTabletMp4(
        string id,
        string displayName,
        int width,
        int height,
        int videoBitrateKbps,
        int maxRateKbps,
        int bufSizeKbps,
        int audioBitrateKbps,
        string videoProfile,
        string videoLevel)
    {
        return new ConversionPreset(
            id,
            displayName,
            ".mp4",
            "libx264",
            "aac",
            width,
            height,
            30,
            videoBitrateKbps,
            maxRateKbps,
            bufSizeKbps,
            audioBitrateKbps,
            videoProfile,
            videoLevel,
            true,
            null,
            false,
            true);
    }

    private static ConversionPreset CreateTabletHevc(
        string id,
        string displayName,
        int width,
        int height,
        int videoBitrateKbps,
        int maxRateKbps,
        int bufSizeKbps,
        int audioBitrateKbps)
    {
        return new ConversionPreset(
            id,
            displayName,
            ".mp4",
            "libx265",
            "aac",
            width,
            height,
            30,
            videoBitrateKbps,
            maxRateKbps,
            bufSizeKbps,
            audioBitrateKbps,
            "main",
            null,
            true,
            null,
            false,
            true);
    }

    private static ConversionPreset CreateAudioPreset(
        string id,
        string displayName,
        string extension,
        string audioCodec,
        int audioBitrateOrQuality,
        bool enableFastStart = false)
    {
        return new ConversionPreset(
            id,
            displayName,
            extension,
            string.Empty,
            audioCodec,
            0,
            0,
            null,
            0,
            0,
            0,
            audioBitrateOrQuality,
            null,
            null,
            enableFastStart,
            null,
            false,
            false,
            true);
    }
}
