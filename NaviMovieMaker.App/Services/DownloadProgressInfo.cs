namespace NaviMovieMaker.App.Services;

public sealed record DownloadProgressInfo(
    double? Percent,
    string Speed,
    string Eta,
    string Detail);
