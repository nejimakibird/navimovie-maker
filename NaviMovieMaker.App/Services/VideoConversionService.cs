using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NaviMovieMaker.App.Services;

public sealed class VideoConversionService
{
    public string FfmpegPath { get; set; } = "ffmpeg";

    public async Task<VideoConversionResult> ConvertAsync(
        string inputFilePath,
        string outputFilePath,
        Action<string> log,
        ConversionPreset? preset = null,
        string aspectMode = "Keep aspect ratio + padding",
        string audioFilter = "",
        Action<FfmpegProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        preset ??= ConversionPresetCatalog.GetDefault();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(inputFilePath);
        AddProgressArguments(process.StartInfo.ArgumentList);
        var videoFilter = BuildVideoFilter(preset, aspectMode);
        AddPresetArguments(process.StartInfo.ArgumentList, preset, videoFilter, audioFilter);
        process.StartInfo.ArgumentList.Add(outputFilePath);

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            process.Start();
            log($"ffmpeg input: {inputFilePath}");
            log($"ffmpeg output: {outputFilePath}");
            log($"ffmpeg preset: {preset.DisplayName}");
            log($"ffmpeg video filter: {videoFilter}");
            log(!string.IsNullOrWhiteSpace(audioFilter)
                ? $"ffmpeg audio filter: {audioFilter}"
                : "ffmpeg audio adjustment: disabled");

            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            var progressState = new FfmpegProgressState(progress);
            var outputTask = ReadProgressStreamAsync(process.StandardOutput, standardOutput, progressState, cancellationToken);
            var errorTask = ReadLogStreamAsync(process.StandardError, standardError, log, progressState, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            return new VideoConversionResult(
                process.ExitCode == 0,
                outputFilePath,
                standardOutput.ToString(),
                standardError.ToString(),
                process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                "Conversion was canceled.",
                null);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                $"ffmpeg was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    public async Task<AudioPeakAnalysisResult> AnalyzeMaxVolumeAsync(
        string inputFilePath,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(inputFilePath);
        process.StartInfo.ArgumentList.Add("-af");
        process.StartInfo.ArgumentList.Add("volumedetect");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("null");
        process.StartInfo.ArgumentList.Add("NUL");

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            process.Start();
            log($"ffmpeg audio peak analysis input: {inputFilePath}");

            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            var outputTask = ReadStreamAsync(process.StandardOutput, standardOutput, log, cancellationToken);
            var errorTask = ReadStreamAsync(process.StandardError, standardError, log, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            var maxVolumeDb = ParseMaxVolume(standardError.ToString());
            return new AudioPeakAnalysisResult(
                process.ExitCode == 0 && maxVolumeDb is not null,
                maxVolumeDb,
                standardOutput.ToString(),
                standardError.ToString(),
                process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new AudioPeakAnalysisResult(false, null, standardOutput.ToString(), "Audio peak analysis was canceled.", null);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new AudioPeakAnalysisResult(
                false,
                null,
                standardOutput.ToString(),
                $"ffmpeg was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    public async Task<VideoConversionResult> ConvertAudioOnlyMp4Async(
        string inputFilePath,
        string outputFilePath,
        Action<string> log,
        string audioFilter = "",
        Action<FfmpegProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(inputFilePath);
        AddProgressArguments(process.StartInfo.ArgumentList);
        process.StartInfo.ArgumentList.Add("-vn");
        process.StartInfo.ArgumentList.Add("-c:a");
        process.StartInfo.ArgumentList.Add("aac");
        process.StartInfo.ArgumentList.Add("-b:a");
        process.StartInfo.ArgumentList.Add("256k");
        process.StartInfo.ArgumentList.Add("-ar");
        process.StartInfo.ArgumentList.Add("48000");
        process.StartInfo.ArgumentList.Add("-ac");
        process.StartInfo.ArgumentList.Add("2");

        if (!string.IsNullOrWhiteSpace(audioFilter))
        {
            process.StartInfo.ArgumentList.Add("-af");
            process.StartInfo.ArgumentList.Add(audioFilter);
        }

        process.StartInfo.ArgumentList.Add("-movflags");
        process.StartInfo.ArgumentList.Add("+faststart");
        process.StartInfo.ArgumentList.Add(outputFilePath);

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            process.Start();
            log($"ffmpeg input: {inputFilePath}");
            log($"ffmpeg output: {outputFilePath}");
            log("ffmpeg preset: Audio-only MP4 AAC");
            log(!string.IsNullOrWhiteSpace(audioFilter)
                ? $"ffmpeg audio filter: {audioFilter}"
                : "ffmpeg audio adjustment: disabled");

            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            var progressState = new FfmpegProgressState(progress);
            var outputTask = ReadProgressStreamAsync(process.StandardOutput, standardOutput, progressState, cancellationToken);
            var errorTask = ReadLogStreamAsync(process.StandardError, standardError, log, progressState, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            return new VideoConversionResult(
                process.ExitCode == 0,
                outputFilePath,
                standardOutput.ToString(),
                standardError.ToString(),
                process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                "Conversion was canceled.",
                null);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                $"ffmpeg was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    public async Task<VideoConversionResult> ConvertAudioPresetAsync(
        string inputFilePath,
        string outputFilePath,
        Action<string> log,
        ConversionPreset preset,
        string audioFilter = "",
        Action<FfmpegProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(inputFilePath);
        AddProgressArguments(process.StartInfo.ArgumentList);
        AddAudioPresetArguments(process.StartInfo.ArgumentList, preset, audioFilter);
        process.StartInfo.ArgumentList.Add(outputFilePath);

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            process.Start();
            log($"ffmpeg input: {inputFilePath}");
            log($"ffmpeg output: {outputFilePath}");
            log($"ffmpeg preset: {preset.DisplayName}");
            log("ffmpeg video output: disabled (-vn)");
            log(!string.IsNullOrWhiteSpace(audioFilter)
                ? $"ffmpeg audio filter: {audioFilter}"
                : "ffmpeg audio adjustment: disabled");

            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            var progressState = new FfmpegProgressState(progress);
            var outputTask = ReadProgressStreamAsync(process.StandardOutput, standardOutput, progressState, cancellationToken);
            var errorTask = ReadLogStreamAsync(process.StandardError, standardError, log, progressState, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            return new VideoConversionResult(
                process.ExitCode == 0,
                outputFilePath,
                standardOutput.ToString(),
                standardError.ToString(),
                process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                "Conversion was canceled.",
                null);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new VideoConversionResult(
                false,
                outputFilePath,
                standardOutput.ToString(),
                $"ffmpeg was not found in PATH. Install it or configure the path later. {ex.Message}",
                null);
        }
    }

    private static void AddAudioPresetArguments(
        IList<string> arguments,
        ConversionPreset preset,
        string audioFilter)
    {
        arguments.Add("-vn");

        switch (preset.Id)
        {
            case "audio-mp3-high":
            case "audio-mp3-medium":
            case "audio-mp3-low":
                arguments.Add("-c:a");
                arguments.Add("libmp3lame");
                arguments.Add("-b:a");
                arguments.Add($"{preset.AudioBitrateKbps}k");
                break;
            case "audio-mp4-aac-only-high":
            case "audio-mp4-aac-only-medium":
            case "audio-mp4-aac-only-low":
            case "audio-m4a-aac-high":
            case "audio-m4a-aac-medium":
            case "audio-m4a-aac-low":
                arguments.Add("-c:a");
                arguments.Add("aac");
                arguments.Add("-b:a");
                arguments.Add($"{preset.AudioBitrateKbps}k");
                break;
            case "audio-wav-pcm-16bit":
                arguments.Add("-c:a");
                arguments.Add("pcm_s16le");
                break;
            case "audio-flac-lossless":
                arguments.Add("-c:a");
                arguments.Add("flac");
                break;
            case "audio-ogg-high":
            case "audio-ogg-medium":
            case "audio-ogg-low":
                arguments.Add("-c:a");
                arguments.Add("libvorbis");
                arguments.Add("-q:a");
                arguments.Add(preset.AudioBitrateKbps.ToString(CultureInfo.InvariantCulture));
                break;
            case "audio-wma-high":
            case "audio-wma-medium":
            case "audio-wma-low":
                arguments.Add("-c:a");
                arguments.Add("wmav2");
                arguments.Add("-b:a");
                arguments.Add($"{preset.AudioBitrateKbps}k");
                break;
            default:
                arguments.Add("-c:a");
                arguments.Add("aac");
                arguments.Add("-b:a");
                arguments.Add($"{preset.AudioBitrateKbps}k");
                break;
        }

        arguments.Add("-ar");
        arguments.Add("48000");
        arguments.Add("-ac");
        arguments.Add("2");

        if (!string.IsNullOrWhiteSpace(audioFilter))
        {
            arguments.Add("-af");
            arguments.Add(audioFilter);
        }

        if (preset.EnableFastStart)
        {
            arguments.Add("-movflags");
            arguments.Add("+faststart");
        }
    }

    private static void AddPresetArguments(
        IList<string> arguments,
        ConversionPreset preset,
        string videoFilter,
        string audioFilter)
    {
        arguments.Add("-vf");
        arguments.Add(videoFilter);
        arguments.Add("-c:v");
        arguments.Add(preset.VideoCodec);

        if (!string.IsNullOrWhiteSpace(preset.VideoProfile))
        {
            arguments.Add("-profile:v");
            arguments.Add(preset.VideoProfile);
        }

        if (!string.IsNullOrWhiteSpace(preset.VideoLevel))
        {
            arguments.Add("-level:v");
            arguments.Add(preset.VideoLevel);
        }

        arguments.Add("-b:v");
        arguments.Add($"{preset.VideoBitrateKbps}k");
        arguments.Add("-maxrate");
        arguments.Add($"{preset.MaxRateKbps}k");
        arguments.Add("-bufsize");
        arguments.Add($"{preset.BufSizeKbps}k");
        arguments.Add("-pix_fmt");
        arguments.Add("yuv420p");
        arguments.Add("-c:a");
        arguments.Add(preset.AudioCodec);
        arguments.Add("-b:a");
        arguments.Add($"{preset.AudioBitrateKbps}k");
        arguments.Add("-ar");
        arguments.Add("48000");
        arguments.Add("-ac");
        arguments.Add("2");

        if (!string.IsNullOrWhiteSpace(audioFilter))
        {
            arguments.Add("-af");
            arguments.Add(audioFilter);
        }

        if (preset.EnableFastStart)
        {
            arguments.Add("-movflags");
            arguments.Add("+faststart");
        }

        if (!string.IsNullOrWhiteSpace(preset.FormatName))
        {
            arguments.Add("-f");
            arguments.Add(preset.FormatName);
        }
    }

    private static void AddProgressArguments(IList<string> arguments)
    {
        arguments.Add("-progress");
        arguments.Add("pipe:1");
    }

    private static string BuildVideoFilter(ConversionPreset preset, string aspectMode)
    {
        if (preset.SupportsAspectMode)
        {
            return aspectMode.Equals("Stretch to fit", StringComparison.OrdinalIgnoreCase)
                ? $"yadif=1,scale={preset.Width}:{preset.Height},fps={preset.FrameRate ?? 30}"
                : $"yadif=1,scale={preset.Width}:{preset.Height}:force_original_aspect_ratio=decrease,pad={preset.Width}:{preset.Height}:(ow-iw)/2:(oh-ih)/2,fps={preset.FrameRate ?? 30}";
        }

        var filter = $"yadif=1,scale={preset.Width}:{preset.Height}";
        if (preset.FrameRate is not null)
        {
            filter += $",fps={preset.FrameRate}";
        }

        if (preset.SetDisplayAspectRatio)
        {
            filter += ",setdar=16/9";
        }

        return filter;
    }

    private static double? ParseMaxVolume(string standardError)
    {
        var match = Regex.Match(
            standardError,
            @"max_volume:\s*(?<value>-?\d+(?:\.\d+)?)\s*dB",
            RegexOptions.IgnoreCase);
        return match.Success
            && double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var maxVolumeDb)
            ? maxVolumeDb
            : null;
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder output,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            output.AppendLine(line);
            log(line);
        }
    }

    private static async Task ReadLogStreamAsync(
        StreamReader reader,
        StringBuilder output,
        Action<string> log,
        FfmpegProgressState progressState,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            output.AppendLine(line);
            log(line);
            progressState.UpdateFromLogLine(line);
        }
    }

    private static async Task ReadProgressStreamAsync(
        StreamReader reader,
        StringBuilder output,
        FfmpegProgressState progressState,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            output.AppendLine(line);
            progressState.UpdateFromProgressLine(line);
        }
    }

    private sealed class FfmpegProgressState(Action<FfmpegProgressInfo>? progress)
    {
        private readonly object _gate = new();
        private TimeSpan? _convertedTime;
        private TimeSpan? _totalDuration;
        private string _speed = string.Empty;

        public void UpdateFromLogLine(string line)
        {
            var match = Regex.Match(line, @"Duration:\s*(?<duration>\d{1,2}:\d{2}:\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase);
            var shouldPublish = false;
            if (match.Success && TryParseFfmpegTime(match.Groups["duration"].Value, out var duration))
            {
                lock (_gate)
                {
                    _totalDuration = duration;
                }

                shouldPublish = true;
            }

            var timeMatch = Regex.Match(line, @"time=(?<time>\d{1,2}:\d{2}:\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (timeMatch.Success && TryParseFfmpegTime(timeMatch.Groups["time"].Value, out var convertedTime))
            {
                lock (_gate)
                {
                    _convertedTime = convertedTime;
                }

                shouldPublish = true;
            }

            var speedMatch = Regex.Match(line, @"speed=\s*(?<speed>\S+)", RegexOptions.IgnoreCase);
            if (speedMatch.Success)
            {
                lock (_gate)
                {
                    _speed = speedMatch.Groups["speed"].Value.Trim();
                }

                shouldPublish = true;
            }

            if (shouldPublish)
            {
                Publish(isComplete: false);
            }
        }

        public void UpdateFromProgressLine(string line)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..].Trim();
            var shouldPublish = false;
            var isComplete = false;

            lock (_gate)
            {
                switch (key)
                {
                    case "out_time_ms":
                    case "out_time_us":
                        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
                        {
                            _convertedTime = TimeSpan.FromTicks(microseconds * 10);
                            shouldPublish = true;
                        }

                        break;
                    case "out_time":
                        if (TryParseFfmpegTime(value, out var outTime))
                        {
                            _convertedTime = outTime;
                            shouldPublish = true;
                        }

                        break;
                    case "speed":
                        _speed = value;
                        shouldPublish = true;
                        break;
                    case "progress":
                        shouldPublish = true;
                        isComplete = value.Equals("end", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            if (shouldPublish)
            {
                Publish(isComplete);
            }
        }

        private void Publish(bool isComplete)
        {
            if (progress is null)
            {
                return;
            }

            TimeSpan? convertedTime;
            TimeSpan? totalDuration;
            string speed;
            lock (_gate)
            {
                convertedTime = _convertedTime;
                totalDuration = _totalDuration;
                speed = _speed;
            }

            progress(new FfmpegProgressInfo(convertedTime, totalDuration, speed, isComplete));
        }

        private static bool TryParseFfmpegTime(string text, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(
                text,
                [@"h\:mm\:ss\.FFFFFF", @"h\:mm\:ss", @"hh\:mm\:ss\.FFFFFF", @"hh\:mm\:ss"],
                CultureInfo.InvariantCulture,
                out time);
        }
    }
}

public sealed record AudioPeakAnalysisResult(
    bool IsSuccess,
    double? MaxVolumeDb,
    string StandardOutput,
    string StandardError,
    int? ExitCode);
