namespace NaviMovieMaker.App.Services;

public sealed record DownloadProfileOption(string Id, string DisplayName, string? FormatExpression);

public static class DownloadProfileCatalog
{
    public const string AutoId = "auto";
    public const string BestId = "best";
    public const string VideoAudio720pId = "video-audio-720p";
    public const string VideoAudio480pId = "video-audio-480p";
    public const string Mp4VideoAudio720pId = "mp4-video-audio-720p";
    public const string Mp4VideoAudio480pId = "mp4-video-audio-480p";
    public const string AudioOnlyId = "audio-only";

    private static readonly DownloadProfileOption[] Profiles =
    [
        new(AutoId, "自動", null),
        new(BestId, "最高品質", "bv*+ba/b"),
        new(VideoAudio720pId, "動画＋音声 720p", "bv*[height<=720]+ba/b[height<=720]"),
        new(VideoAudio480pId, "動画＋音声 480p", "bv*[height<=480]+ba/b[height<=480]"),
        new(Mp4VideoAudio720pId, "MP4動画＋音声 720p", "bv*[height<=720][ext=mp4]+ba[ext=m4a]/b[height<=720][ext=mp4]/b[height<=720]"),
        new(Mp4VideoAudio480pId, "MP4動画＋音声 480p", "bv*[height<=480][ext=mp4]+ba[ext=m4a]/b[height<=480][ext=mp4]/b[height<=480]"),
        new(AudioOnlyId, "音声のみ", "ba/bestaudio"),
    ];

    public static IReadOnlyList<DownloadProfileOption> GetProfiles()
    {
        return Profiles;
    }

    public static DownloadProfileOption GetProfile(string? id)
    {
        return Profiles.FirstOrDefault(
            profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];
    }

    public static DownloadProfileOption ResolveAuto(string runMode, ConversionPreset? selectedPreset)
    {
        if (runMode == "Download Only")
        {
            return GetProfile(Mp4VideoAudio720pId);
        }

        if (runMode == "Download & Convert" && selectedPreset?.IsAudioOnlyPreset == true)
        {
            return GetProfile(AudioOnlyId);
        }

        return GetProfile(VideoAudio720pId);
    }

    public static bool IsKnownProfile(string? id)
    {
        return Profiles.Any(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
