using System.Globalization;
using System.Text.RegularExpressions;

namespace NaviMovieMaker.App.Services;

public sealed record ReplayGainNormalizationOptions(
    double TargetReplayGainVolumeDb = 89.0,
    double PeakLimitDb = -1.0,
    double ToleranceDb = 0.5,
    double MaximumGainDb = 20.0)
{
    public const double ReplayGainReferenceVolumeDb = 89.0;
    public const double MinimumTargetReplayGainVolumeDb = 80.0;
    public const double MaximumTargetReplayGainVolumeDb = 105.0;
    public const double TargetReplayGainVolumeStepDb = 0.1;
    public const double MinimumPeakLimitDb = -12.0;
    public const double MaximumPeakLimitDb = -0.1;
    public const double MinimumToleranceDb = 0.0;
    public const double MaximumToleranceDb = 3.0;
    public const double MinimumMaximumGainDb = 0.0;
    public const double MaximumMaximumGainDb = 30.0;
    public const double MaximumAttenuationDb = 30.0;

    public ReplayGainNormalizationOptions Normalize()
    {
        return new ReplayGainNormalizationOptions(
            RoundToStep(ClampFinite(
                TargetReplayGainVolumeDb,
                ReplayGainReferenceVolumeDb,
                MinimumTargetReplayGainVolumeDb,
                MaximumTargetReplayGainVolumeDb)),
            ClampFinite(PeakLimitDb, -1.0, MinimumPeakLimitDb, MaximumPeakLimitDb),
            ClampFinite(ToleranceDb, 0.5, MinimumToleranceDb, MaximumToleranceDb),
            ClampFinite(MaximumGainDb, 20.0, MinimumMaximumGainDb, MaximumMaximumGainDb));
    }

    private static double RoundToStep(double value)
    {
        return Math.Round(value / TargetReplayGainVolumeStepDb, MidpointRounding.AwayFromZero)
            * TargetReplayGainVolumeStepDb;
    }

    private static double ClampFinite(double value, double fallback, double minimum, double maximum)
    {
        return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }
}

public sealed record ReplayGainAnalysis(double TrackGainDb, double TrackPeak)
{
    public const double MaximumValidTrackPeak = 16.0;

    public bool IsValid => double.IsFinite(TrackGainDb)
        && double.IsFinite(TrackPeak)
        && TrackPeak > 0
        && TrackPeak <= MaximumValidTrackPeak;
}

public enum ReplayGainNormalizationAction
{
    Skip,
    VolumeOnly,
    VolumeAndLimiter,
}

public sealed record ReplayGainNormalizationDecision(
    ReplayGainNormalizationAction Action,
    double DetectedTrackVolumeDb,
    double TargetOffsetDb,
    double RequestedGainDb,
    double AppliedGainDb,
    double PredictedPeakLinear,
    double PredictedPeakDb,
    double LinearPeakLimit,
    bool GainWasLimited,
    string AudioFilter,
    string Reason);

public enum ReplayGainPreparationStatus
{
    Ready,
    Skipped,
    AnalysisFailed,
    Canceled,
}

public sealed record ReplayGainPreparationResult(
    ReplayGainPreparationStatus Status,
    ReplayGainAnalysisResult? AnalysisResult,
    ReplayGainNormalizationDecision? Decision)
{
    public string AudioFilter => Decision?.AudioFilter ?? string.Empty;
}

public static class ReplayGainParser
{
    public static ReplayGainAnalysis? Parse(string standardError)
    {
        var trackGainDb = ParseValue(standardError, "track_gain", requireDbSuffix: true);
        var trackPeak = ParseValue(standardError, "track_peak", requireDbSuffix: false);
        if (trackGainDb is null || trackPeak is null)
        {
            return null;
        }

        var analysis = new ReplayGainAnalysis(trackGainDb.Value, trackPeak.Value);
        return analysis.IsValid ? analysis : null;
    }

    private static double? ParseValue(string text, string fieldName, bool requireDbSuffix)
    {
        var suffix = requireDbSuffix ? @"\s*dB" : string.Empty;
        var match = Regex.Match(
            text,
            $@"{Regex.Escape(fieldName)}\s*(?:=|:)\s*(?<value>[+-]?(?:\d+(?:\.\d+)?|\.\d+)|[+-]?(?:nan|inf(?:inity)?)){suffix}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value;
        if (value.Contains("nan", StringComparison.OrdinalIgnoreCase))
        {
            return double.NaN;
        }

        if (value.Contains("inf", StringComparison.OrdinalIgnoreCase))
        {
            return value.StartsWith("-", StringComparison.Ordinal)
                ? double.NegativeInfinity
                : double.PositiveInfinity;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

public static class ReplayGainNormalizationCalculator
{
    public static ReplayGainNormalizationDecision Calculate(
        ReplayGainAnalysis analysis,
        ReplayGainNormalizationOptions options)
    {
        options = options.Normalize();
        if (!analysis.IsValid)
        {
            return CreateInvalidDecision(options, "ReplayGain解析値が不正なため正規化を省略します。");
        }

        var detectedTrackVolumeDb = ReplayGainNormalizationOptions.ReplayGainReferenceVolumeDb - analysis.TrackGainDb;
        var targetOffsetDb = options.TargetReplayGainVolumeDb - ReplayGainNormalizationOptions.ReplayGainReferenceVolumeDb;
        var requestedGainDb = analysis.TrackGainDb + targetOffsetDb;
        var limitLinear = DbfsToLinear(options.PeakLimitDb);

        if (Math.Abs(requestedGainDb) <= options.ToleranceDb)
        {
            return new ReplayGainNormalizationDecision(
                ReplayGainNormalizationAction.Skip,
                detectedTrackVolumeDb,
                targetOffsetDb,
                requestedGainDb,
                0,
                analysis.TrackPeak,
                LinearToDbfs(analysis.TrackPeak),
                limitLinear,
                false,
                string.Empty,
                "許容範囲内のため正規化を省略します。");
        }

        var appliedGainDb = Math.Clamp(
            requestedGainDb,
            -ReplayGainNormalizationOptions.MaximumAttenuationDb,
            options.MaximumGainDb);
        var gainLinear = DbfsToLinear(appliedGainDb);
        var predictedPeakLinear = analysis.TrackPeak * gainLinear;
        if (!double.IsFinite(predictedPeakLinear) || predictedPeakLinear <= 0)
        {
            return CreateInvalidDecision(options, "予測ピークを安全に計算できないため正規化を省略します。");
        }

        var predictedPeakDb = LinearToDbfs(predictedPeakLinear);
        var useLimiter = predictedPeakLinear > limitLinear;
        var audioFilter = $"volume={appliedGainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB";
        if (useLimiter)
        {
            audioFilter += $",alimiter=limit={limitLinear.ToString("0.######", CultureInfo.InvariantCulture)}:level=false:latency=true";
        }

        return new ReplayGainNormalizationDecision(
            useLimiter ? ReplayGainNormalizationAction.VolumeAndLimiter : ReplayGainNormalizationAction.VolumeOnly,
            detectedTrackVolumeDb,
            targetOffsetDb,
            requestedGainDb,
            appliedGainDb,
            predictedPeakLinear,
            predictedPeakDb,
            limitLinear,
            Math.Abs(appliedGainDb - requestedGainDb) > 0.000001,
            audioFilter,
            useLimiter
                ? "音量ゲインとピークリミッターを適用します。"
                : "音量ゲインを適用します。");
    }

    public static double DbfsToLinear(double dbfs)
    {
        return Math.Pow(10.0, dbfs / 20.0);
    }

    public static double LinearToDbfs(double linear)
    {
        return 20.0 * Math.Log10(linear);
    }

    private static ReplayGainNormalizationDecision CreateInvalidDecision(
        ReplayGainNormalizationOptions options,
        string reason)
    {
        return new ReplayGainNormalizationDecision(
            ReplayGainNormalizationAction.Skip,
            0,
            0,
            0,
            0,
            0,
            double.NegativeInfinity,
            DbfsToLinear(options.PeakLimitDb),
            false,
            string.Empty,
            reason);
    }
}

public static class PeakNormalizationFilterBuilder
{
    public static string BuildBoostOnly(double maxVolumeDb, double targetPeakDb)
    {
        var gainDb = targetPeakDb - maxVolumeDb;
        if (gainDb <= 0)
        {
            return string.Empty;
        }

        return $"volume={gainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB,alimiter=limit=0.98";
    }
}

public static class AudioNormalizationPolicy
{
    public static bool PerItemOverridesGlobal(AudioAdjustmentMode itemMode)
    {
        return itemMode == AudioAdjustmentMode.LoudnessNormalize;
    }
}

public sealed class ReplayGainNormalizationService
{
    public async Task<ReplayGainPreparationResult> PrepareAsync(
        Func<CancellationToken, Task<ReplayGainAnalysisResult>> analyzeAsync,
        ReplayGainNormalizationOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ReplayGainPreparationResult(ReplayGainPreparationStatus.Canceled, null, null);
        }

        ReplayGainAnalysisResult analysisResult;
        try
        {
            analysisResult = await analyzeAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new ReplayGainPreparationResult(ReplayGainPreparationStatus.Canceled, null, null);
        }

        if (cancellationToken.IsCancellationRequested || analysisResult.WasCanceled)
        {
            return new ReplayGainPreparationResult(ReplayGainPreparationStatus.Canceled, analysisResult, null);
        }

        if (!analysisResult.IsSuccess || analysisResult.Analysis is null)
        {
            return new ReplayGainPreparationResult(ReplayGainPreparationStatus.AnalysisFailed, analysisResult, null);
        }

        var decision = ReplayGainNormalizationCalculator.Calculate(analysisResult.Analysis, options);
        return new ReplayGainPreparationResult(
            decision.Action == ReplayGainNormalizationAction.Skip
                ? ReplayGainPreparationStatus.Skipped
                : ReplayGainPreparationStatus.Ready,
            analysisResult,
            decision);
    }
}
