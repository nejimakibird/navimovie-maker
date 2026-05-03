namespace NaviMovieMaker.App.Services;

public sealed record VideoConversionResult(
    bool IsSuccess,
    string OutputFilePath,
    string StandardOutput,
    string StandardError,
    int? ExitCode);
