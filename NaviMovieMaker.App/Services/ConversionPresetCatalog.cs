namespace NaviMovieMaker.App.Services;

public static class ConversionPresetCatalog
{
    public const string CurrentCompatibilityId = "car-navi-current";
    public const string AudioMp4AacOnlyHighId = "audio-mp4-aac-only-high";

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
            CreateCarNaviMp4("car-navi-standard", "Car Navi MP4 - Standard", 720, 480, 4000, 5000, 8000),
            CreateCarNaviMp4("car-navi-high", "Car Navi MP4 - High Quality", 720, 480, 5000, 6000, 10000),
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
            "car-navi-standard",
            "car-navi-small",
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
