using System.Windows;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using NaviMovieMaker.App.Services;

namespace NaviMovieMaker.App;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand NewPlaylistCommand = new("新規プレイリスト", nameof(NewPlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand OpenPlaylistCommand = new("プレイリストを開く", nameof(OpenPlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand SavePlaylistCommand = new("プレイリストを保存", nameof(SavePlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand SavePlaylistAsCommand = new("名前を付けて保存", nameof(SavePlaylistAsCommand), typeof(MainWindow));
    public static readonly RoutedUICommand PlaySourcePlaylistCommand = new("元のプレイリストを再生", nameof(PlaySourcePlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand PlayConvertedPlaylistCommand = new("変換済みプレイリストを再生", nameof(PlayConvertedPlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand PlayPlaylistCommand = new("プレイリストを再生", nameof(PlayPlaylistCommand), typeof(MainWindow));
    public static readonly RoutedUICommand RefreshOutputStateCommand = new("出力状態を更新", nameof(RefreshOutputStateCommand), typeof(MainWindow));
    public static readonly RoutedUICommand SynchronizeOutputSequenceCommand = new("出力ファイルの連番を同期", nameof(SynchronizeOutputSequenceCommand), typeof(MainWindow));
    private const int MaxLogEntryCount = 3000;
    private const int MaxDownloadAttempts = 3;
    private const string YtDlpInstallCommand = "winget install yt-dlp.yt-dlp";
    private const string FfmpegSearchCommand = "winget search ffmpeg";
    private const string FfmpegInstallCommand = "winget install Gyan.FFmpeg";
    private const string YtDlpUpdateCommand = "winget upgrade yt-dlp.yt-dlp";
    private const string FfmpegUpdateCommand = "winget upgrade Gyan.FFmpeg";
    private const string QueueReorderDragFormat = "NaviMovieMaker.QueueReorder";
    private const string AudioOnlyPresetFolderName = "Audio_MP4_AAC_Only";
    private const string QueueStatusReady = "待機中";
    private const string QueueStatusMetadataLoading = "情報取得中...";
    private const string QueueStatusReadyWithWarning = "注意: 動画情報を取得できませんでした。処理時に再試行します。";
    private const string QueueStatusUnsupported = "対象外";
    private const string UnsupportedCurrentModeReason = "現在のモードではこの項目は処理対象外です。";
    private const string UnsupportedUrlModeReason = "URLはこのモードでは処理対象外です。";
    private const string UnsupportedLocalFileModeReason = "ローカルファイルはこのモードでは処理対象外です。";
    private const string UnsupportedFileFormatReason = "対応していないファイル形式です。";
    private const string MissingLocalFileReason = "保存時に指定されていたローカルファイルが見つかりません。";
    private const string UnsafeOnlineUrlReason = "このURLはチャンネルまたはプレイリストの可能性があります。通常モードでは単体動画URLを指定してください。";
    private static readonly HttpClient ThumbnailHttpClient = new();
    private string? _lastQueueSortMemberPath;
    private ListSortDirection _lastQueueSortDirection = ListSortDirection.Ascending;
    private static readonly TimeSpan[] DownloadRetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
    ];

    private readonly ExternalToolService _externalToolService = new();
    private readonly VideoMetadataService _videoMetadataService = new();
    private readonly VideoDownloadService _videoDownloadService = new();
    private readonly VideoConversionService _videoConversionService = new();
    private readonly SettingsService _settingsService = new();
    private readonly ConversionPlaylistService _playlistService = new();
    private readonly PlaybackPlaylistBuilder _playbackPlaylistBuilder = new();
    private readonly MpvPlaybackService _mpvPlaybackService = new();
    private readonly MpvExecutableResolver _mpvExecutableResolver;
    private readonly PlaylistResultService _playlistResultService = new();
    private readonly ReplayGainNormalizationService _replayGainNormalizationService = new();
    private readonly AppLog _log = new();
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly ObservableCollection<VideoListItem> _videos = new();
    private readonly ObservableCollection<ConversionQueueItem> _conversionQueue = new();
    private readonly Dictionary<string, ImageSource?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings;
    private string _sessionOutputFolder = string.Empty;
    private ExternalToolResult? _lastYtDlpResult;
    private ExternalToolResult? _lastFfmpegResult;
    private ExternalToolResult? _lastFfprobeResult;
    private CancellationTokenSource? _downloadCancellationTokenSource;
    private CancellationTokenSource? _queueCancellationTokenSource;
    private Point? _queueDragStartPoint;
    private List<ConversionQueueItem> _draggedQueueItems = [];
    private int _queueProgressProcessed;
    private int _queueProgressTotal;
    private double _queueProgressValue;
    private IReadOnlyCollection<ConversionQueueItem> _activeQueueProgressItems = [];
    private int? _activeNumberPrefixStartNumber;
    private bool _isDownloading;
    private bool _isConverting;
    private bool _isQueueConverting;
    private bool _isSimpleModeRunning;
    private bool _isInitializingUi;
    private bool _isApplyingPersistedUiOptions;
    private bool _hasUserChangedOutputPreset;
    private bool? _preSimpleCandidatesExpanded;
    private bool? _preSimpleLogExpanded;
    private string? _currentPlaylistFilePath;
    private bool _isPlaylistDirty;
    private bool _isUpdatingPlaylist;

    public MainWindow()
    {
        _isInitializingUi = true;
        _mpvExecutableResolver = new MpvExecutableResolver(_externalToolService.ToolsFolder);
        InitializeComponent();
        _settings = _settingsService.Load(out var settingsWarning);
        _externalToolService.EnsureToolsFolder();
        ApplyResolvedToolPathsFromSettings();
        _sessionOutputFolder = _settings.ConvertedFolder;
        LogListBox.ItemsSource = _logEntries;
        VideoListDataGrid.ItemsSource = _videos;
        ConversionQueueDataGrid.ItemsSource = _conversionQueue;
        _videos.CollectionChanged += Videos_CollectionChanged;
        _conversionQueue.CollectionChanged += ConversionQueue_CollectionChanged;
        _log.EntryAdded += OnLogEntryAdded;
        if (!string.IsNullOrWhiteSpace(settingsWarning))
        {
            _log.Error(settingsWarning);
        }

        PopulateOutputPresetComboBox();
        ApplyPersistedUiOptions();
        ApplyStartupLayout();
        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        UpdateExternalToolsStatus();
        UpdateSourceInputMode();
        UpdateAspectModeSelector();
        UpdateAudioAdjustmentControls();
        UpdateNumberPrefixControls();
        UpdateSectionHeaders();
        UpdateMainWorkspaceLayout();
        ApplyStartupRowLayout();
        ApplySimpleModeUiState(restoreNormalLayout: false);
        UpdateSimpleModeStatus();
        _isInitializingUi = false;
        SetPlaylistClean(null);
        _log.Info("Application started.");
        _log.Info("SD card copying and playback order sorting are handled outside NaviMovie-Maker, for example with Explorer and UMSSort.");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckExternalToolsOnStartupAsync();
    }

    private async void CheckExternalToolsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckExternalToolsMenuItem.IsEnabled = false;
        ExternalToolsStatusTextBlock.Text = "External tools: Checking...";
        _log.Info("Checking external tools from configured paths, tools folder, then PATH.");

        try
        {
            var result = await CheckExternalToolsAsync();
            LogToolResult(result.YtDlp);
            LogToolResult(result.Ffmpeg);
            LogToolResult(result.Ffprobe);
            UpdateExternalToolsStatus();
        }
        catch (Exception ex)
        {
            _log.Error($"Tool check failed unexpectedly. {ex.Message}");
            MessageBox.Show(this, ex.Message, "Tool Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CheckExternalToolsMenuItem.IsEnabled = true;
        }
    }

    private async void InstallExternalToolsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await InstallExternalToolsAsync();
    }

    private void OpenToolsFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_externalToolService.ToolsFolder, "tools フォルダ");
    }

    private void SavePlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SavePlaylist(saveAs: false);
    }

    private void SavePlaylistAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SavePlaylist(saveAs: true);
    }

    private async void PlaySourcePlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        await StartPlaylistPlaybackAsync(_playbackPlaylistBuilder.BuildSource(_conversionQueue), "元のプレイリストを再生");
    }

    private async void PlayConvertedPlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        await StartPlaylistPlaybackAsync(_playbackPlaylistBuilder.BuildConverted(_conversionQueue), "変換済みプレイリストを再生");
    }

    private async void PlayPlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ReconcileAllResults();
        var sourceReport = _playbackPlaylistBuilder.BuildSource(_conversionQueue);
        var playableItems = _conversionQueue
            .OrderBy(static item => item.Order)
            .Where(IsSourcePlayable)
            .ToList();
        var useResults = playableItems.Count > 0
            && playableItems.All(static item => item.ResultState == PlaylistResultState.Available && item.Result is not null);
        if (useResults)
        {
            _log.Info("処理結果のプレイリストを再生します");
            await StartPlaylistPlaybackAsync(_playbackPlaylistBuilder.BuildConverted(playableItems), "プレイリストを再生");
        }
        else
        {
            _log.Info("未処理の項目があるため、元データのプレイリストを再生します");
            await StartPlaylistPlaybackAsync(sourceReport, "プレイリストを再生");
        }
    }

    private static bool IsSourcePlayable(ConversionQueueItem item)
    {
        if (item.SourceType == "LocalFile") return File.Exists(item.SourcePathOrUrl);
        return item.SourceType == "OnlineVideo"
            && Uri.TryCreate(item.SourcePathOrUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private void RefreshOutputStateCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ReconcileAllResults();
        _log.Info("出力状態を更新しました。");
    }

    private void SynchronizeOutputSequenceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ReconcileAllResults();
        var sequenceStart = GetNumberPrefixStartNumber();
        if (sequenceStart is null)
        {
            MessageBox.Show(this, "連番開始を指定してください。", "出力ファイルの連番を同期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var renames = _playlistResultService.BuildSequenceRenames(_conversionQueue, sequenceStart.Value);
        if (renames.Count == 0)
        {
            MessageBox.Show(this, "同期が必要な出力ファイルはありません。", "出力ファイルの連番を同期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = string.Join(Environment.NewLine, renames.Select(static rename => $"{Path.GetFileName(rename.OldPath)} → {Path.GetFileName(rename.NewPath)}"));
        if (MessageBox.Show(this, $"次のファイル名を変更します。\n\n{preview}", "出力ファイルの連番を同期", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            _playlistResultService.ApplySequenceRenames(renames);
            MarkPlaylistDirty();
            _log.Success($"{renames.Count} 件の出力ファイル連番を同期しました。");
        }
        catch (Exception ex)
        {
            ReconcileAllResults();
            MessageBox.Show(this, $"連番を同期できませんでした。\n{ex.Message}", "出力ファイルの連番を同期", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PlaybackCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _conversionQueue.Count > 0;
    }

    private async Task StartPlaylistPlaybackAsync(PlaybackPlaylistReport report, string title)
    {
        if (report.Entries.Count == 0)
        {
            var detail = BuildPlaybackExclusionSummary(report);
            MessageBox.Show(this,
                "再生できる項目がありません。" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n{detail}"),
                title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (report.ExcludedCount > 0)
        {
            MessageBox.Show(this,
                $"{report.Entries.Count}件を再生します。\n{BuildPlaybackExclusionSummary(report)}",
                title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        var mpvPath = _mpvExecutableResolver.Resolve(_settings.MpvPath);
        if (mpvPath is null)
        {
            MessageBox.Show(this,
                "プレイリストの再生には mpv が必要です。\n［ツール］→［設定］→［外部ツール］で mpv.exe を選択してください。",
                title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ytDlpPath = _externalToolService.ResolveYtDlpExecutablePath(_settings.YtDlpPath);
        MpvPlaybackResult result;
        try
        {
            result = await _mpvPlaybackService.PlayAsync(
                mpvPath,
                report.Entries,
                ytDlpPath,
                diagnostics =>
                {
                    _log.Debug($"mpv playlist: {diagnostics.PlaylistPath}");
                    for (var index = 0; index < diagnostics.Entries.Count; index++)
                    {
                        _log.Debug($"mpv playlist entry {index + 1}: {diagnostics.Entries[index]}");
                    }
                    _log.Debug($"mpv arguments: {string.Join(" | ", diagnostics.Arguments)}");
                });
        }
        catch (Exception ex)
        {
            _log.Error($"mpv の再生準備に失敗しました。{ex.Message}");
            MessageBox.Show(this, $"mpv の再生準備に失敗しました。\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (result.Succeeded)
        {
            _log.Info($"mpv で {report.Entries.Count} 件の再生が終了しました。");
            return;
        }

        var (diagnosticLabel, diagnosticTail) = GetUsefulDiagnosticTail(result.StandardError, result.StandardOutput);
        var exitCode = result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "取得できませんでした";
        var usedYtDlpPath = result.Diagnostics.YtDlpPath ?? "使用していません";
        var message = $"mpv の再生に失敗しました。\n\n"
            + $"終了コード: {exitCode}\n"
            + $"mpv: {result.Diagnostics.MpvPath}\n"
            + $"yt-dlp: {usedYtDlpPath}\n"
            + $"一時プレイリスト: {result.Diagnostics.PlaylistPath}\n\n"
            + $"{diagnosticLabel}（末尾）:\n{diagnosticTail}";
        _log.Error(message);
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static (string Label, string Output) GetUsefulDiagnosticTail(string standardError, string standardOutput)
    {
        var label = "mpv stderr";
        var usefulOutput = standardError;
        if (string.IsNullOrWhiteSpace(usefulOutput))
        {
            label = "mpv stdout";
            usefulOutput = standardOutput;
        }
        if (string.IsNullOrWhiteSpace(usefulOutput))
        {
            return ("mpv 診断出力", "mpv は診断出力を生成しませんでした。");
        }

        var lines = usefulOutput.SplitLines().TakeLast(20);
        var tail = string.Join(Environment.NewLine, lines);
        const int maximumLength = 2500;
        return (label, tail.Length <= maximumLength ? tail : "…" + tail[^maximumLength..]);
    }

    private static string BuildPlaybackExclusionSummary(PlaybackPlaylistReport report)
    {
        return string.Join(Environment.NewLine,
            report.Exclusions.Select(static entry => $"{entry.Value}件は{entry.Key}ため除外しました。"));
    }

    private void ReconcileAllResults()
    {
        foreach (var item in _conversionQueue)
        {
            ReconcileResult(item);
        }
    }

    private PlaylistResultState ReconcileResult(ConversionQueueItem item, int? outputOrder = null)
    {
        try
        {
            var (operationMode, profileId, expectedSequence) = GetResultContext(item, outputOrder);
            return _playlistResultService.Reconcile(item, operationMode, profileId, expectedSequence);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            item.ResultState = PlaylistResultState.NeedsReprocess;
            item.ResultStateReason = $"結果を照合できませんでした: {ex.Message}";
            return item.ResultState;
        }
    }

    private (string OperationMode, string ProfileId, int? SequenceNumber) GetResultContext(
        ConversionQueueItem item,
        int? outputOrder = null)
    {
        var simpleMode = _isSimpleModeRunning || SimpleModeCheckBox.IsChecked == true && item.IsSimpleModeItem;
        var operationMode = simpleMode ? "Simple Mode" : GetQueueExecutionMode();
        var preset = GetSelectedConversionPreset();
        var profileId = operationMode switch
        {
            "Download Only" => ResolveDownloadProfile(operationMode, null).Id,
            "Copy Files" => "copy",
            _ => preset.Id,
        };
        var sequenceNumber = operationMode is "Download Only" or "Simple Mode"
            ? null
            : _activeNumberPrefixStartNumber is not null
                ? outputOrder
                : GetNumberPrefixStartNumber() is int start
                    ? start + item.Order - 1
                    : null;
        return (operationMode, profileId, sequenceNumber);
    }

    private void RecordSuccessfulResult(ConversionQueueItem item, string resultPath, int outputOrder)
    {
        var (operationMode, profileId, sequenceNumber) = GetResultContext(item, outputOrder);
        _playlistResultService.RecordSuccessfulResult(item, resultPath, operationMode, profileId, sequenceNumber);
        MarkPlaylistDirty();
    }

    private bool SavePlaylist(bool saveAs)
    {
        ConversionQueueDataGrid.CommitEdit();
        ConversionQueueDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var filePath = saveAs ? null : _currentPlaylistFilePath;
        var defaultFolder = GetPlaylistFolder();
        try
        {
            Directory.CreateDirectory(defaultFolder);
        }
        catch (Exception ex)
        {
            _log.Error($"プレイリスト保存先を作成できませんでした。{ex.Message}");
            MessageBox.Show(this, ex.Message, "プレイリストを保存", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (filePath is null)
        {
            var dialog = new SaveFileDialog
            {
                Title = saveAs ? "名前を付けて保存" : "プレイリストを保存",
                InitialDirectory = defaultFolder,
                FileName = _currentPlaylistFilePath is null
                    ? $"playlist-{DateTime.Now:yyyyMMdd-HHmmss}.nmm-playlist.json"
                    : Path.GetFileName(_currentPlaylistFilePath),
                DefaultExt = ".nmm-playlist.json",
                AddExtension = true,
                Filter = "NaviMovie-Maker プレイリスト|*.nmm-playlist.json|JSON ファイル|*.json",
            };
            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }
            filePath = dialog.FileName;
        }

        try
        {
            _playlistService.Save(filePath, CreatePlaylist(filePath));
            RememberPlaylistFolder(filePath);
            SetPlaylistClean(filePath);
            _log.Success($"プレイリストを保存しました: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"プレイリストを保存できませんでした。{ex.Message}");
            MessageBox.Show(this, $"プレイリストを保存できませんでした。\n{ex.Message}", "プレイリストを保存", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenPlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (IsQueueProcessingActive())
        {
            MessageBox.Show(this, "キュー処理中はプレイリストを開けません。先にキャンセルするか、処理が完了してから開いてください。", "プレイリストを開く", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ConfirmDiscardOrSaveChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "プレイリストを開く",
            InitialDirectory = GetPlaylistFolder(),
            Filter = "NaviMovie-Maker プレイリスト|*.nmm-playlist.json|JSON ファイル|*.json",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ConversionPlaylist playlist;
        try
        {
            playlist = _playlistService.Load(dialog.FileName);
            ValidatePlaylist(playlist);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _log.Error($"プレイリストを開けませんでした。{ex.Message}");
            MessageBox.Show(this, ex.Message, "プレイリストを開く", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _isUpdatingPlaylist = true;
        try { ApplyPlaylist(playlist); }
        finally { _isUpdatingPlaylist = false; }
        RememberPlaylistFolder(dialog.FileName);
        SetPlaylistClean(dialog.FileName);
        _log.Success($"プレイリストを開きました: {dialog.FileName} ({_conversionQueue.Count} 件)");
    }

    private void NewPlaylistCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscardOrSaveChanges()) return;
        _isUpdatingPlaylist = true;
        try
        {
            _sessionOutputFolder = _settings.ConvertedFolder;
            _conversionQueue.Clear();
            ClearQueueProgress();
            RefreshQueueOrderNumbers();
            UpdateConvertQueueButtonState();
        }
        finally { _isUpdatingPlaylist = false; }
        SetPlaylistClean(null);
        _log.Info("新規プレイリストを作成しました。");
    }

    private void PlaylistCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !IsQueueProcessingActive();
    }

    private bool ConfirmDiscardOrSaveChanges()
    {
        if (!_isPlaylistDirty) return true;
        var dialog = new UnsavedPlaylistChangesDialog { Owner = this };
        dialog.ShowDialog();
        return dialog.Choice switch
        {
            UnsavedPlaylistChoice.Save => SavePlaylist(saveAs: false),
            UnsavedPlaylistChoice.DontSave => true,
            _ => false,
        };
    }

    private void MarkPlaylistDirty()
    {
        if (_isInitializingUi || _isUpdatingPlaylist || _isApplyingPersistedUiOptions) return;
        _isPlaylistDirty = true;
        UpdateWindowTitle();
    }

    private void SetPlaylistClean(string? filePath)
    {
        _currentPlaylistFilePath = filePath;
        _isPlaylistDirty = false;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var name = _currentPlaylistFilePath is null
            ? null
            : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(_currentPlaylistFilePath));
        Title = "NaviMovie-Maker" + (name is null ? string.Empty : $" - {name}") + (_isPlaylistDirty ? " *" : string.Empty);
    }

    private ConversionPlaylist CreatePlaylist(string filePath)
    {
        var now = DateTimeOffset.Now;
        var replayGainOptions = GetReplayGainNormalizationOptions();
        return new ConversionPlaylist
        {
            AppVersion = GetType().Assembly.GetName().Version?.ToString() ?? string.Empty,
            Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath)),
            CreatedAt = now,
            UpdatedAt = now,
            OutputFolder = GetBaseOutputFolder(),
            OutputPresetId = GetSelectedComboBoxTag(OutputPresetComboBox) ?? string.Empty,
            SimpleModeEnabled = SimpleModeCheckBox.IsChecked == true,
            OperationMode = GetQueueExecutionMode(),
            AspectMode = GetSelectedAspectMode(),
            KeepOriginalDownloadedFiles = KeepOriginalDownloadedFilesCheckBox.IsChecked == true,
            PeakBoost = PeakBoostCheckBox.IsChecked == true,
            AudioNormalizationMode = GetSelectedAudioNormalizationMode(),
            TargetPeakDb = GetSelectedTargetPeakDb(),
            TargetReplayGainVolumeDb = replayGainOptions.TargetReplayGainVolumeDb,
            PeakLimitDb = replayGainOptions.PeakLimitDb,
            NormalizationToleranceDb = replayGainOptions.ToleranceDb,
            MaximumNormalizationGainDb = replayGainOptions.MaximumGainDb,
            NumberPrefixStart = GetNumberPrefixStartNumber(),
            Items = _conversionQueue
                .OrderBy(static item => item.Order)
                .Select(item => new ConversionPlaylistItem
                {
                    ItemId = item.ItemId,
                    SourceKind = item.SourceType,
                    SourcePathOrUrl = item.SourcePathOrUrl,
                    Title = item.Title,
                    OutputBaseName = item.Title,
                    OriginalUrl = item.SourceType == "OnlineVideo" ? item.SourcePathOrUrl : string.Empty,
                    LocalFilePath = item.SourceType == "LocalFile" ? item.SourcePathOrUrl : string.Empty,
                    Notes = item.IsUnsupported ? item.UnsupportedReason : null,
                    IsSimpleModeItem = item.IsSimpleModeItem,
                    AudioAdjustmentMode = item.AudioAdjustmentMode,
                    Result = item.Result,
                })
                .ToList(),
        };
    }

    private static void ValidatePlaylist(ConversionPlaylist playlist)
    {
        if (playlist.FormatVersion is < 1 or > ConversionPlaylist.CurrentFormatVersion)
        {
            throw new InvalidDataException($"このプレイリストの形式バージョン ({playlist.FormatVersion}) には対応していません。");
        }

        if (playlist.Items is null)
        {
            throw new InvalidDataException("プレイリストの変換キュー情報を読み取れませんでした。");
        }

        for (var index = 0; index < playlist.Items.Count; index++)
        {
            var item = playlist.Items[index];
            if (string.IsNullOrWhiteSpace(item.SourceKind)
                || string.IsNullOrWhiteSpace(item.SourcePathOrUrl))
            {
                throw new InvalidDataException($"プレイリストの {index + 1} 件目に必要なソース情報がありません。");
            }
        }
    }

    private void ApplyPlaylist(ConversionPlaylist playlist)
    {
        _sessionOutputFolder = ConversionPlaylistService.ResolveOutputFolder(playlist, _settings.ConvertedFolder);
        if (!string.IsNullOrWhiteSpace(playlist.OutputFolder) && !Directory.Exists(_sessionOutputFolder))
        {
            _log.Warn($"プレイリストの出力フォルダが見つかりません: {_sessionOutputFolder}");
        }

        _conversionQueue.Clear();
        foreach (var playlistItem in playlist.Items)
        {
            _conversionQueue.Add(CreateQueueItemFromPlaylist(playlistItem));
        }

        ApplyPlaylistSettings(playlist);
        RefreshQueueOrderNumbers();
        ReconcileAllResults();
        ClearQueueProgress();
        UpdateQueueUnsupportedStatusesForCurrentMode();
        UpdateAudioAdjustmentControls();
        UpdateConvertQueueButtonState();
        ConversionQueueExpander.IsExpanded = true;
    }

    private ConversionQueueItem CreateQueueItemFromPlaylist(ConversionPlaylistItem playlistItem)
    {
        var sourceKind = playlistItem.SourceKind.Trim();
        var source = playlistItem.SourcePathOrUrl.Trim();
        var title = string.IsNullOrWhiteSpace(playlistItem.Title)
            ? playlistItem.OutputBaseName.Trim()
            : playlistItem.Title.Trim();

        ConversionQueueItem item;
        if (string.Equals(sourceKind, "LocalFile", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = string.IsNullOrWhiteSpace(playlistItem.LocalFilePath)
                ? source
                : playlistItem.LocalFilePath.Trim();
            item = CreateQueueItem(
                sourceType: "LocalFile",
                title: string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(localPath) : title,
                sourcePathOrUrl: localPath,
                status: QueueStatusReady,
                unsupportedReason: File.Exists(localPath) ? string.Empty : MissingLocalFileReason,
                isSimpleModeItem: playlistItem.IsSimpleModeItem,
                itemId: playlistItem.ItemId,
                result: playlistItem.Result);
        }
        else if (string.Equals(sourceKind, "OnlineVideo", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            item = CreateQueueItem(
                sourceType: "OnlineVideo",
                title: string.IsNullOrWhiteSpace(title) ? source : title,
                sourcePathOrUrl: source,
                status: QueueStatusReady,
                isSimpleModeItem: playlistItem.IsSimpleModeItem,
                itemId: playlistItem.ItemId,
                result: playlistItem.Result);
        }
        else
        {
            item = CreateQueueItem(
                sourceType: "Unsupported",
                title: string.IsNullOrWhiteSpace(title) ? "対象外の項目" : title,
                sourcePathOrUrl: source,
                status: QueueStatusUnsupported,
                unsupportedReason: string.IsNullOrWhiteSpace(playlistItem.Notes)
                    ? "プレイリスト内のソース情報が処理対象外です。"
                    : playlistItem.Notes,
                isSimpleModeItem: playlistItem.IsSimpleModeItem,
                itemId: playlistItem.ItemId,
                result: playlistItem.Result);
        }

        item.AudioAdjustmentMode = Enum.IsDefined(playlistItem.AudioAdjustmentMode)
            ? playlistItem.AudioAdjustmentMode
            : AudioAdjustmentMode.Off;
        return item;
    }

    private void ApplyPlaylistSettings(ConversionPlaylist playlist)
    {
        _isApplyingPersistedUiOptions = true;
        try
        {
            SelectComboBoxItemByTag(QueueExecutionModeComboBox, playlist.OperationMode);
            SelectComboBoxItemByTag(AspectModeComboBox, playlist.AspectMode);
            KeepOriginalDownloadedFilesCheckBox.IsChecked = playlist.KeepOriginalDownloadedFiles;
            PeakBoostCheckBox.IsChecked = playlist.PeakBoost;
            SelectComboBoxItemByTag(NormalizationModeComboBox, playlist.AudioNormalizationMode.ToString());
            SelectComboBoxItemByContent(
                TargetPeakComboBox,
                $"{playlist.TargetPeakDb.ToString("0.0", CultureInfo.InvariantCulture)} dBFS",
                "-1.0 dBFS");
            ApplyReplayGainNormalizationOptionsToControls(new ReplayGainNormalizationOptions(
                playlist.TargetReplayGainVolumeDb,
                playlist.PeakLimitDb,
                playlist.NormalizationToleranceDb,
                playlist.MaximumNormalizationGainDb).Normalize());
            NumberPrefixTextBox.Text = playlist.NumberPrefixStart is > 0
                ? playlist.NumberPrefixStart.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

            var presetItem = OutputPresetComboBox.Items
                .OfType<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), playlist.OutputPresetId, StringComparison.OrdinalIgnoreCase));
            if (presetItem is not null)
            {
                OutputPresetComboBox.SelectedItem = presetItem;
                _hasUserChangedOutputPreset = true;
            }
            else if (!string.IsNullOrWhiteSpace(playlist.OutputPresetId))
            {
                _log.Warn($"プレイリストの出力プリセット '{playlist.OutputPresetId}' は現在表示されていないか利用できないため、現在のプリセットを使用します。");
            }

            SimpleModeCheckBox.IsChecked = playlist.SimpleModeEnabled;
        }
        finally
        {
            _isApplyingPersistedUiOptions = false;
        }

        ApplySimpleModeUiState(restoreNormalLayout: true);
        UpdateSimpleModeStatus();
        UpdateAspectModeSelector();
        SavePersistedUiOptions();
    }

    private string GetPlaylistFolder()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastPlaylistFolder)
            && Directory.Exists(_settings.LastPlaylistFolder))
        {
            return _settings.LastPlaylistFolder;
        }

        var videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return Path.Combine(
            string.IsNullOrWhiteSpace(videosFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : videosFolder,
            "NaviMovie-Maker",
            "playlists");
    }

    private void RememberPlaylistFolder(string filePath)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        _settings.LastPlaylistFolder = folder;
        SavePersistedUiOptions();
    }

    private async void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings, _settingsService)
        {
            Owner = this,
        };

        if (settingsWindow.ShowDialog() != true)
        {
            return;
        }

        _settings = settingsWindow.Settings;
        ApplyResolvedToolPathsFromSettings();
        PopulateOutputPresetComboBox();
        SavePersistedUiOptions();
        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        var toolResult = await CheckExternalToolsAsync();
        UpdateExternalToolsStatus();
        foreach (var tool in toolResult.Results)
        {
            LogToolResult(tool);
        }

        _log.Info($"Settings saved: {_settingsService.SettingsFilePath}");
        _log.Info("Configured folders were created if they did not already exist.");
    }

    private void OpenWorkingFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.WorkingFolder, "作業フォルダ");
    }

    private void OpenConvertedFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.ConvertedFolder, "変換済みフォルダ");
    }

    private void OpenTemporaryFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.TemporaryFolder, "一時フォルダ");
    }

    private void OpenLocalVideoFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.LocalVideoFolder, "ローカル動画フォルダ");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardOrSaveChanges())
        {
            e.Cancel = true;
            return;
        }
        SaveLastUsedLayoutState();
        SavePersistedUiOptions();
    }

    private void OutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder for converted and copied files",
            InitialDirectory = Directory.Exists(GetBaseOutputFolder())
                ? GetBaseOutputFolder()
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dialog.FolderName);
            if (!string.Equals(_sessionOutputFolder, dialog.FolderName, StringComparison.OrdinalIgnoreCase))
            {
                _sessionOutputFolder = dialog.FolderName;
                MarkPlaylistDirty();
            }
            _log.Info($"Output folder set for this session: {_sessionOutputFolder}");
        }
        catch (Exception ex)
        {
            _log.Error($"Output folder could not be created: {dialog.FolderName}. {ex.Message}");
            MessageBox.Show(this, ex.Message, "Output Folder Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void FetchVideoListButton_Click(object sender, RoutedEventArgs e)
    {
        var ytDlpInput = BuildVideoSourceInput();
        if (ytDlpInput is null)
        {
            return;
        }

        if (!await EnsureRequiredToolsAsync(requireYtDlp: true, requireFfmpeg: false, "動画情報の取得"))
        {
            return;
        }

        SetFetchControlsEnabled(false);

        try
        {
            var result = await _videoMetadataService.FetchVideoListAsync(ytDlpInput, message => _log.Info(message));
            if (!result.IsSuccess)
            {
                _log.Error($"yt-dlp failed with exit code {result.ExitCode?.ToString() ?? "unknown"}.");
                LogProcessOutput(result.StandardError, "yt-dlp stderr");
                LogProcessOutput(result.StandardOutput, "yt-dlp stdout");
                return;
            }

            _videos.Clear();
            foreach (var video in result.Videos)
            {
                _videos.Add(video);
            }

            RefreshOrderNumbers();
            _ = LoadCandidateThumbnailsAsync(result.Videos);
            _log.Success($"Fetch completed. {result.Videos.Count} item(s) fetched.");
            LogProcessOutput(result.StandardError, "yt-dlp stderr");
        }
        catch (Exception ex)
        {
            _log.Error($"Fetch failed unexpectedly. {ex.Message}");
        }
        finally
        {
            SetFetchControlsEnabled(true);
        }
    }

    private void SourceModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        UpdateSourceInputMode();
    }

    private void VideoSourceInputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        FetchVideoListButton_Click(FetchVideoListButton, new RoutedEventArgs());
        e.Handled = true;
    }

    private void VideoSourceInputTextBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedText(e.Data) is null
            ? DragDropEffects.None
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void VideoSourceInputTextBox_Drop(object sender, DragEventArgs e)
    {
        var droppedText = GetDroppedText(e.Data);
        if (droppedText is null)
        {
            e.Handled = true;
            return;
        }

        var urls = GetDroppedUrls(droppedText).ToList();
        if (urls.Count == 0)
        {
            _log.Info($"Dropped text is not recognized as URL: {GetLogPreview(droppedText)}");
            e.Handled = true;
            return;
        }

        var originalUrl = urls[0];
        var validation = NormalizeSingleVideoUrl(originalUrl);
        if (!validation.IsAllowed)
        {
            _log.Error(validation.Reason);
            e.Handled = true;
            return;
        }

        var normalizedUrl = validation.Url;
        LogDroppedUrlNormalization(originalUrl, normalizedUrl);

        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = normalizedUrl;
            textBox.CaretIndex = textBox.Text.Length;
            _log.Info($"Dropped URL into video source input: {normalizedUrl}");
        }

        e.Handled = true;
    }

    private void SimpleModeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPersistedUiOptions)
        {
            return;
        }

        _settings.SimpleModeEnabled = SimpleModeCheckBox.IsChecked == true;
        SavePersistedUiOptions();
        ApplySimpleModeUiState(restoreNormalLayout: true);
        UpdateSimpleModeStatus();
        MarkPlaylistDirty();
    }

    private void ApplySimpleModeUiState(bool restoreNormalLayout)
    {
        if (SimpleModeCheckBox is null
            || CandidatesExpander is null
            || ConversionQueueExpander is null
            || LogExpander is null)
        {
            return;
        }

        var simpleModeEnabled = SimpleModeCheckBox.IsChecked == true;
        SimpleModePanel.Visibility = simpleModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        NormalQueueOptionsPanel.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        NormalAudioOptionsPanel.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        NormalQueueActionsPanel.Visibility = Visibility.Visible;
        ConvertQueueButton.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        RetryFailedQueueButton.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        CancelQueueButton.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        OpenConvertedFolderButton.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        CandidatesExpander.Visibility = simpleModeEnabled ? Visibility.Collapsed : Visibility.Visible;

        if (simpleModeEnabled)
        {
            _preSimpleCandidatesExpanded ??= CandidatesExpander.IsExpanded;
            _preSimpleLogExpanded ??= LogExpander.IsExpanded;
            ConversionQueueExpander.IsExpanded = true;
        }
        else if (restoreNormalLayout)
        {
            if (_preSimpleCandidatesExpanded is { } candidatesExpanded)
            {
                CandidatesExpander.IsExpanded = candidatesExpanded;
            }

            if (_preSimpleLogExpanded is { } logExpanded)
            {
                LogExpander.IsExpanded = logExpanded;
            }

            _preSimpleCandidatesExpanded = null;
            _preSimpleLogExpanded = null;
        }

        UpdateMainWorkspaceLayout();
        if (simpleModeEnabled)
        {
            VideoListWorkRow.MinHeight = 0;
            VideoListWorkRow.Height = GridLength.Auto;
            QueueWorkRow.MinHeight = 180;
            QueueWorkRow.Height = new GridLength(3, GridUnitType.Star);
            LogWorkRow.MinHeight = LogExpander.IsExpanded ? 140 : 0;
            LogWorkRow.Height = LogExpander.IsExpanded
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
            CandidateQueueGridSplitter.Visibility = Visibility.Collapsed;
            LogGridSplitter.Visibility = LogExpander.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateSimpleModeStatus()
    {
        if (SimpleModeStatusTextBlock is null || SimpleModeCheckBox is null)
        {
            return;
        }

        if (_isSimpleModeRunning)
        {
            SimpleModeStatusTextBlock.Text = "Simple Mode 処理中...";
            return;
        }

        SimpleModeStatusTextBlock.Text = SimpleModeCheckBox.IsChecked == true
            ? "URL またはローカルファイルをここへドロップ"
            : "Simple Mode を有効にするとドロップできます";
    }

    private void SimpleModeDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = SimpleModeCheckBox.IsChecked == true
            && (e.Data.GetDataPresent(DataFormats.FileDrop) || GetDroppedText(e.Data) is not null)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        e.Handled = true;
    }

    private async void SimpleModeDropArea_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (SimpleModeCheckBox.IsChecked != true)
        {
            SimpleModeStatusTextBlock.Text = "Simple Mode を有効にしてください";
            return;
        }

        var beforeItems = _conversionQueue.ToHashSet();
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddLocalFilesToQueue(paths, "Simple Mode", isSimpleModeItem: true);
        }
        else if (GetDroppedText(e.Data) is { } droppedText)
        {
            await AddDroppedTextUrlsToQueueAsync(droppedText, isSimpleModeItem: true);
        }
        else
        {
            SimpleModeStatusTextBlock.Text = "ドロップされた項目を認識できません";
            return;
        }

        var addedItems = _conversionQueue
            .Where(item => !beforeItems.Contains(item) && item.IsSimpleModeItem)
            .ToList();
        if (addedItems.Count == 0)
        {
            SimpleModeStatusTextBlock.Text = "追加できる項目がありません";
            return;
        }

        SimpleModeStatusTextBlock.Text = $"{addedItems.Count} 件を追加しました";
        _ = RunSimpleModeQueueAsync();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoListDataGrid.SelectedItem is not VideoListItem selectedItem)
        {
            return;
        }

        var index = _videos.IndexOf(selectedItem);
        if (index <= 0)
        {
            return;
        }

        _videos.Move(index, index - 1);
        RefreshOrderNumbers();
        VideoListDataGrid.SelectedItem = selectedItem;
        VideoListDataGrid.ScrollIntoView(selectedItem);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoListDataGrid.SelectedItem is not VideoListItem selectedItem)
        {
            return;
        }

        var index = _videos.IndexOf(selectedItem);
        if (index < 0 || index >= _videos.Count - 1)
        {
            return;
        }

        _videos.Move(index, index + 1);
        RefreshOrderNumbers();
        VideoListDataGrid.SelectedItem = selectedItem;
        VideoListDataGrid.ScrollIntoView(selectedItem);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = VideoListDataGrid.SelectedItems
            .OfType<VideoListItem>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            return;
        }

        foreach (var selectedItem in selectedItems)
        {
            _videos.Remove(selectedItem);
        }

        RefreshOrderNumbers();
        _log.Info($"Removed {selectedItems.Count} item(s) from the video list.");
    }

    private void VideoSelectionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox
            || checkBox.DataContext is not VideoListItem clickedItem)
        {
            return;
        }

        var newSelectionState = checkBox.IsChecked == true;
        var selectedItems = VideoListDataGrid.SelectedItems
            .OfType<VideoListItem>()
            .ToList();

        if (selectedItems.Count > 1 && selectedItems.Contains(clickedItem))
        {
            foreach (var selectedItem in selectedItems)
            {
                selectedItem.IsSelected = newSelectionState;
            }

            _log.Info($"{(newSelectionState ? "Selected" : "Deselected")} {selectedItems.Count} selected row(s).");
        }
        else
        {
            clickedItem.IsSelected = newSelectionState;
        }

        UpdateDownloadButtonState();
        UpdateConvertButtonState();
    }

    private void SelectAllVideosButton_Click(object sender, RoutedEventArgs e)
    {
        var changedCount = SetVideoSelection(isSelected: true);
        _log.Info($"Selected {changedCount} item(s).");
    }

    private void DeselectAllVideosButton_Click(object sender, RoutedEventArgs e)
    {
        var changedCount = SetVideoSelection(isSelected: false);
        _log.Info($"Deselected {changedCount} item(s).");
    }

    private void InvertVideoSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var video in _videos)
        {
            video.IsSelected = !video.IsSelected;
        }

        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        _log.Info($"Inverted selection for {_videos.Count} item(s).");
    }

    private void CandidateThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VideoListItem video })
        {
            return;
        }

        var sourceUrl = string.IsNullOrWhiteSpace(video.Url) ? video.SourcePath : video.Url;
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            _log.Error($"Cannot open source URL for {video.Title}: URL is missing.");
            return;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri)
            || sourceUri.Scheme is not ("http" or "https"))
        {
            _log.Error($"Cannot open source URL for {video.Title}: unsupported URL '{sourceUrl}'.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = sourceUri.AbsoluteUri,
                UseShellExecute = true,
            });
            _log.Info($"Opened candidate source URL: {sourceUri.AbsoluteUri}");
        }
        catch (Exception ex)
        {
            _log.Error($"Could not open candidate source URL. {ex.Message}");
        }
    }

    private static ConversionQueueItem CreateQueueItem(
        string sourceType,
        string title,
        string sourcePathOrUrl,
        string status,
        string downloadedFilePath = "",
        string convertedFilePath = "",
        string unsupportedReason = "",
        bool isSimpleModeItem = false,
        string? itemId = null,
        PlaylistResultRecord? result = null)
    {
        return new ConversionQueueItem
        {
            SourceType = sourceType,
            Title = title,
            SourcePathOrUrl = sourcePathOrUrl,
            IsSimpleModeItem = isSimpleModeItem,
            DownloadedFilePath = downloadedFilePath,
            ConvertedFilePath = convertedFilePath,
            Status = status,
            UnsupportedReason = unsupportedReason,
            ItemId = string.IsNullOrWhiteSpace(itemId) ? Guid.NewGuid().ToString("N") : itemId,
            Result = result,
        };
    }

    private void AddSelectedToQueueButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedVideos = VideoListDataGrid.SelectedItems
            .OfType<VideoListItem>()
            .OrderBy(static video => video.Order)
            .ToList();

        if (selectedVideos.Count == 0)
        {
            _log.Error("Select at least one online candidate before adding to the conversion queue.");
            return;
        }

        var addedCount = 0;
        var skippedCount = 0;
        foreach (var video in selectedVideos)
        {
            var sourcePathOrUrl = string.IsNullOrWhiteSpace(video.SourcePath) ? video.Url : video.SourcePath;
            if (string.IsNullOrWhiteSpace(sourcePathOrUrl))
            {
                skippedCount++;
                _log.Error($"Skipped queue add for {video.Title}: source URL is missing.");
                continue;
            }

            if (QueueContainsSource(sourcePathOrUrl))
            {
                skippedCount++;
                _log.Info($"Skipped duplicate queue item: {sourcePathOrUrl}");
                continue;
            }

            _conversionQueue.Add(CreateQueueItem(
                sourceType: "OnlineVideo",
                title: video.Title,
                sourcePathOrUrl: sourcePathOrUrl,
                status: "Pending",
                downloadedFilePath: video.DownloadedFilePath,
                convertedFilePath: video.ConvertedFilePath));
            addedCount++;
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Added {addedCount} online item(s) to the conversion queue. Skipped {skippedCount} item(s).");
    }

    private void AddLocalFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = Directory.Exists(_settings.LocalVideoFolder)
            ? _settings.LocalVideoFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        var dialog = new OpenFileDialog
        {
            Title = "Add local media files to conversion queue",
            InitialDirectory = initialDirectory,
            Multiselect = true,
            Filter = "Media files|*.mp4;*.m4v;*.mov;*.avi;*.mpg;*.mpeg;*.wmv;*.mkv;*.webm;*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.wma|Video files|*.mp4;*.m4v;*.mov;*.avi;*.mpg;*.mpeg;*.wmv;*.mkv;*.webm|Audio files|*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.wma|All files|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AddLocalFilesToQueue(dialog.FileNames, "file picker");
    }

    private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        VideoListDataGrid.CommitEdit();
        VideoListDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var workingFolder = _settings.WorkingFolder.Trim();
        if (string.IsNullOrWhiteSpace(workingFolder))
        {
            _log.Error("Configure a working folder in Settings before downloading.");
            return;
        }

        try
        {
            Directory.CreateDirectory(workingFolder);
        }
        catch (Exception ex)
        {
            _log.Error($"Working folder could not be created. {ex.Message}");
            return;
        }

        var selectedVideos = _videos
            .Where(static video => video.IsSelected && video.SourceType == "YouTube")
            .OrderBy(static video => video.Order)
            .ToList();

        if (selectedVideos.Count == 0)
        {
            _log.Error("Select at least one YouTube video before downloading. Local files can be converted directly.");
            return;
        }

        if (!await EnsureRequiredToolsAsync(requireYtDlp: true, requireFfmpeg: false, "ダウンロード"))
        {
            return;
        }

        _isDownloading = true;
        _downloadCancellationTokenSource = new CancellationTokenSource();
        SetDownloadState(isDownloading: true);
        _log.Info($"Starting download of {selectedVideos.Count} selected item(s) to: {workingFolder}");
        var downloadProfile = ResolveDownloadProfile("Download Only", null);
        LogDownloadProfileSelection(downloadProfile);

        try
        {
            foreach (var video in selectedVideos)
            {
                video.Status = "Pending";
            }

            var downloadOrder = 1;
            foreach (var video in selectedVideos)
            {
                if (_downloadCancellationTokenSource.IsCancellationRequested)
                {
                    video.Status = "Skipped";
                    MarkRemainingAsSkipped(selectedVideos, video);
                    break;
                }

                video.Status = "Downloading";
                _log.Info($"Downloading {downloadOrder:000}: {video.Title}");

                var result = await _videoDownloadService.DownloadAsync(
                    video,
                    workingFolder,
                    downloadOrder,
                    message => _log.Info(message),
                    downloadProfile,
                    cancellationToken: _downloadCancellationTokenSource.Token);

                if (result.IsCanceled)
                {
                    video.Status = "Skipped";
                    MarkRemainingAsSkipped(selectedVideos, video);
                    _log.Info("Download canceled.");
                    break;
                }

                if (result.IsSuccess)
                {
                    video.DownloadedFilePath = result.DownloadedFilePath ?? string.Empty;
                    video.Status = "Downloaded";
                    _log.Success($"Downloaded {downloadOrder:000}: {video.Title}");
                    if (string.IsNullOrWhiteSpace(video.DownloadedFilePath))
                    {
                        _log.Error($"Downloaded file path could not be detected for {downloadOrder:000}: {video.Title}");
                    }
                }
                else
                {
                    video.Status = "Failed";
                    _log.Error($"Download failed for {downloadOrder:000}: {video.Title}");
                    LogProcessOutput(result.StandardError, "yt-dlp stderr");
                    LogProcessOutput(result.StandardOutput, "yt-dlp stdout");
                }

                downloadOrder++;
            }
        }
        finally
        {
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;
            _isDownloading = false;
            SetDownloadState(isDownloading: false);
            _log.Info("Download task finished.");
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Cancel requested. Stopping current yt-dlp process...");
        _downloadCancellationTokenSource?.Cancel();
    }

    private void OutputPresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isInitializingUi && !_isApplyingPersistedUiOptions)
        {
            _hasUserChangedOutputPreset = true;
        }

        SyncPresetSelection(OutputPresetComboBox, SimpleOutputPresetComboBox);
        UpdateAspectModeSelector();
        SavePersistedUiOptions();
        MarkPlaylistDirty();
    }

    private void SimpleOutputPresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isInitializingUi && !_isApplyingPersistedUiOptions)
        {
            _hasUserChangedOutputPreset = true;
        }

        SyncPresetSelection(SimpleOutputPresetComboBox, OutputPresetComboBox);
        SavePersistedUiOptions();
        MarkPlaylistDirty();
    }

    private static void SyncPresetSelection(
        System.Windows.Controls.ComboBox source,
        System.Windows.Controls.ComboBox target)
    {
        var selectedTag = GetSelectedComboBoxTag(source);
        if (string.IsNullOrWhiteSpace(selectedTag)
            || string.Equals(GetSelectedComboBoxTag(target), selectedTag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectComboBoxItemByTag(target, selectedTag);
    }

    private void AudioAdjustmentComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateAudioAdjustmentControls();
    }

    private void PeakBoostCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateAudioAdjustmentControls();
        SavePersistedUiOptions();
        MarkPlaylistDirty();
    }

    private void NormalizationModeComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateAudioAdjustmentControls();
        SavePersistedUiOptions();
        MarkPlaylistDirty();
    }

    private void NormalizationValueTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        SavePersistedUiOptions();
        MarkPlaylistDirty();
    }

    private void NormalizationValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isApplyingPersistedUiOptions = true;
        try
        {
            ApplyReplayGainNormalizationOptionsToControls(GetReplayGainNormalizationOptions());
        }
        finally
        {
            _isApplyingPersistedUiOptions = false;
        }

        SavePersistedUiOptions();
    }

    private void ReplayGainIncrementButton_Click(object sender, RoutedEventArgs e)
    {
        AdjustTargetReplayGainVolume(ReplayGainNormalizationOptions.TargetReplayGainVolumeStepDb);
    }

    private void ReplayGainDecrementButton_Click(object sender, RoutedEventArgs e)
    {
        AdjustTargetReplayGainVolume(-ReplayGainNormalizationOptions.TargetReplayGainVolumeStepDb);
    }

    private void AdjustTargetReplayGainVolume(double deltaDb)
    {
        var options = GetReplayGainNormalizationOptions();
        var adjusted = options with
        {
            TargetReplayGainVolumeDb = options.TargetReplayGainVolumeDb + deltaDb,
        };
        TargetReplayGainVolumeTextBox.Text = adjusted.Normalize().TargetReplayGainVolumeDb
            .ToString("0.0", CultureInfo.InvariantCulture);
        TargetReplayGainVolumeTextBox.Focus();
        TargetReplayGainVolumeTextBox.SelectAll();
    }

    private void PersistedUiOption_Changed(object sender, RoutedEventArgs e)
    {
        SavePersistedUiOptions();
        UpdateQueueUnsupportedStatusesForCurrentMode();
        MarkPlaylistDirty();
    }

    private void PlaylistSetting_Changed(object sender, RoutedEventArgs e)
    {
        MarkPlaylistDirty();
    }

    private void NumberPrefixTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void ApplyAudioAdjustmentButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ConversionQueueDataGrid.SelectedItems
            .OfType<ConversionQueueItem>()
            .ToList();
        if (selectedItems.Count == 0)
        {
            _log.Error("Select one or more queue items before applying audio adjustment.");
            return;
        }

        var mode = GetSelectedAudioAdjustmentMode();
        foreach (var item in selectedItems)
        {
            item.AudioAdjustmentMode = mode;
        }

        _log.Info($"Applied audio adjustment '{GetAudioAdjustmentDisplay(mode)}' to {selectedItems.Count} queue item(s).");
    }

    private async void ConvertDownloadedButton_Click(object sender, RoutedEventArgs e)
    {
        VideoListDataGrid.CommitEdit();
        VideoListDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var convertedFolder = _settings.ConvertedFolder.Trim();
        if (string.IsNullOrWhiteSpace(convertedFolder))
        {
            _log.Error("Configure a converted output folder in Settings before converting.");
            return;
        }

        try
        {
            Directory.CreateDirectory(convertedFolder);
        }
        catch (Exception ex)
        {
            _log.Error($"Converted output folder could not be created. {ex.Message}");
            return;
        }

        var selectedVideos = _videos
            .Where(static video => video.IsSelected)
            .OrderBy(static video => video.Order)
            .ToList();

        if (selectedVideos.Count == 0)
        {
            _log.Error("Select at least one video with an available source file before converting.");
            return;
        }

        if (!await EnsureRequiredToolsAsync(requireYtDlp: false, requireFfmpeg: true, "変換"))
        {
            return;
        }

        _isConverting = true;
        SetConversionState(isConverting: true);
        _log.Info($"Starting conversion of {selectedVideos.Count} selected item(s) to: {convertedFolder}");

        try
        {
            foreach (var video in selectedVideos)
            {
                var inputFilePath = GetConversionInputFilePath(video);
                if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
                {
                    video.Status = "Skipped";
                    _log.Error($"Skipping {video.Order:000}: source file path is missing for {video.Title}");
                    continue;
                }

                var outputStem = $"{video.Order:000}_{SafeFileName.Create(video.Title, video.VideoId)}";
                var outputFilePath = PathHelper.GetUniqueFilePath(convertedFolder, outputStem, ".mp4");
                LogOutputConflictIfNeeded(convertedFolder, outputStem, ".mp4", outputFilePath);

                video.Status = "Converting";
                _log.Info($"Converting {video.Order:000}: {video.Title}");

                var result = await _videoConversionService.ConvertAsync(
                    inputFilePath,
                    outputFilePath,
                    message => _log.Info(message));

                if (result.IsSuccess)
                {
                    video.ConvertedFilePath = result.OutputFilePath;
                    video.Status = "Converted";
                    _log.Success($"Converted {video.Order:000}: {video.Title}");
                }
                else
                {
                    video.Status = "Convert Failed";
                    _log.Error($"Conversion failed for {video.Order:000}: {video.Title}");
                    LogProcessOutput(result.StandardError, "ffmpeg stderr");
                    LogProcessOutput(result.StandardOutput, "ffmpeg stdout");
                }
            }
        }
        finally
        {
            _isConverting = false;
            SetConversionState(isConverting: false);
            _log.Info("Conversion task finished.");
        }
    }

    private void ConversionQueue_DragOver(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || GetDroppedText(e.Data) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ConversionQueue_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            e.Handled = true;
            AddLocalFilesToQueue(paths, "drag and drop", isSimpleModeItem: SimpleModeCheckBox.IsChecked == true);
            if (SimpleModeCheckBox.IsChecked == true)
            {
                _ = RunSimpleModeQueueAsync();
            }

            return;
        }

        if (GetDroppedText(e.Data) is { } droppedText)
        {
            e.Handled = true;
            await AddDroppedTextUrlsToQueueAsync(droppedText, isSimpleModeItem: SimpleModeCheckBox.IsChecked == true);
            if (SimpleModeCheckBox.IsChecked == true)
            {
                _ = RunSimpleModeQueueAsync();
            }
        }
    }

    private void ConversionQueueDataGrid_PreviewMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        _queueDragStartPoint = e.GetPosition(ConversionQueueDataGrid);
    }

    private void ConversionQueueDataGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_queueDragStartPoint is null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        if (IsQueueProcessingActive())
        {
            _queueDragStartPoint = null;
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        var currentPosition = e.GetPosition(ConversionQueueDataGrid);
        if (Math.Abs(currentPosition.X - _queueDragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - _queueDragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (GetDataGridRowItem(e.OriginalSource as DependencyObject) is not ConversionQueueItem rowItem)
        {
            return;
        }

        _draggedQueueItems = GetQueueDragItems(rowItem);
        if (_draggedQueueItems.Count == 0)
        {
            return;
        }

        if (_draggedQueueItems.Any(IsActiveQueueItem))
        {
            _draggedQueueItems = [];
            _queueDragStartPoint = null;
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        var data = new DataObject();
        data.SetData(QueueReorderDragFormat, true);
        DragDrop.DoDragDrop(ConversionQueueDataGrid, data, DragDropEffects.Move);
        _queueDragStartPoint = null;
    }

    private void ConversionQueueDataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (GetDroppedText(e.Data) is not null)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(QueueReorderDragFormat) && _draggedQueueItems.Count > 0
            && !IsQueueProcessingActive()
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ConversionQueueDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            e.Handled = true;
            AddLocalFilesToQueue(paths, "drag and drop", isSimpleModeItem: SimpleModeCheckBox.IsChecked == true);
            if (SimpleModeCheckBox.IsChecked == true)
            {
                _ = RunSimpleModeQueueAsync();
            }

            return;
        }

        if (GetDroppedText(e.Data) is { } droppedText)
        {
            e.Handled = true;
            await AddDroppedTextUrlsToQueueAsync(droppedText, isSimpleModeItem: SimpleModeCheckBox.IsChecked == true);
            if (SimpleModeCheckBox.IsChecked == true)
            {
                _ = RunSimpleModeQueueAsync();
            }

            return;
        }

        if (!e.Data.GetDataPresent(QueueReorderDragFormat) || _draggedQueueItems.Count == 0)
        {
            return;
        }

        if (IsQueueProcessingActive() || _draggedQueueItems.Any(IsActiveQueueItem))
        {
            _draggedQueueItems = [];
            _log.Info("処理中はキューの並べ替えはできません。");
            e.Handled = true;
            return;
        }

        var targetItem = GetDataGridRowItem(e.OriginalSource as DependencyObject);
        ReorderQueueItemsByDrop(_draggedQueueItems, targetItem);
        _draggedQueueItems = [];
        e.Handled = true;
    }

    private void ConversionQueueDataGrid_Sorting(
        object sender,
        System.Windows.Controls.DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var sortMemberPath = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return;
        }

        var direction = _lastQueueSortMemberPath == sortMemberPath
            && _lastQueueSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

        ApplyOneShotQueueSort(sortMemberPath, direction);
        _lastQueueSortMemberPath = sortMemberPath;
        _lastQueueSortDirection = direction;

        foreach (var column in ConversionQueueDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = direction;
        CollectionViewSource.GetDefaultView(ConversionQueueDataGrid.ItemsSource)?.SortDescriptions.Clear();
        ConversionQueueDataGrid.Items.Refresh();
        _log.Info($"Sorted conversion queue by {e.Column.Header} {direction}.");
    }

    private void ApplyOneShotQueueSort(string sortMemberPath, ListSortDirection direction)
    {
        var sortedItems = _conversionQueue
            .Select((item, index) => new { Item = item, Index = index })
            .OrderBy(entry => GetQueueSortValue(entry.Item, sortMemberPath), QueueSortValueComparer.Instance)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item)
            .ToList();

        if (direction == ListSortDirection.Descending)
        {
            sortedItems.Reverse();
        }

        _conversionQueue.Clear();
        foreach (var item in sortedItems)
        {
            _conversionQueue.Add(item);
        }

        RefreshQueueOrderNumbers();
    }

    private static object? GetQueueSortValue(ConversionQueueItem item, string sortMemberPath)
    {
        return sortMemberPath switch
        {
            nameof(ConversionQueueItem.Order) => item.Order,
            nameof(ConversionQueueItem.SourceType) => item.SourceType,
            nameof(ConversionQueueItem.Title) => item.Title,
            nameof(ConversionQueueItem.SourcePathOrUrl) => item.SourcePathOrUrl,
            nameof(ConversionQueueItem.Status) => item.Status,
            _ => item.Title,
        };
    }

    private sealed class QueueSortValueComparer : IComparer<object?>
    {
        public static QueueSortValueComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            return x is int leftInt && y is int rightInt
                ? leftInt.CompareTo(rightInt)
                : string.Compare(
                    x.ToString(),
                    y.ToString(),
                    CultureInfo.CurrentCulture,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);
        }
    }

    private List<ConversionQueueItem> GetQueueDragItems(ConversionQueueItem rowItem)
    {
        var selectedItems = ConversionQueueDataGrid.SelectedItems
            .OfType<ConversionQueueItem>()
            .Where(item => _conversionQueue.Contains(item))
            .OrderBy(item => _conversionQueue.IndexOf(item))
            .ToList();

        if (selectedItems.Contains(rowItem))
        {
            return selectedItems;
        }

        return [rowItem];
    }

    private void ReorderQueueItemsByDrop(
        List<ConversionQueueItem> draggedItems,
        ConversionQueueItem? targetItem)
    {
        if (IsQueueProcessingActive() || draggedItems.Any(IsActiveQueueItem))
        {
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        var itemsToMove = draggedItems
            .Where(item => _conversionQueue.Contains(item))
            .OrderBy(item => _conversionQueue.IndexOf(item))
            .ToList();
        if (itemsToMove.Count == 0 || (targetItem is not null && itemsToMove.Contains(targetItem)))
        {
            return;
        }

        foreach (var item in itemsToMove)
        {
            _conversionQueue.Remove(item);
        }

        var insertIndex = targetItem is null ? _conversionQueue.Count : _conversionQueue.IndexOf(targetItem);
        if (insertIndex < 0)
        {
            insertIndex = _conversionQueue.Count;
        }

        for (var index = 0; index < itemsToMove.Count; index++)
        {
            _conversionQueue.Insert(insertIndex + index, itemsToMove[index]);
        }

        RefreshQueueOrderNumbers();
        ConversionQueueDataGrid.SelectedItems.Clear();
        foreach (var item in itemsToMove)
        {
            ConversionQueueDataGrid.SelectedItems.Add(item);
        }

        ConversionQueueDataGrid.ScrollIntoView(itemsToMove[0]);
        _log.Info($"Reordered {itemsToMove.Count} queue item(s).");
    }

    private static ConversionQueueItem? GetDataGridRowItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.DataGridRow row
                && row.Item is ConversionQueueItem item)
            {
                return item;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void QueueMoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsQueueProcessingActive())
        {
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItems.Count > 1)
        {
            _log.Info("Move Up supports one selected queue row at a time.");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItem is not ConversionQueueItem selectedItem)
        {
            return;
        }

        if (IsActiveQueueItem(selectedItem))
        {
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        var index = _conversionQueue.IndexOf(selectedItem);
        if (index <= 0)
        {
            return;
        }

        _conversionQueue.Move(index, index - 1);
        RefreshQueueOrderNumbers();
        ConversionQueueDataGrid.SelectedItem = selectedItem;
        ConversionQueueDataGrid.ScrollIntoView(selectedItem);
    }

    private void QueueMoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsQueueProcessingActive())
        {
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItems.Count > 1)
        {
            _log.Info("Move Down supports one selected queue row at a time.");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItem is not ConversionQueueItem selectedItem)
        {
            return;
        }

        if (IsActiveQueueItem(selectedItem))
        {
            _log.Info("処理中はキューの並べ替えはできません。");
            return;
        }

        var index = _conversionQueue.IndexOf(selectedItem);
        if (index < 0 || index >= _conversionQueue.Count - 1)
        {
            return;
        }

        _conversionQueue.Move(index, index + 1);
        RefreshQueueOrderNumbers();
        ConversionQueueDataGrid.SelectedItem = selectedItem;
        ConversionQueueDataGrid.ScrollIntoView(selectedItem);
    }

    private void QueueRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveSelectedQueueItems();
    }

    private void SimpleCancelQueueButton_Click(object sender, RoutedEventArgs e)
    {
        RequestQueueCancellation();
    }

    private void RequestQueueCancellation()
    {
        if (_queueCancellationTokenSource is null || !IsQueueProcessingActive())
        {
            _log.Info("実行中のキュー処理はありません。");
            return;
        }

        _log.Info("Cancel requested. Stopping current queue process...");
        _queueCancellationTokenSource.Cancel();
    }

    private void RemoveSelectedQueueItems()
    {
        var selectedItems = ConversionQueueDataGrid.SelectedItems
            .OfType<ConversionQueueItem>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            return;
        }

        var activeItems = selectedItems.Where(IsActiveQueueItem).ToList();
        if (activeItems.Count > 0)
        {
            _log.Warn("処理中の項目は削除できません。中断する場合はキャンセルを使用してください。");
        }

        var removableItems = selectedItems.Except(activeItems).ToList();
        if (removableItems.Count == 0)
        {
            return;
        }

        foreach (var selectedItem in removableItems)
        {
            _conversionQueue.Remove(selectedItem);
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Removed {removableItems.Count} item(s) from the conversion queue.");
    }

    private void QueueClearButton_Click(object sender, RoutedEventArgs e)
    {
        var activeCount = _conversionQueue.Count(IsActiveQueueItem);
        var removableItems = _conversionQueue.Where(item => !IsActiveQueueItem(item)).ToList();
        if (activeCount > 0)
        {
            _log.Warn("処理中の項目は削除できません。中断する場合はキャンセルを使用してください。");
        }

        if (removableItems.Count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Remove {removableItems.Count} item(s) from the conversion queue?",
            "Clear Queue",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var item in removableItems)
        {
            _conversionQueue.Remove(item);
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Cleared {removableItems.Count} item(s) from the conversion queue.");
    }

    private void ConversionQueueDataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete)
        {
            return;
        }

        RemoveSelectedQueueItems();
        e.Handled = true;
    }

    private void ConversionQueueDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete)
        {
            return;
        }

        RemoveSelectedQueueItems();
        e.Handled = true;
    }

    private async void ConvertQueueButton_Click(object sender, RoutedEventArgs e)
    {
        ConversionQueueDataGrid.CommitEdit();
        ConversionQueueDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var queueItems = _conversionQueue
            .OrderBy(static item => item.Order)
            .ToList();

        await RunQueueItemsAsync(queueItems, "Add at least one item to the conversion queue before running it.");
    }

    private async void RetryFailedQueueButton_Click(object sender, RoutedEventArgs e)
    {
        ConversionQueueDataGrid.CommitEdit();
        ConversionQueueDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var failedItems = _conversionQueue
            .Where(static item => item.Status == "Failed")
            .OrderBy(static item => item.Order)
            .ToList();

        await RunQueueItemsAsync(failedItems, "No failed queue items to retry.");
    }

    private async Task RunQueueItemsAsync(List<ConversionQueueItem> selectedItems, string emptyQueueMessage)
    {
        if (selectedItems.Count == 0)
        {
            _log.Error(emptyQueueMessage);
            return;
        }

        var executionMode = GetQueueExecutionMode();
        if (!EnsureFoldersForQueueMode(executionMode))
        {
            return;
        }

        var requiresYtDlp = executionMode is "Download Only" or "Download & Convert";
        var requiresFfmpeg = executionMode is "Download & Convert" or "Convert Only";
        if (!await EnsureRequiredToolsAsync(requiresYtDlp, requiresFfmpeg, "キュー実行"))
        {
            _log.Warn("必要な外部ツールが不足しているため、キュー実行を開始しませんでした。");
            return;
        }

        _isQueueConverting = true;
        _queueCancellationTokenSource = new CancellationTokenSource();
        var selectedPreset = GetSelectedConversionPreset();
        var downloadProfile = RequiresDownloadProfile(executionMode)
            ? ResolveDownloadProfile(executionMode, selectedPreset)
            : null;
        var convertedOutputStartNumber = GetNumberPrefixStartNumber();
        _activeNumberPrefixStartNumber = convertedOutputStartNumber;
        UpdateQueueUnsupportedStatusesForCurrentMode();
        foreach (var item in selectedItems)
        {
            if (item.IsUnsupported)
            {
                continue;
            }

            ClearQueueProgress(item);
            item.Status = "Pending";
        }

        _activeQueueProgressItems = selectedItems;
        ResetQueueProgress(selectedItems.Count);
        SetQueueConversionState(isConverting: true);
        _log.Info($"Starting queue. Mode: {executionMode}. {selectedItems.Count} selected item(s).");
        _log.Info($"Output preset: {selectedPreset.DisplayName}");
        _log.Info($"Converted output extension: {selectedPreset.ContainerExtension}");
        if (selectedPreset.SupportsAspectMode)
        {
            _log.Info($"Aspect mode: {GetSelectedAspectMode()}");
        }
        else if (selectedPreset.IsAudioOnlyPreset)
        {
            _log.Info("Aspect mode: not applied to audio output presets.");
        }
        else
        {
            _log.Info("Aspect mode: not applied to Current Compatibility preset.");
        }

        if (selectedPreset.IsAudioOnlyPreset)
        {
            _log.Info("Audio output preset selected. ffmpeg will disable video output with -vn.");
        }

        if (IsPortableDvdPreset(selectedPreset.DisplayName))
        {
            _log.Info("DVD presets use fixed MP2 audio: -c:a mp2 -b:a 192k -ar 48000 -ac 2");
        }

        if (selectedPreset.Id == ConversionPresetCatalog.CurrentCompatibilityId)
        {
            _log.Info("Car Navi MP4 - Current Compatibility is retained as a known-working fallback preset.");
        }

        _log.Info($"Keep original downloaded files: {KeepOriginalDownloadedFilesCheckBox.IsChecked == true}");
        _log.Info($"Number prefix: {(convertedOutputStartNumber is null ? "Off" : convertedOutputStartNumber.Value.ToString("000", CultureInfo.InvariantCulture))}");
        if (downloadProfile is not null)
        {
            LogDownloadProfileSelection(downloadProfile);
        }

        if (convertedOutputStartNumber is not null)
        {
            _log.Info($"Converted output start number: {convertedOutputStartNumber.Value:000}");
        }

        if (executionMode == "Copy Files")
        {
            _log.Info($"Copy Files selected output folder: {GetBaseOutputFolder()}");
        }

        _log.Info($"Preset subfolder output: {_settings.CreateSubfolderPerOutputPreset}");
        if (executionMode == "Download Only")
        {
            _log.Info("Audio adjustment: not applied in Download Only mode.");
        }
        else
        {
            _log.Info($"Global audio normalization: {(PeakBoostCheckBox.IsChecked == true ? "On" : "Off")}");
            if (PeakBoostCheckBox.IsChecked == true)
            {
                var normalizationMode = GetSelectedAudioNormalizationMode();
                _log.Info($"Audio normalization mode: {normalizationMode}");
                if (normalizationMode == AudioNormalizationMode.Peak)
                {
                    _log.Info($"Peak Boost target peak: {GetSelectedTargetPeakDb():0.0} dBFS");
                }
                else
                {
                    var options = GetReplayGainNormalizationOptions();
                    _log.Info($"ReplayGain target normalization: {options.TargetReplayGainVolumeDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
                    _log.Info($"ReplayGain peak limit: {options.PeakLimitDb.ToString("0.0", CultureInfo.InvariantCulture)} dBFS");
                    _log.Info($"ReplayGain tolerance: {options.ToleranceDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
                    _log.Info($"ReplayGain maximum gain: {options.MaximumGainDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
                }
            }

            _log.Info("Per-item Loudness Normalize overrides global audio normalization.");
        }

        try
        {
            var outputOrder = convertedOutputStartNumber is not null
                ? convertedOutputStartNumber.Value
                : 1;
            foreach (var item in selectedItems)
            {
                if (_queueCancellationTokenSource.IsCancellationRequested)
                {
                    MarkRemainingQueueItemsAsSkipped(selectedItems, item);
                    RefreshQueueProgressFromStatuses(selectedItems);
                    _log.Info("Queue canceled.");
                    break;
                }

                _log.Info($"Queue item start {item.Order:000}: {item.Title}");
                if (item.IsUnsupported)
                {
                    _log.Info($"Skipping {item.Order:000}: {item.UnsupportedReason}");
                    RefreshQueueProgressFromStatuses(selectedItems);
                    continue;
                }

                if (!EnsureQueueUrlIsSafe(item))
                {
                    RefreshQueueProgressFromStatuses(selectedItems);
                    continue;
                }

                var existingResultState = ReconcileResult(item, outputOrder);
                if (existingResultState is PlaylistResultState.Available or PlaylistResultState.SequenceOutOfSync)
                {
                    item.Status = "Completed";
                    _log.Success($"{item.Order:000}: 出力済みのため処理を省略しました");
                    outputOrder++;
                    RefreshQueueProgressFromStatuses(selectedItems);
                    continue;
                }

                switch (executionMode)
                {
                    case "Download Only":
                        await RunDownloadOnlyQueueItemAsync(item, outputOrder, downloadProfile!, _queueCancellationTokenSource.Token);
                        if (item.SourceType == "OnlineVideo")
                        {
                            outputOrder++;
                        }

                        break;
                    case "Copy Files":
                        await RunCopyFilesQueueItemAsync(item, outputOrder);
                        if (item.SourceType == "LocalFile")
                        {
                            outputOrder++;
                        }

                        break;
                    case "Convert Only":
                        await RunConvertOnlyQueueItemAsync(item, outputOrder, selectedPreset, _queueCancellationTokenSource.Token);
                        if (item.SourceType == "LocalFile")
                        {
                            outputOrder++;
                        }

                        break;
                    default:
                        await RunDownloadAndConvertQueueItemAsync(item, outputOrder, selectedPreset, downloadProfile!, _queueCancellationTokenSource.Token);
                        if (item.SourceType is "OnlineVideo" or "LocalFile")
                        {
                            outputOrder++;
                        }

                        break;
                }

                _log.Info($"Queue item finish {item.Order:000}: {item.Title} - {item.Status}");
                RefreshQueueProgressFromStatuses(selectedItems);
            }
        }
        finally
        {
            _queueCancellationTokenSource?.Dispose();
            _queueCancellationTokenSource = null;
            _activeNumberPrefixStartNumber = null;
            _activeQueueProgressItems = [];
            _isQueueConverting = false;
            SetQueueConversionState(isConverting: false);
            _log.Info("Queue task finished.");
        }
    }

    private async Task RunSimpleModeQueueAsync()
    {
        if (_isSimpleModeRunning || _isQueueConverting)
        {
            return;
        }

        _isSimpleModeRunning = true;
        _isQueueConverting = true;
        _queueCancellationTokenSource = new CancellationTokenSource();
        SetQueueConversionState(isConverting: true);
        SimpleModeStatusTextBlock.Text = "Simple Mode 処理中...";

        try
        {
            Directory.CreateDirectory(_settings.TemporaryFolder);
            Directory.CreateDirectory(_settings.ConvertedFolder);

            while (true)
            {
                var pendingItems = _conversionQueue
                    .Where(static item => item.IsSimpleModeItem && IsSimpleModeProcessableStatus(item.Status))
                    .OrderBy(static item => item.Order)
                    .ToList();
                if (pendingItems.Count == 0)
                {
                    break;
                }

                var requiresYtDlp = pendingItems.Any(static item => item.SourceType == "OnlineVideo");
                if (!await EnsureRequiredToolsAsync(requiresYtDlp, requireFfmpeg: true, "Simple Mode"))
                {
                    SimpleModeStatusTextBlock.Text = "必要な外部ツールが不足しています";
                    break;
                }

                var preset = GetSelectedConversionPreset();
                var downloadProfile = ResolveDownloadProfile("Download & Convert", preset);
                _activeNumberPrefixStartNumber = null;
                _activeQueueProgressItems = pendingItems;
                ResetQueueProgress(pendingItems.Count);
                _log.Info($"Simple Mode started. {pendingItems.Count} item(s). Preset: {preset.DisplayName}");

                var outputOrder = 1;
                foreach (var item in pendingItems)
                {
                    if (_queueCancellationTokenSource.IsCancellationRequested)
                    {
                        MarkRemainingQueueItemsAsSkipped(pendingItems, item);
                        SimpleModeStatusTextBlock.Text = "Simple Mode キャンセル";
                        break;
                    }

                    ApplySimpleModeSupportStatus(item);
                    if (item.IsUnsupported)
                    {
                        _log.Info($"Simple Mode skipping {item.Order:000}: {item.UnsupportedReason}");
                        RefreshQueueProgressFromStatuses(pendingItems);
                        continue;
                    }

                    if (!EnsureQueueUrlIsSafe(item))
                    {
                        RefreshQueueProgressFromStatuses(pendingItems);
                        continue;
                    }

                    var existingResultState = ReconcileResult(item, outputOrder);
                    if (existingResultState is PlaylistResultState.Available or PlaylistResultState.SequenceOutOfSync)
                    {
                        item.Status = "Completed";
                        _log.Success($"{item.Order:000}: 出力済みのため処理を省略しました");
                        outputOrder++;
                        RefreshQueueProgressFromStatuses(pendingItems);
                        continue;
                    }

                    SimpleModeStatusTextBlock.Text = $"処理中: {item.Title}";
                    ClearQueueProgress(item);
                    item.Status = "Pending";

                    if (item.SourceType == "OnlineVideo")
                    {
                        await RunSimpleModeOnlineItemAsync(item, outputOrder, preset, downloadProfile, _queueCancellationTokenSource.Token);
                    }
                    else if (item.SourceType == "LocalFile")
                    {
                        await ConvertQueueItemAsync(item, item.SourcePathOrUrl, outputOrder, preset, _queueCancellationTokenSource.Token);
                    }

                    outputOrder++;
                    RefreshQueueProgressFromStatuses(pendingItems);
                }
            }
        }
        finally
        {
            _queueCancellationTokenSource?.Dispose();
            _queueCancellationTokenSource = null;
            _activeNumberPrefixStartNumber = null;
            _activeQueueProgressItems = [];
            _isQueueConverting = false;
            _isSimpleModeRunning = false;
            SetQueueConversionState(isConverting: false);
            UpdateSimpleModeStatus();
            _log.Info("Simple Mode task finished.");
        }
    }

    private async Task RunSimpleModeOnlineItemAsync(
        ConversionQueueItem item,
        int outputOrder,
        ConversionPreset preset,
        DownloadProfileOption downloadProfile,
        CancellationToken cancellationToken)
    {
        var downloadResult = await DownloadQueueItemAsync(
            item,
            _settings.TemporaryFolder,
            outputOrder,
            addNumberPrefix: false,
            downloadProfile: downloadProfile,
            cleanupFailedDownloadArtifacts: true,
            cancellationToken);

        try
        {
            if (downloadResult.IsCanceled)
            {
                ClearQueueProgress(item);
                item.Status = "Skipped";
                return;
            }

            if (!downloadResult.IsSuccess || string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
            {
                ClearQueueProgress(item);
                item.Status = "Failed";
                _log.Error($"Simple Mode download failed for queue item {item.Order:000}: {item.Title}");
                LogProcessOutput(downloadResult.StandardError, "yt-dlp stderr");
                LogProcessOutput(downloadResult.StandardOutput, "yt-dlp stdout");
                return;
            }

            item.DownloadedFilePath = downloadResult.DownloadedFilePath;
            ClearQueueProgress(item);
            item.Status = "Downloaded";
            _log.Info($"Simple Mode temporary download path: {item.DownloadedFilePath}");
            await ConvertQueueItemAsync(item, item.DownloadedFilePath, outputOrder, preset, cancellationToken);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
            {
                DeleteTemporaryDownload(downloadResult.DownloadedFilePath);
            }
        }
    }

    private static bool IsSimpleModeProcessableStatus(string status)
    {
        return status is "Pending" or QueueStatusReady or QueueStatusReadyWithWarning;
    }

    private void CancelQueueButton_Click(object sender, RoutedEventArgs e)
    {
        RequestQueueCancellation();
    }

    private async Task RunCopyFilesQueueItemAsync(ConversionQueueItem item, int outputOrder)
    {
        if (item.SourceType == "OnlineVideo")
        {
            item.Status = "Skipped";
            _log.Info($"Skipping {item.Order:000}: Online videos are skipped in Copy Files mode.");
            return;
        }

        if (item.SourceType != "LocalFile")
        {
            item.Status = "Skipped";
            _log.Error($"Skipping {item.Order:000}: unsupported queue source type for Copy Files mode: {item.SourceType}");
            return;
        }

        var sourcePath = item.SourcePathOrUrl;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            item.Status = "Failed";
            _log.Error($"Copy failed for queue item {item.Order:000}: source file is missing: {sourcePath}");
            return;
        }

        try
        {
            SetQueueProgress(item, "コピー中", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);
            var outputFolder = GetBaseOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var safeTitle = SafeFileName.Create(item.Title, Path.GetFileNameWithoutExtension(sourcePath));
            var extension = Path.GetExtension(sourcePath);
            var outputStem = _activeNumberPrefixStartNumber is not null
                ? $"{outputOrder:000}_{safeTitle}"
                : safeTitle;
            var destinationPath = _playlistResultService.ResolveCollisionSafePath(item, outputFolder, outputStem, extension);
            LogOutputConflictIfNeeded(outputFolder, outputStem, extension, destinationPath);
            var copyOutputPath = _playlistResultService.CanReuseRecordedResultPath(item, destinationPath)
                ? CreateReplacementTemporaryPath(destinationPath)
                : destinationPath;

            _log.Info($"Copy Files output folder: {outputFolder}");
            _log.Info($"Copy source path: {sourcePath}");
            try
            {
                await CopyFileWithProgressAsync(item, sourcePath, copyOutputPath);
                ReplaceTrackedResultIfNeeded(copyOutputPath, destinationPath);
            }
            catch
            {
                DeleteReplacementTemporaryFile(copyOutputPath, destinationPath);
                throw;
            }
            item.ConvertedFilePath = destinationPath;
            RecordSuccessfulResult(item, destinationPath, outputOrder);
            ClearQueueProgress(item);
            item.Status = "Completed";
            _log.Info($"Copy destination path: {destinationPath}");
        }
        catch (Exception ex)
        {
            ClearQueueProgress(item);
            item.Status = "Failed";
            _log.Error($"Copy failed for queue item {item.Order:000}: {item.Title}. {ex.Message}");
        }
    }

    private async Task RunDownloadOnlyQueueItemAsync(
        ConversionQueueItem item,
        int outputOrder,
        DownloadProfileOption downloadProfile,
        CancellationToken cancellationToken)
    {
        if (item.SourceType == "LocalFile")
        {
            item.Status = "Skipped";
            _log.Info($"Skipping {item.Order:000}: Local files are skipped in Download Only mode.");
            return;
        }

        if (item.SourceType != "OnlineVideo")
        {
            item.Status = "Skipped";
            _log.Error($"Skipping {item.Order:000}: unsupported queue source type: {item.SourceType}");
            return;
        }

        var result = await DownloadQueueItemAsync(
            item,
            _settings.WorkingFolder,
            outputOrder,
            addNumberPrefix: false,
            downloadProfile: downloadProfile,
            cleanupFailedDownloadArtifacts: false,
            cancellationToken);
        if (result.IsCanceled)
        {
            ClearQueueProgress(item);
            item.Status = "Skipped";
            return;
        }

        if (result.IsSuccess)
        {
            item.DownloadedFilePath = result.DownloadedFilePath ?? string.Empty;
            ClearQueueProgress(item);
            item.Status = "Completed";
            if (!string.IsNullOrWhiteSpace(item.DownloadedFilePath))
            {
                RecordSuccessfulResult(item, item.DownloadedFilePath, outputOrder);
            }
            _log.Info($"Download output path: {item.DownloadedFilePath}");
            return;
        }

        ClearQueueProgress(item);
        item.Status = "Failed";
        _log.Error($"Download failed for queue item {item.Order:000}: {item.Title}");
        LogProcessOutput(result.StandardError, "yt-dlp stderr");
        LogProcessOutput(result.StandardOutput, "yt-dlp stdout");
    }

    private async Task RunConvertOnlyQueueItemAsync(
        ConversionQueueItem item,
        int outputOrder,
        ConversionPreset preset,
        CancellationToken cancellationToken)
    {
        if (item.SourceType == "OnlineVideo")
        {
            item.Status = "Skipped";
            _log.Info($"Skipping {item.Order:000}: Online videos are skipped in Convert Only mode. Use Download & Convert instead.");
            return;
        }

        if (item.SourceType != "LocalFile")
        {
            item.Status = "Skipped";
            _log.Error($"Skipping {item.Order:000}: unsupported queue source type: {item.SourceType}");
            return;
        }

        await ConvertQueueItemAsync(item, item.SourcePathOrUrl, outputOrder, preset, cancellationToken);
    }

    private async Task RunDownloadAndConvertQueueItemAsync(
        ConversionQueueItem item,
        int outputOrder,
        ConversionPreset preset,
        DownloadProfileOption downloadProfile,
        CancellationToken cancellationToken)
    {
        if (item.SourceType == "LocalFile")
        {
            await ConvertQueueItemAsync(item, item.SourcePathOrUrl, outputOrder, preset, cancellationToken);
            return;
        }

        if (item.SourceType != "OnlineVideo")
        {
            item.Status = "Skipped";
            _log.Error($"Skipping {item.Order:000}: unsupported queue source type: {item.SourceType}");
            return;
        }

        var keepOriginalDownloadedFile = KeepOriginalDownloadedFilesCheckBox.IsChecked == true;
        var downloadFolder = keepOriginalDownloadedFile
            ? _settings.WorkingFolder
            : _settings.TemporaryFolder;
        _log.Info(keepOriginalDownloadedFile
            ? $"Original downloaded files will be kept in: {downloadFolder}"
            : $"Original downloaded files are temporary and will be downloaded to: {downloadFolder}");

        var downloadResult = await DownloadQueueItemAsync(
            item,
            downloadFolder,
            outputOrder,
            addNumberPrefix: false,
            downloadProfile: downloadProfile,
            cleanupFailedDownloadArtifacts: !keepOriginalDownloadedFile,
            cancellationToken);
        if (downloadResult.IsCanceled)
        {
            ClearQueueProgress(item);
            item.Status = "Skipped";
            if (!keepOriginalDownloadedFile && !string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
            {
                DeleteTemporaryDownload(downloadResult.DownloadedFilePath);
            }

            return;
        }

        if (!downloadResult.IsSuccess || string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
        {
            ClearQueueProgress(item);
            item.Status = "Failed";
            _log.Error($"Download failed for queue item {item.Order:000}: {item.Title}");
            LogProcessOutput(downloadResult.StandardError, "yt-dlp stderr");
            LogProcessOutput(downloadResult.StandardOutput, "yt-dlp stdout");
            if (!keepOriginalDownloadedFile && !string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
            {
                DeleteTemporaryDownload(downloadResult.DownloadedFilePath);
            }

            return;
        }

        item.DownloadedFilePath = downloadResult.DownloadedFilePath;
        ClearQueueProgress(item);
        item.Status = "Downloaded";
        _log.Info(keepOriginalDownloadedFile
            ? $"Original download path: {item.DownloadedFilePath}"
            : $"Temporary download path: {item.DownloadedFilePath}");

        await ConvertQueueItemAsync(item, item.DownloadedFilePath, outputOrder, preset, cancellationToken);

        if (!keepOriginalDownloadedFile)
        {
            DeleteTemporaryDownload(item.DownloadedFilePath);
        }
    }

    private async Task<VideoDownloadResult> DownloadQueueItemAsync(
        ConversionQueueItem item,
        string outputFolder,
        int outputOrder,
        bool addNumberPrefix,
        DownloadProfileOption downloadProfile,
        bool cleanupFailedDownloadArtifacts,
        CancellationToken cancellationToken)
    {
        SetQueueProgress(item, "ダウンロード中", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);
        _log.Info($"Downloading queue item {outputOrder:000}: {item.Title}");
        _log.Info($"Download destination folder: {outputFolder}");

        var downloadItem = CreateDownloadVideoListItem(item);
        VideoDownloadResult? lastResult = null;
        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new VideoDownloadResult(false, true, null, string.Empty, "Download canceled.", null);
            }

            SetQueueProgress(item, $"ダウンロード中 (試行 {attempt}/{MaxDownloadAttempts})", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);
            _log.Info($"yt-dlp download attempt {attempt}/{MaxDownloadAttempts} for: {item.Title}");

            lastResult = await _videoDownloadService.DownloadAsync(
                downloadItem,
                outputFolder,
                outputOrder,
                message => _log.Info(message),
                downloadProfile,
                addNumberPrefix,
                deterministicCollisionSuffix: _playlistResultService.GetStableCollisionSuffix(item),
                progress: CreateQueueDownloadProgressHandler(item),
                cancellationToken: cancellationToken);

            _log.Info($"yt-dlp attempt {attempt}/{MaxDownloadAttempts} finished for {item.Title}. Exit code: {lastResult.ExitCode?.ToString() ?? "unknown"}");

            if (lastResult.StandardError.Contains("所有者不明ファイルと競合", StringComparison.Ordinal))
            {
                item.ResultState = PlaylistResultState.NameConflict;
                item.ResultStateReason = lastResult.StandardError;
                return lastResult;
            }

            if (lastResult.IsSuccess || lastResult.IsCanceled)
            {
                return lastResult;
            }

            if (cleanupFailedDownloadArtifacts && !string.IsNullOrWhiteSpace(lastResult.DownloadedFilePath))
            {
                DeleteTemporaryDownload(lastResult.DownloadedFilePath);
            }

            if (attempt >= MaxDownloadAttempts)
            {
                break;
            }

            var retryDelay = DownloadRetryDelays[Math.Min(attempt - 1, DownloadRetryDelays.Length - 1)];
            _log.Error($"yt-dlp attempt {attempt}/{MaxDownloadAttempts} failed for {item.Title}. Exit code: {lastResult.ExitCode?.ToString() ?? "unknown"}.");
            _log.Warn($"Retrying {item.Title} after {retryDelay.TotalSeconds:0} second(s).");

            try
            {
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new VideoDownloadResult(
                    false,
                    true,
                    null,
                    lastResult.StandardOutput,
                    lastResult.StandardError,
                    lastResult.ExitCode);
            }
        }

        if (lastResult is not null)
        {
            var finalReason = string.IsNullOrWhiteSpace(lastResult.StandardError)
                ? lastResult.StandardOutput.Trim()
                : lastResult.StandardError.Trim();
            _log.Error($"Final yt-dlp failure for {item.Title}. Exit code: {lastResult.ExitCode?.ToString() ?? "unknown"}. {finalReason}");
            return lastResult;
        }

        _log.Error($"Final yt-dlp failure for {item.Title}. No download attempts completed.");
        return new VideoDownloadResult(false, false, null, string.Empty, "No download attempts completed.", null);
    }

    private bool EnsureQueueUrlIsSafe(ConversionQueueItem item)
    {
        if (item.SourceType != "OnlineVideo")
        {
            return true;
        }

        var validation = NormalizeSingleVideoUrl(item.SourcePathOrUrl);
        if (validation.IsAllowed && string.Equals(validation.Url, item.SourcePathOrUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        ClearQueueProgress(item);
        item.Status = "Skipped";
        item.UnsupportedReason = validation.IsAllowed
            ? "URLの正規化が必要です。キューへ追加し直してください。"
            : validation.Reason;
        _log.Error($"Skipping {item.Order:000}: {item.UnsupportedReason} URL: {item.SourcePathOrUrl}");
        return false;
    }

    private async Task ConvertQueueItemAsync(
        ConversionQueueItem item,
        string inputFilePath,
        int outputOrder,
        ConversionPreset preset,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
        {
            ClearQueueProgress(item);
            item.Status = "Failed";
            _log.Error($"Queue item {item.Order:000} source file is missing: {inputFilePath}");
            return;
        }

        var safeTitle = SafeFileName.Create(item.Title, Path.GetFileNameWithoutExtension(inputFilePath));
        var isAudioOnlyInput = item.SourceType == "LocalFile" && IsSupportedLocalAudioFile(inputFilePath);
        var useAudioOutputPreset = preset.IsAudioOnlyPreset;
        var outputStem = _activeNumberPrefixStartNumber is not null
            ? $"{outputOrder:000}_{safeTitle}"
            : safeTitle;
        var outputFolder = useAudioOutputPreset
            ? GetConvertedOutputFolder(preset)
            : isAudioOnlyInput
            ? GetConvertedOutputFolder(AudioOnlyPresetFolderName)
            : GetConvertedOutputFolder(preset);
        var outputExtension = useAudioOutputPreset || !isAudioOnlyInput
            ? preset.ContainerExtension
            : ".mp4";
        string outputFilePath;
        try
        {
            outputFilePath = _playlistResultService.ResolveCollisionSafePath(item, outputFolder, outputStem, outputExtension);
        }
        catch (IOException ex)
        {
            ClearQueueProgress(item);
            item.Status = "Failed";
            _log.Error($"Output name conflict for queue item {item.Order:000}: {ex.Message}");
            return;
        }
        LogOutputConflictIfNeeded(outputFolder, outputStem, outputExtension, outputFilePath);
        var conversionOutputPath = _playlistResultService.CanReuseRecordedResultPath(item, outputFilePath)
            ? CreateReplacementTemporaryPath(outputFilePath)
            : outputFilePath;

        _log.Info($"Converting queue item {outputOrder:000}: {item.Title}");
        _log.Info($"Preset subfolder output enabled: {_settings.CreateSubfolderPerOutputPreset}");
        _log.Info($"Final converted output folder: {outputFolder}");
        if (useAudioOutputPreset)
        {
            _log.Info($"Audio output preset selected: {preset.DisplayName}");
            _log.Info("Using audio-only output. Video output is disabled with -vn.");
        }
        else if (isAudioOnlyInput)
        {
            _log.Info($"Audio-only input detected: {inputFilePath}");
            _log.Info("Using audio-only MP4 output: AAC 256k, 48000 Hz, stereo, faststart, no video track.");
        }

        var audioFilter = await BuildAudioFilterAsync(item, inputFilePath, cancellationToken);
        if (audioFilter is null)
        {
            ClearQueueProgress(item);
            item.Status = cancellationToken.IsCancellationRequested ? "Skipped" : "Failed";
            return;
        }

        SetQueueProgress(item, "変換中...", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);

        var result = useAudioOutputPreset
            ? await _videoConversionService.ConvertAudioPresetAsync(
                inputFilePath,
                conversionOutputPath,
                message => _log.Info(message),
                preset,
                audioFilter,
                CreateQueueConversionProgressHandler(item),
                cancellationToken)
            : isAudioOnlyInput
            ? await _videoConversionService.ConvertAudioOnlyMp4Async(
                inputFilePath,
                conversionOutputPath,
                message => _log.Info(message),
                audioFilter,
                CreateQueueConversionProgressHandler(item),
                cancellationToken)
            : await _videoConversionService.ConvertAsync(
                inputFilePath,
                conversionOutputPath,
                message => _log.Info(message),
                preset,
                GetSelectedAspectMode(),
                audioFilter,
                CreateQueueConversionProgressHandler(item),
                cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            DeleteReplacementTemporaryFile(conversionOutputPath, outputFilePath);
            ClearQueueProgress(item);
            item.Status = "Skipped";
            _log.Info($"Queue conversion canceled for {item.Order:000}: {item.Title}");
            return;
        }

        if (result.IsSuccess)
        {
            try
            {
                ReplaceTrackedResultIfNeeded(conversionOutputPath, outputFilePath);
            }
            catch (Exception ex)
            {
                DeleteReplacementTemporaryFile(conversionOutputPath, outputFilePath);
                ClearQueueProgress(item);
                item.Status = "Failed";
                _log.Error($"既存の処理結果を置き換えられませんでした: {ex.Message}");
                return;
            }
            item.ConvertedFilePath = outputFilePath;
            RecordSuccessfulResult(item, outputFilePath, outputOrder);
            SetQueueProgress(item, "100%", 100, string.Empty, string.Empty, string.Empty, isIndeterminate: false);
            ClearQueueProgress(item);
            item.Status = "Converted";
            _log.Info($"Conversion output path: {outputFilePath}");
            return;
        }

        DeleteReplacementTemporaryFile(conversionOutputPath, outputFilePath);
        ClearQueueProgress(item);
        item.Status = "Failed";
        _log.Error($"Queue conversion failed for {item.Order:000}: {item.Title}");
        LogProcessOutput(result.StandardError, "ffmpeg stderr");
        LogProcessOutput(result.StandardOutput, "ffmpeg stdout");
    }

    private Action<FfmpegProgressInfo> CreateQueueConversionProgressHandler(ConversionQueueItem item)
    {
        return progress =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (item.Status is "Converted" or "Failed" or "Skipped")
                {
                    return;
                }

                ApplyConversionProgress(item, progress);
            });
        };
    }

    private Action<DownloadProgressInfo> CreateQueueDownloadProgressHandler(ConversionQueueItem item)
    {
        return progress =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (item.Status is "Downloaded" or "Failed" or "Skipped")
                {
                    return;
                }

                var progressText = progress.Percent is { } percent
                    ? $"{Math.Clamp(percent, 0, 100):0.0}%"
                    : string.Empty;
                var detailText = string.IsNullOrWhiteSpace(progress.Detail) ? string.Empty : progress.Detail;
                var speedText = string.IsNullOrWhiteSpace(progress.Speed) ? string.Empty : progress.Speed;
                var etaText = string.IsNullOrWhiteSpace(progress.Eta) ? string.Empty : $"残り {progress.Eta}";
                SetQueueProgress(item, "ダウンロード中", progress.Percent, progressText, detailText, speedText, etaText, progress.Percent is null);
            });
        };
    }

    private static void ApplyConversionProgress(ConversionQueueItem item, FfmpegProgressInfo progress)
    {
        if (progress.ConvertedTime is null)
        {
            SetQueueProgress(item, "変換中", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);
            return;
        }

        var convertedText = FormatProgressTime(progress.ConvertedTime.Value);
        var speedText = string.IsNullOrWhiteSpace(progress.Speed) || progress.Speed.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"speed {progress.Speed}";

        if (progress.TotalDuration is not { TotalSeconds: > 0 } totalDuration)
        {
            SetQueueProgress(item, "変換中", null, string.Empty, convertedText, speedText, string.Empty, isIndeterminate: true);
            return;
        }

        var boundedConvertedSeconds = Math.Clamp(progress.ConvertedTime.Value.TotalSeconds, 0, totalDuration.TotalSeconds);
        var percent = Math.Clamp(
            Math.Round(boundedConvertedSeconds * 100.0 / totalDuration.TotalSeconds),
            0,
            progress.IsComplete ? 100 : 99);
        var totalText = FormatProgressTime(totalDuration);
        var etaText = TryGetProgressSpeed(progress.Speed, out var speed)
            ? FormatEta(totalDuration.TotalSeconds - boundedConvertedSeconds, speed)
            : string.Empty;

        SetQueueProgress(
            item,
            "変換中",
            percent,
            $"{percent:0}%",
            $"{convertedText} / {totalText}",
            speedText,
            etaText,
            isIndeterminate: false);
    }

    private static void SetQueueProgress(
        ConversionQueueItem item,
        string status,
        double? percent,
        string progressText,
        string detailText,
        string speedText,
        string etaText = "",
        bool isIndeterminate = false)
    {
        item.Status = status;
        item.ProgressPercent = percent;
        item.ProgressText = progressText;
        item.DetailText = detailText;
        item.SpeedText = speedText;
        item.EtaText = etaText;
        item.IsIndeterminate = isIndeterminate;
    }

    private static void ClearQueueProgress(ConversionQueueItem item)
    {
        item.ProgressPercent = null;
        item.ProgressText = string.Empty;
        item.DetailText = string.Empty;
        item.SpeedText = string.Empty;
        item.EtaText = string.Empty;
        item.IsIndeterminate = false;
    }

    private static async Task CopyFileWithProgressAsync(
        ConversionQueueItem item,
        string sourcePath,
        string destinationPath)
    {
        const int bufferSize = 1024 * 1024;
        var totalBytes = new FileInfo(sourcePath).Length;
        var copiedBytes = 0L;
        var buffer = new byte[bufferSize];

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

        while (true)
        {
            var read = await source.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read));
            copiedBytes += read;
            if (totalBytes > 0)
            {
                var percent = Math.Clamp(copiedBytes * 100.0 / totalBytes, 0, 100);
                SetQueueProgress(
                    item,
                    "コピー中",
                    percent,
                    $"{percent:0}%",
                    $"{FormatBytes(copiedBytes)} / {FormatBytes(totalBytes)}",
                    string.Empty,
                    string.Empty,
                    isIndeterminate: false);
            }
        }
    }

    private static string CreateReplacementTemporaryPath(string destinationPath)
    {
        var folder = Path.GetDirectoryName(destinationPath)!;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);
        return Path.Combine(folder, $".{stem}.{Guid.NewGuid():N}.nmm-replace{extension}");
    }

    private static void ReplaceTrackedResultIfNeeded(string outputPath, string destinationPath)
    {
        if (string.Equals(outputPath, destinationPath, StringComparison.OrdinalIgnoreCase)) return;
        File.Replace(outputPath, destinationPath, null);
    }

    private static void DeleteReplacementTemporaryFile(string outputPath, string destinationPath)
    {
        if (string.Equals(outputPath, destinationPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(outputPath)) return;
        try { File.Delete(outputPath); } catch { }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#}{units[unitIndex]}";
    }

    private static string FormatProgressTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static bool TryGetProgressSpeed(string speedText, out double speed)
    {
        speed = 0;
        if (string.IsNullOrWhiteSpace(speedText))
        {
            return false;
        }

        var normalizedSpeed = speedText.Trim().TrimEnd('x');
        return double.TryParse(normalizedSpeed, NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
            && speed > 0.01;
    }

    private static string FormatEta(double remainingSeconds, double speed)
    {
        if (remainingSeconds <= 0)
        {
            return "残り 約0分";
        }

        var eta = TimeSpan.FromSeconds(remainingSeconds / speed);
        if (eta.TotalHours >= 1)
        {
            return $"残り 約{Math.Ceiling(eta.TotalHours):0}時間";
        }

        return $"残り 約{Math.Max(1, Math.Ceiling(eta.TotalMinutes)):0}分";
    }

    private bool EnsureFoldersForQueueMode(string executionMode)
    {
        try
        {
            if (executionMode == "Download Only")
            {
                Directory.CreateDirectory(_settings.WorkingFolder);
            }
            else if (executionMode == "Copy Files")
            {
                Directory.CreateDirectory(GetBaseOutputFolder());
            }
            else
            {
                Directory.CreateDirectory(GetBaseOutputFolder());
                if (executionMode == "Download & Convert")
                {
                    var downloadFolder = KeepOriginalDownloadedFilesCheckBox.IsChecked == true
                        ? _settings.WorkingFolder
                        : _settings.TemporaryFolder;
                    Directory.CreateDirectory(downloadFolder);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Required queue folder could not be created. {ex.Message}");
            return false;
        }
    }

    private string GetQueueExecutionMode()
    {
        if (QueueExecutionModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && item.Tag is string mode)
        {
            return mode;
        }

        return "Download & Convert";
    }

    private static bool RequiresDownloadProfile(string executionMode)
    {
        return executionMode is "Download Only" or "Download & Convert";
    }

    private DownloadProfileOption ResolveDownloadProfile(string executionMode, ConversionPreset? selectedPreset)
    {
        var configuredProfile = DownloadProfileCatalog.GetProfile(_settings.DownloadProfile);
        return configuredProfile.Id == DownloadProfileCatalog.AutoId
            ? DownloadProfileCatalog.ResolveAuto(executionMode, selectedPreset)
            : configuredProfile;
    }

    private void LogDownloadProfileSelection(DownloadProfileOption resolvedProfile)
    {
        var configuredProfile = DownloadProfileCatalog.GetProfile(_settings.DownloadProfile);
        _log.Info($"Download Profile: {configuredProfile.DisplayName}");
        if (configuredProfile.Id == DownloadProfileCatalog.AutoId)
        {
            _log.Info($"Resolved Download Profile: {resolvedProfile.DisplayName}");
        }

        _log.Info($"yt-dlp format: {resolvedProfile.FormatExpression ?? "auto"}");
    }

    private ConversionPreset GetSelectedConversionPreset()
    {
        var selectedPresetId = OutputPresetComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Tag?.ToString() ?? string.Empty
            : string.Empty;

        return ConversionPresetCatalog
            .GetPresets()
            .FirstOrDefault(preset => preset.Id == selectedPresetId)
            ?? ConversionPresetCatalog.GetDefault();
    }

    private void PopulateOutputPresetComboBox()
    {
        if (OutputPresetComboBox is null)
        {
            return;
        }

        var selectedPresetId = OutputPresetComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem
            ? selectedItem.Tag?.ToString()
            : _settings.OutputPresetId;

        OutputPresetComboBox.SelectionChanged -= OutputPresetComboBox_SelectionChanged;
        PopulatePresetComboBox(OutputPresetComboBox, selectedPresetId);
        OutputPresetComboBox.SelectionChanged += OutputPresetComboBox_SelectionChanged;

        if (SimpleOutputPresetComboBox is not null)
        {
            SimpleOutputPresetComboBox.SelectionChanged -= SimpleOutputPresetComboBox_SelectionChanged;
            PopulatePresetComboBox(SimpleOutputPresetComboBox, GetSelectedComboBoxTag(OutputPresetComboBox));
            SimpleOutputPresetComboBox.SelectionChanged += SimpleOutputPresetComboBox_SelectionChanged;
        }

        UpdateAspectModeSelector();
    }

    private void PopulatePresetComboBox(System.Windows.Controls.ComboBox comboBox, string? selectedPresetId)
    {
        comboBox.Items.Clear();
        foreach (var preset in GetVisibleOutputPresets())
        {
            comboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = preset.DisplayName,
                Tag = preset.Id,
            });
        }

        comboBox.SelectedItem = comboBox.Items
            .OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), selectedPresetId, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault();
    }

    private void ApplyPersistedUiOptions()
    {
        _isApplyingPersistedUiOptions = true;
        try
        {
            SelectComboBoxItemByTag(QueueExecutionModeComboBox, _settings.RunMode);
            SelectComboBoxItemByTag(OutputPresetComboBox, _settings.OutputPresetId);
            SelectComboBoxItemByTag(AspectModeComboBox, _settings.AspectMode);
            KeepOriginalDownloadedFilesCheckBox.IsChecked = _settings.KeepOriginalDownloadedFiles;
            PeakBoostCheckBox.IsChecked = _settings.PeakBoost;
            SelectComboBoxItemByTag(NormalizationModeComboBox, _settings.AudioNormalizationMode.ToString());
            SimpleModeCheckBox.IsChecked = _settings.SimpleModeEnabled;
            SelectComboBoxItemByContent(
                TargetPeakComboBox,
                $"{_settings.TargetPeakDb.ToString("0.0", CultureInfo.InvariantCulture)} dBFS",
                "-1.0 dBFS");
            ApplyReplayGainNormalizationOptionsToControls(new ReplayGainNormalizationOptions(
                _settings.TargetReplayGainVolumeDb,
                _settings.PeakLimitDb,
                _settings.NormalizationToleranceDb,
                _settings.MaximumNormalizationGainDb).Normalize());
            NumberPrefixTextBox.Text = string.Empty;
        }
        finally
        {
            _isApplyingPersistedUiOptions = false;
        }

        UpdateAspectModeSelector();
        UpdateAudioAdjustmentControls();
    }

    private void SavePersistedUiOptions()
    {
        if (_isInitializingUi || _isApplyingPersistedUiOptions || _settings is null)
        {
            return;
        }

        _settings.RunMode = GetQueueExecutionMode();
        if (_hasUserChangedOutputPreset)
        {
            _settings.OutputPresetId = OutputPresetComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem presetItem
                ? presetItem.Tag?.ToString() ?? _settings.OutputPresetId
                : _settings.OutputPresetId;
        }

        _settings.AspectMode = GetSelectedAspectMode();
        _settings.KeepOriginalDownloadedFiles = KeepOriginalDownloadedFilesCheckBox.IsChecked == true;
        _settings.PeakBoost = PeakBoostCheckBox.IsChecked == true;
        _settings.AudioNormalizationMode = GetSelectedAudioNormalizationMode();
        _settings.SimpleModeEnabled = SimpleModeCheckBox.IsChecked == true;
        _settings.TargetPeakDb = GetSelectedTargetPeakDb();
        var replayGainOptions = GetReplayGainNormalizationOptions();
        _settings.TargetReplayGainVolumeDb = replayGainOptions.TargetReplayGainVolumeDb;
        _settings.PeakLimitDb = replayGainOptions.PeakLimitDb;
        _settings.NormalizationToleranceDb = replayGainOptions.ToleranceDb;
        _settings.MaximumNormalizationGainDb = replayGainOptions.MaximumGainDb;
        _settings.KnownOutputPresetIds = ConversionPresetCatalog.GetPresets().Select(static preset => preset.Id).ToList();
        SaveLastUsedLayoutState();

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            _log.Error($"UI options could not be saved. {ex.Message}");
        }
    }

    private void ApplyStartupLayout()
    {
        switch (_settings.StartupLayout)
        {
            case "Standard":
                CandidatesExpander.IsExpanded = true;
                ConversionQueueExpander.IsExpanded = true;
                LogExpander.IsExpanded = false;
                break;
            case "BrowserFocus":
                CandidatesExpander.IsExpanded = true;
                ConversionQueueExpander.IsExpanded = true;
                LogExpander.IsExpanded = false;
                break;
            case "LastUsed":
                ApplyLastUsedWindowSize();
                CandidatesExpander.IsExpanded = _settings.LastCandidatesExpanded;
                ConversionQueueExpander.IsExpanded = true;
                LogExpander.IsExpanded = _settings.LastLogExpanded;
                break;
            case "QueueFocus":
            default:
                CandidatesExpander.IsExpanded = false;
                ConversionQueueExpander.IsExpanded = true;
                LogExpander.IsExpanded = false;
                break;
        }
    }

    private void ApplyStartupRowLayout()
    {
        switch (_settings.StartupLayout)
        {
            case "BrowserFocus":
                if (CandidatesExpander.IsExpanded)
                {
                    VideoListWorkRow.Height = new GridLength(2, GridUnitType.Star);
                }

                if (ConversionQueueExpander.IsExpanded)
                {
                    QueueWorkRow.Height = new GridLength(1, GridUnitType.Star);
                }

                break;
            case "LastUsed":
                ApplyLastUsedRowHeights();
                break;
            case "QueueFocus":
            default:
                if (ConversionQueueExpander.IsExpanded)
                {
                    QueueWorkRow.Height = new GridLength(3, GridUnitType.Star);
                }

                if (LogExpander.IsExpanded)
                {
                    LogWorkRow.Height = new GridLength(1, GridUnitType.Star);
                }

                break;
        }
    }

    private void ApplyLastUsedWindowSize()
    {
        if (_settings.LastWindowWidth >= MinWidth)
        {
            Width = _settings.LastWindowWidth;
        }

        if (_settings.LastWindowHeight >= MinHeight)
        {
            Height = _settings.LastWindowHeight;
        }
    }

    private void ApplyLastUsedRowHeights()
    {
        if (CandidatesExpander.IsExpanded && _settings.LastVideoListRowHeight > 0)
        {
            VideoListWorkRow.Height = new GridLength(_settings.LastVideoListRowHeight);
        }

        if (ConversionQueueExpander.IsExpanded && _settings.LastQueueRowHeight > 0)
        {
            QueueWorkRow.Height = new GridLength(_settings.LastQueueRowHeight);
        }

        if (LogExpander.IsExpanded && _settings.LastLogRowHeight > 0)
        {
            LogWorkRow.Height = new GridLength(_settings.LastLogRowHeight);
        }
    }

    private void SaveLastUsedLayoutState()
    {
        if (_settings is null
            || CandidatesExpander is null
            || LogExpander is null
            || VideoListWorkRow is null
            || QueueWorkRow is null
            || LogWorkRow is null)
        {
            return;
        }

        _settings.LastCandidatesExpanded = CandidatesExpander.IsExpanded;
        _settings.LastLogExpanded = LogExpander.IsExpanded;
        if (WindowState == WindowState.Normal)
        {
            _settings.LastWindowWidth = Width;
            _settings.LastWindowHeight = Height;
        }

        _settings.LastVideoListRowHeight = Math.Max(0, VideoListWorkRow.ActualHeight);
        _settings.LastQueueRowHeight = Math.Max(0, QueueWorkRow.ActualHeight);
        _settings.LastLogRowHeight = Math.Max(0, LogWorkRow.ActualHeight);
    }

    private static void SelectComboBoxItemByContent(
        System.Windows.Controls.ComboBox comboBox,
        string? value,
        string fallbackValue)
    {
        var itemToSelect = comboBox.Items
            .OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items
                .OfType<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Content?.ToString(), fallbackValue, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault();

        comboBox.SelectedItem = itemToSelect;
    }

    private static void SelectComboBoxItemByTag(System.Windows.Controls.ComboBox comboBox, string? value)
    {
        var itemToSelect = comboBox.Items
            .OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault();

        comboBox.SelectedItem = itemToSelect;
    }

    private static string? GetSelectedComboBoxTag(System.Windows.Controls.ComboBox comboBox)
    {
        return comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Tag?.ToString()
            : null;
    }

    private List<ConversionPreset> GetVisibleOutputPresets()
    {
        var presetsById = ConversionPresetCatalog.GetPresets()
            .ToDictionary(static preset => preset.Id, StringComparer.OrdinalIgnoreCase);
        var visiblePresets = _settings.VisibleOutputPresetIds
            .Where(presetsById.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(presetId => presetsById[presetId])
            .ToList();

        if (visiblePresets.Count == 0)
        {
            visiblePresets = ConversionPresetCatalog.GetDefaultVisiblePresetIds()
                .Where(presetsById.ContainsKey)
                .Select(presetId => presetsById[presetId])
                .ToList();
        }

        return visiblePresets;
    }

    private string GetSelectedAspectMode()
    {
        return AspectModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Tag?.ToString() ?? "Keep aspect ratio + padding"
            : "Keep aspect ratio + padding";
    }

    private AudioAdjustmentMode GetSelectedAudioAdjustmentMode()
    {
        var adjustmentText = AudioAdjustmentComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Tag?.ToString() ?? "Off"
            : "Off";

        return adjustmentText switch
        {
            "Loudness normalize" => AudioAdjustmentMode.LoudnessNormalize,
            _ => AudioAdjustmentMode.Off,
        };
    }

    private double GetSelectedTargetPeakDb()
    {
        var targetPeakText = TargetPeakComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Content?.ToString() ?? "-1.0 dBFS"
            : "-1.0 dBFS";
        var numericText = targetPeakText.Replace("dBFS", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetPeakDb)
            ? targetPeakDb
            : -1.0;
    }

    private AudioNormalizationMode GetSelectedAudioNormalizationMode()
    {
        var modeText = GetSelectedComboBoxTag(NormalizationModeComboBox);
        return Enum.TryParse<AudioNormalizationMode>(modeText, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode)
            ? mode
            : AudioNormalizationMode.Peak;
    }

    private ReplayGainNormalizationOptions GetReplayGainNormalizationOptions()
    {
        return new ReplayGainNormalizationOptions(
            ParseInvariantTextBoxValue(TargetReplayGainVolumeTextBox, _settings.TargetReplayGainVolumeDb),
            ParseInvariantTextBoxValue(PeakLimitTextBox, _settings.PeakLimitDb),
            ParseInvariantTextBoxValue(NormalizationToleranceTextBox, _settings.NormalizationToleranceDb),
            ParseInvariantTextBoxValue(MaximumNormalizationGainTextBox, _settings.MaximumNormalizationGainDb))
            .Normalize();
    }

    private static double ParseInvariantTextBoxValue(System.Windows.Controls.TextBox textBox, double fallback)
    {
        return double.TryParse(
            textBox.Text.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private void ApplyReplayGainNormalizationOptionsToControls(ReplayGainNormalizationOptions options)
    {
        TargetReplayGainVolumeTextBox.Text = options.TargetReplayGainVolumeDb.ToString("0.0", CultureInfo.InvariantCulture);
        PeakLimitTextBox.Text = options.PeakLimitDb.ToString("0.0", CultureInfo.InvariantCulture);
        NormalizationToleranceTextBox.Text = options.ToleranceDb.ToString("0.0", CultureInfo.InvariantCulture);
        MaximumNormalizationGainTextBox.Text = options.MaximumGainDb.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private async Task<string?> BuildAudioFilterAsync(
        ConversionQueueItem item,
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        _log.Info($"Audio adjustment for {item.Title}: {GetAudioAdjustmentDisplay(item.AudioAdjustmentMode)}");
        switch (item.AudioAdjustmentMode)
        {
            case AudioAdjustmentMode.LoudnessNormalize when AudioNormalizationPolicy.PerItemOverridesGlobal(item.AudioAdjustmentMode):
                _log.Info("項目別の音量ノーマライズが有効なため、グローバルの音量正規化は適用しません。");
                _log.Info("Audio adjustment filter: loudnorm=I=-16:LRA=11:TP=-1.5");
                return "loudnorm=I=-16:LRA=11:TP=-1.5";
            case AudioAdjustmentMode.Off when PeakBoostCheckBox.IsChecked == true:
                if (GetSelectedAudioNormalizationMode() == AudioNormalizationMode.ReplayGain)
                {
                    _log.Info($"Global ReplayGain normalization applies to {item.Title}.");
                    return await BuildReplayGainNormalizationFilterAsync(item, inputFilePath, cancellationToken);
                }

                _log.Info($"Global Peak Boost applies to {item.Title}.");
                return await BuildPeakNormalizeBoostOnlyFilterAsync(inputFilePath, cancellationToken);
            default:
                _log.Info($"Audio adjustment: Off for {item.Title}. No audio filter will be applied.");
                return string.Empty;
        }
    }

    private async Task<string?> BuildPeakNormalizeBoostOnlyFilterAsync(
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        var targetPeakDb = GetSelectedTargetPeakDb();
        _log.Info($"Analyzing source audio peak for boost-only normalization. Target peak: {targetPeakDb:0.0} dBFS");

        var analysisResult = await _videoConversionService.AnalyzeMaxVolumeAsync(
            inputFilePath,
            message => _log.Info(message),
            cancellationToken);

        if (cancellationToken.IsCancellationRequested || analysisResult.StandardError == "Audio peak analysis was canceled.")
        {
            _log.Info("Audio peak analysis canceled.");
            return null;
        }

        if (!analysisResult.IsSuccess || analysisResult.MaxVolumeDb is null)
        {
            _log.Error($"Audio peak analysis failed. Exit code: {analysisResult.ExitCode?.ToString() ?? "unknown"}.");
            LogProcessOutput(analysisResult.StandardError, "ffmpeg volumedetect stderr");
            LogProcessOutput(analysisResult.StandardOutput, "ffmpeg volumedetect stdout");
            return null;
        }

        var maxVolumeDb = analysisResult.MaxVolumeDb.Value;
        var gainDb = targetPeakDb - maxVolumeDb;
        _log.Info($"Audio max_volume: {maxVolumeDb:0.0} dB");
        _log.Info($"Audio target peak: {targetPeakDb:0.0} dBFS");
        _log.Info($"Computed audio gain: {gainDb:0.0} dB");

        if (gainDb <= 0)
        {
            _log.Info("Source is already loud enough. No boost filter will be applied.");
            return string.Empty;
        }

        var filter = PeakNormalizationFilterBuilder.BuildBoostOnly(maxVolumeDb, targetPeakDb);
        _log.Info($"Audio boost applied: {filter}");
        return filter;
    }

    private async Task<string?> BuildReplayGainNormalizationFilterAsync(
        ConversionQueueItem item,
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        var options = GetReplayGainNormalizationOptions();
        SetQueueProgress(item, "音量を解析中...", null, string.Empty, string.Empty, string.Empty, isIndeterminate: true);
        _log.Info("音量を解析中...");

        var preparation = await _replayGainNormalizationService.PrepareAsync(
            token => _videoConversionService.AnalyzeReplayGainAsync(
                inputFilePath,
                message => _log.Info(message),
                token),
            options,
            cancellationToken);

        if (preparation.Status == ReplayGainPreparationStatus.Canceled)
        {
            _log.Info("音量解析をキャンセルしました。");
            return null;
        }

        if (preparation.Status == ReplayGainPreparationStatus.AnalysisFailed)
        {
            var analysisResult = preparation.AnalysisResult;
            _log.Warn($"ReplayGainを解析できなかったため、正規化なしで変換を続行します。終了コード: {analysisResult?.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "不明"}");
            if (analysisResult is not null)
            {
                LogProcessOutput(analysisResult.StandardError, "ffmpeg replaygain stderr");
                LogProcessOutput(analysisResult.StandardOutput, "ffmpeg replaygain stdout");
            }

            return string.Empty;
        }

        var analysis = preparation.AnalysisResult!.Analysis!;
        var decision = preparation.Decision!;
        _log.Info("音量を解析しました。");
        _log.Info($"トラック音量: {decision.DetectedTrackVolumeDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
        _log.Info($"ReplayGain: {FormatSignedDb(analysis.TrackGainDb)} dB");
        _log.Info($"正規化設定: {options.TargetReplayGainVolumeDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
        _log.Info($"要求ゲイン: {FormatSignedDb(decision.RequestedGainDb)} dB");
        _log.Info($"トラックピーク: {analysis.TrackPeak.ToString("0.000000", CultureInfo.InvariantCulture)}");

        if (decision.Action == ReplayGainNormalizationAction.Skip)
        {
            _log.Info($"判定: {decision.Reason}");
            return string.Empty;
        }

        _log.Info($"適用ゲイン: {FormatSignedDb(decision.AppliedGainDb)} dB");
        if (decision.GainWasLimited)
        {
            _log.Info($"判定: 最大増幅量または安全な最大減衰量によってゲインを制限しました。上限: +{options.MaximumGainDb.ToString("0.0", CultureInfo.InvariantCulture)} dB / 下限: -{ReplayGainNormalizationOptions.MaximumAttenuationDb.ToString("0.0", CultureInfo.InvariantCulture)} dB");
        }

        _log.Info($"予測ピーク: {FormatSignedDb(decision.PredictedPeakDb)} dBFS");
        _log.Info($"ピーク上限: {options.PeakLimitDb.ToString("0.0", CultureInfo.InvariantCulture)} dBFS");
        _log.Info($"判定: {decision.Reason}");
        _log.Info($"Audio adjustment filter: {decision.AudioFilter}");
        return decision.AudioFilter;
    }

    private static string FormatSignedDb(double value)
    {
        return value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }

    private void UpdateAspectModeSelector()
    {
        if (OutputPresetComboBox is null || AspectModeComboBox is null || AspectModeLabel is null)
        {
            return;
        }

        var preset = GetSelectedConversionPreset();
        AspectModeLabel.IsEnabled = preset.SupportsAspectMode;
        AspectModeComboBox.IsEnabled = preset.SupportsAspectMode;
    }

    private void UpdateAudioAdjustmentControls()
    {
        if (AudioAdjustmentComboBox is null
            || TargetPeakComboBox is null
            || TargetPeakLabel is null
            || NormalizationModeComboBox is null
            || ReplayGainOptionsPanel is null)
        {
            return;
        }

        var controlsAvailable = PeakBoostCheckBox.IsEnabled;
        var normalizationEnabled = controlsAvailable && PeakBoostCheckBox.IsChecked == true;
        var isPeakMode = GetSelectedAudioNormalizationMode() == AudioNormalizationMode.Peak;
        NormalizationModeComboBox.IsEnabled = controlsAvailable;
        TargetPeakLabel.Visibility = isPeakMode ? Visibility.Visible : Visibility.Collapsed;
        TargetPeakComboBox.Visibility = isPeakMode ? Visibility.Visible : Visibility.Collapsed;
        TargetPeakLabel.IsEnabled = normalizationEnabled;
        TargetPeakComboBox.IsEnabled = normalizationEnabled;
        ReplayGainOptionsPanel.Visibility = isPeakMode ? Visibility.Collapsed : Visibility.Visible;
        ReplayGainOptionsPanel.IsEnabled = normalizationEnabled;
    }

    private static string GetAudioAdjustmentDisplay(AudioAdjustmentMode mode)
    {
        return mode switch
        {
            AudioAdjustmentMode.LoudnessNormalize => "音量ノーマライズ",
            _ => "なし",
        };
    }

    private static bool IsPortableDvdPreset(string presetName)
    {
        return presetName.StartsWith("Portable DVD Player MPG", StringComparison.OrdinalIgnoreCase);
    }

    private static VideoListItem CreateDownloadVideoListItem(ConversionQueueItem item)
    {
        var videoId = Uri.TryCreate(item.SourcePathOrUrl, UriKind.Absolute, out _)
            ? string.Empty
            : item.SourcePathOrUrl;

        return new VideoListItem
        {
            IsSelected = true,
            Order = item.Order,
            Title = item.Title,
            VideoId = videoId,
            Url = item.SourcePathOrUrl,
            SourceType = "YouTube",
            SourcePath = item.SourcePathOrUrl,
            Status = item.Status,
        };
    }

    private string GetConvertedOutputFolder(ConversionPreset preset)
    {
        return GetConvertedOutputFolder(GetPresetFolderName(preset));
    }

    private string GetConvertedOutputFolder(string presetFolderName)
    {
        var outputFolder = _settings.CreateSubfolderPerOutputPreset
            ? Path.Combine(GetBaseOutputFolder(), presetFolderName)
            : GetBaseOutputFolder();
        Directory.CreateDirectory(outputFolder);
        return outputFolder;
    }

    private string GetBaseOutputFolder()
    {
        return string.IsNullOrWhiteSpace(_sessionOutputFolder)
            ? _settings.ConvertedFolder
            : _sessionOutputFolder;
    }

    private static string GetPresetFolderName(ConversionPreset preset)
    {
        var folderName = preset.Id switch
        {
            ConversionPresetCatalog.CurrentCompatibilityId => "CarNavi_MP4_Current",
            "car-navi-standard" => "CarNavi_MP4_Standard",
            "car-navi-small" => "CarNavi_MP4_SmallSize",
            "car-navi-high" => "CarNavi_MP4_HighQuality",
            ConversionPresetCatalog.IpadTabletMp41080pStandardId => "iPad_Tablet_MP4_1080p_Standard",
            ConversionPresetCatalog.IpadTabletMp4720pCompatibleId => "iPad_Tablet_MP4_720p_Compatible",
            ConversionPresetCatalog.IpadTabletHevc1080pHighCompressionId => "iPad_Tablet_HEVC_1080p_HighCompression",
            ConversionPresetCatalog.AndroidTabletMp41080pStandardId => "AndroidTablet_MP4_1080p_Standard",
            ConversionPresetCatalog.AndroidTabletMp4720pCompatibleId => "AndroidTablet_MP4_720p_Compatible",
            ConversionPresetCatalog.AndroidTabletHevc1080pHighCompressionId => "AndroidTablet_HEVC_1080p_HighCompression",
            "dvd-standard" => "PortableDVD_MPG_Standard",
            "dvd-small" => "PortableDVD_MPG_SmallSize",
            "dvd-high" => "PortableDVD_MPG_HighQuality",
            "audio-mp4-aac-only-high" or "audio-mp4-aac-only-medium" or "audio-mp4-aac-only-low" => "Audio_MP4_AAC_Only",
            "audio-mp3-high" or "audio-mp3-medium" or "audio-mp3-low" => "Audio_MP3",
            "audio-m4a-aac-high" or "audio-m4a-aac-medium" or "audio-m4a-aac-low" => "Audio_M4A_AAC",
            "audio-wav-pcm-16bit" => "Audio_WAV",
            "audio-flac-lossless" => "Audio_FLAC",
            "audio-ogg-high" or "audio-ogg-medium" or "audio-ogg-low" => "Audio_OGG",
            "audio-wma-high" or "audio-wma-medium" or "audio-wma-low" => "Audio_WMA",
            _ => SafeFileName.Create(preset.DisplayName, preset.Id).Replace(' ', '_'),
        };

        return SafeFileName.Create(folderName, preset.Id).Replace(' ', '_');
    }

    private void LogOutputConflictIfNeeded(string folder, string desiredStem, string extension, string selectedPath)
    {
        var desiredPath = PathHelper.BuildFilePath(folder, desiredStem, extension);
        if (!string.Equals(desiredPath, selectedPath, StringComparison.OrdinalIgnoreCase))
        {
            _log.Info("出力先に同名の管理外ファイルまたは別項目の成果物が存在するため、衝突回避名を使用します。");
            _log.Info($"元の候補: {Path.GetFileName(desiredPath)}");
            _log.Info($"出力名: {Path.GetFileName(selectedPath)}");
        }
    }

    private void DeleteTemporaryDownload(string downloadedFilePath)
    {
        if (string.IsNullOrWhiteSpace(downloadedFilePath) || !File.Exists(downloadedFilePath))
        {
            return;
        }

        try
        {
            File.Delete(downloadedFilePath);
            _log.Success($"Deleted temporary file: {downloadedFilePath}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Warning: failed to delete temporary file: {downloadedFilePath}. {ex.Message}");
        }
    }

    private void LogExpander_Expanded(object sender, RoutedEventArgs e)
    {
        LogGridSplitter.Visibility = Visibility.Visible;
        LogWorkRow.MinHeight = 140;
        LogWorkRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void LogExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        LogGridSplitter.Visibility = Visibility.Collapsed;
        LogWorkRow.MinHeight = 0;
        LogWorkRow.Height = GridLength.Auto;
    }

    private void CandidatesExpander_Expanded(object sender, RoutedEventArgs e)
    {
        UpdateMainWorkspaceLayout();
    }

    private void CandidatesExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        UpdateMainWorkspaceLayout();
    }

    private void ConversionQueueExpander_Expanded(object sender, RoutedEventArgs e)
    {
        UpdateMainWorkspaceLayout();
    }

    private void ConversionQueueExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        UpdateMainWorkspaceLayout();
    }

    private async Task CheckExternalToolsOnStartupAsync()
    {
        var result = await CheckExternalToolsAsync();
        foreach (var tool in result.Results)
        {
            LogToolResult(tool);
        }

        UpdateExternalToolsStatus();
        if (result.IsReady)
        {
            return;
        }

        var dialogResult = MessageBox.Show(
            this,
            "yt-dlp または FFmpeg が見つかりません。\nダウンロードや変換を行うには外部ツールが必要です。\n\nはい: 自動取得する\nいいえ: 手動で指定する\nキャンセル: 後で行う",
            "外部ツールが必要です",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (dialogResult == MessageBoxResult.Yes)
        {
            await InstallExternalToolsAsync();
        }
        else if (dialogResult == MessageBoxResult.No)
        {
            SettingsMenuItem_Click(SettingsMenuItem, new RoutedEventArgs());
        }
    }

    private async Task<ExternalToolCheckResult> CheckExternalToolsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _externalToolService.CheckAllAsync(_settings, cancellationToken);
        _lastYtDlpResult = result.YtDlp;
        _lastFfmpegResult = result.Ffmpeg;
        _lastFfprobeResult = result.Ffprobe;
        ApplyResolvedToolPaths(result);
        return result;
    }

    private async Task<bool> EnsureRequiredToolsAsync(bool requireYtDlp, bool requireFfmpeg, string title)
    {
        var result = await CheckExternalToolsAsync();
        UpdateExternalToolsStatus();

        var missingTools = new List<string>();
        if (requireYtDlp && !result.YtDlp.IsAvailable)
        {
            missingTools.Add("yt-dlp");
        }

        if (requireFfmpeg && !result.Ffmpeg.IsAvailable)
        {
            missingTools.Add("ffmpeg");
        }

        if (requireFfmpeg && !result.Ffprobe.IsAvailable)
        {
            missingTools.Add("ffprobe");
        }

        if (missingTools.Count == 0)
        {
            return true;
        }

        var dialogResult = MessageBox.Show(
            this,
            $"必要な外部ツールが見つかりません: {string.Join(", ", missingTools)}\n\nはい: 自動取得する\nいいえ: 手動で指定する\nキャンセル: 中止",
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (dialogResult == MessageBoxResult.Yes)
        {
            return await InstallExternalToolsAsync() && await EnsureRequiredToolsAsync(requireYtDlp, requireFfmpeg, title);
        }

        if (dialogResult == MessageBoxResult.No)
        {
            SettingsMenuItem_Click(SettingsMenuItem, new RoutedEventArgs());
        }

        return false;
    }

    private async Task<bool> InstallExternalToolsAsync()
    {
        InstallExternalToolsMenuItem.IsEnabled = false;
        CheckExternalToolsMenuItem.IsEnabled = false;
        try
        {
            _log.Info("外部ツールの自動取得を開始します。");
            var progress = new Progress<string>(message =>
            {
                ExternalToolsStatusTextBlock.Text = message;
                _log.Info(message);
            });

            await _externalToolService.InstallToolsAsync(_settings, message => _log.Info(message), progress);
            var result = await CheckExternalToolsAsync();
            foreach (var tool in result.Results)
            {
                LogToolResult(tool);
            }

            UpdateExternalToolsStatus();
            if (result.IsReady)
            {
                _log.Success("外部ツールの自動取得が完了しました。");
                MessageBox.Show(this, "外部ツールの準備が完了しました。", "外部ツール", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }

            MessageBox.Show(this, "取得後の確認で不足しているツールがあります。ログを確認してください。", "外部ツール", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex)
        {
            _log.Error($"外部ツールの自動取得に失敗しました。{ex.Message}");
            MessageBox.Show(this, $"外部ツールの自動取得に失敗しました。\n{ex.Message}", "外部ツール取得エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            InstallExternalToolsMenuItem.IsEnabled = true;
            CheckExternalToolsMenuItem.IsEnabled = !_isDownloading && !_isConverting && !_isQueueConverting;
        }
    }

    private void ApplyResolvedToolPathsFromSettings()
    {
        if (File.Exists(_settings.YtDlpPath))
        {
            _videoDownloadService.YtDlpPath = _settings.YtDlpPath;
            _videoMetadataService.YtDlpPath = _settings.YtDlpPath;
        }

        if (File.Exists(_settings.FfmpegPath))
        {
            _videoConversionService.FfmpegPath = _settings.FfmpegPath;
        }
    }

    private void ApplyResolvedToolPaths(ExternalToolCheckResult result)
    {
        if (result.YtDlp.IsAvailable && !string.IsNullOrWhiteSpace(result.YtDlp.ExecutablePath))
        {
            _videoDownloadService.YtDlpPath = result.YtDlp.ExecutablePath;
            _videoMetadataService.YtDlpPath = result.YtDlp.ExecutablePath;
            _log.Info($"Using yt-dlp executable: {result.YtDlp.ExecutablePath}");
        }

        if (result.Ffmpeg.IsAvailable && !string.IsNullOrWhiteSpace(result.Ffmpeg.ExecutablePath))
        {
            _videoConversionService.FfmpegPath = result.Ffmpeg.ExecutablePath;
            _log.Info($"Using ffmpeg executable: {result.Ffmpeg.ExecutablePath}");
        }

        if (result.Ffprobe.IsAvailable && !string.IsNullOrWhiteSpace(result.Ffprobe.ExecutablePath))
        {
            _log.Info($"Using ffprobe executable: {result.Ffprobe.ExecutablePath}");
        }
    }

    private void LogToolResult(ExternalToolResult result)
    {
        if (result.IsAvailable)
        {
            _log.Info(BuildToolStatusMessage(result));
        }
        else
        {
            _log.Error(BuildToolStatusMessage(result));
        }
    }

    private void UpdateExternalToolsStatus()
    {
        if (_lastYtDlpResult is null || _lastFfmpegResult is null || _lastFfprobeResult is null)
        {
            ExternalToolsStatusTextBlock.Text = "External tools: Check required";
            return;
        }

        var missingTools = new List<string>();
        if (!_lastYtDlpResult.IsAvailable)
        {
            missingTools.Add("yt-dlp");
        }

        if (!_lastFfmpegResult.IsAvailable)
        {
            missingTools.Add("ffmpeg");
        }

        if (!_lastFfprobeResult.IsAvailable)
        {
            missingTools.Add("ffprobe");
        }

        ExternalToolsStatusTextBlock.Text = missingTools.Count == 0
            ? "External tools: OK"
            : $"External tools: Missing {string.Join(", ", missingTools)}";
    }

    private void CopyYtDlpInstallCommandButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCommandToClipboard(YtDlpInstallCommand);
    }

    private void CopyFfmpegSearchCommandButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCommandToClipboard(FfmpegSearchCommand);
    }

    private void CopyFfmpegInstallCommandButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCommandToClipboard(FfmpegInstallCommand);
    }

    private void CopyUpdateCommandsButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCommandToClipboard($"{YtDlpUpdateCommand}{Environment.NewLine}{FfmpegUpdateCommand}");
    }

    private void CopyAllLogsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_logEntries.Count == 0)
        {
            return;
        }

        var lines = _logEntries.Select(static entry =>
            $"[{entry.Timestamp:HH:mm:ss}] {entry.Level.ToString().ToUpperInvariant()}: {entry.Message}");
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnLogEntryAdded(sender, entry));
            return;
        }

        _logEntries.Add(entry);
        while (_logEntries.Count > MaxLogEntryCount)
        {
            _logEntries.RemoveAt(0);
        }

        LogListBox.ScrollIntoView(entry);
    }

    private void SetFetchControlsEnabled(bool isEnabled)
    {
        FetchVideoListButton.IsEnabled = isEnabled;
        DirectUrlModeRadioButton.IsEnabled = isEnabled;
        YouTubeSearchModeRadioButton.IsEnabled = isEnabled;
        UrlTextBox.IsEnabled = isEnabled;
        SearchQueryTextBox.IsEnabled = isEnabled;
        SearchResultCountComboBox.IsEnabled = isEnabled;
        SelectAllVideosButton.IsEnabled = isEnabled;
        DeselectAllVideosButton.IsEnabled = isEnabled;
        InvertVideoSelectionButton.IsEnabled = isEnabled;
        MoveUpButton.IsEnabled = isEnabled;
        MoveDownButton.IsEnabled = isEnabled;
        RemoveButton.IsEnabled = isEnabled;
        AddSelectedToQueueButton.IsEnabled = isEnabled;
    }

    private void SetDownloadState(bool isDownloading)
    {
        CheckExternalToolsMenuItem.IsEnabled = !isDownloading;
        InstallExternalToolsMenuItem.IsEnabled = !isDownloading;
        SettingsMenuItem.IsEnabled = !isDownloading;
        FetchVideoListButton.IsEnabled = !isDownloading;
        DirectUrlModeRadioButton.IsEnabled = !isDownloading;
        YouTubeSearchModeRadioButton.IsEnabled = !isDownloading;
        UrlTextBox.IsEnabled = !isDownloading;
        SearchQueryTextBox.IsEnabled = !isDownloading;
        SearchResultCountComboBox.IsEnabled = !isDownloading;
        AddSelectedToQueueButton.IsEnabled = !isDownloading;
        ConvertDownloadedButton.IsEnabled = !isDownloading;
        ConvertQueueButton.IsEnabled = !isDownloading;
        RetryFailedQueueButton.IsEnabled = !isDownloading;
        CancelQueueButton.IsEnabled = false;
        SimpleCancelQueueButton.IsEnabled = false;
        QueueExecutionModeComboBox.IsEnabled = !isDownloading;
        OutputPresetComboBox.IsEnabled = !isDownloading;
        AspectModeComboBox.IsEnabled = !isDownloading && GetSelectedConversionPreset().SupportsAspectMode;
        KeepOriginalDownloadedFilesCheckBox.IsEnabled = !isDownloading;
        NumberPrefixTextBox.IsEnabled = !isDownloading;
        PeakBoostCheckBox.IsEnabled = !isDownloading;
        AudioAdjustmentComboBox.IsEnabled = !isDownloading;
        ApplyAudioAdjustmentButton.IsEnabled = !isDownloading;
        UpdateAudioAdjustmentControls();
        QueueMoveUpButton.IsEnabled = !isDownloading;
        QueueMoveDownButton.IsEnabled = !isDownloading;
        QueueRemoveButton.IsEnabled = !isDownloading;
        QueueClearButton.IsEnabled = !isDownloading;
        SelectAllVideosButton.IsEnabled = !isDownloading;
        DeselectAllVideosButton.IsEnabled = !isDownloading;
        InvertVideoSelectionButton.IsEnabled = !isDownloading;
        MoveUpButton.IsEnabled = !isDownloading;
        MoveDownButton.IsEnabled = !isDownloading;
        RemoveButton.IsEnabled = !isDownloading;
        CancelDownloadButton.IsEnabled = isDownloading;
        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        UpdateNumberPrefixControls();
    }

    private void SetConversionState(bool isConverting)
    {
        CheckExternalToolsMenuItem.IsEnabled = !isConverting;
        InstallExternalToolsMenuItem.IsEnabled = !isConverting;
        SettingsMenuItem.IsEnabled = !isConverting;
        FetchVideoListButton.IsEnabled = !isConverting;
        DirectUrlModeRadioButton.IsEnabled = !isConverting;
        YouTubeSearchModeRadioButton.IsEnabled = !isConverting;
        UrlTextBox.IsEnabled = !isConverting;
        SearchQueryTextBox.IsEnabled = !isConverting;
        SearchResultCountComboBox.IsEnabled = !isConverting;
        AddSelectedToQueueButton.IsEnabled = !isConverting;
        MoveUpButton.IsEnabled = !isConverting;
        MoveDownButton.IsEnabled = !isConverting;
        RemoveButton.IsEnabled = !isConverting;
        DownloadSelectedButton.IsEnabled = !isConverting;
        SelectAllVideosButton.IsEnabled = !isConverting;
        DeselectAllVideosButton.IsEnabled = !isConverting;
        InvertVideoSelectionButton.IsEnabled = !isConverting;
        ConvertQueueButton.IsEnabled = !isConverting;
        RetryFailedQueueButton.IsEnabled = !isConverting;
        CancelQueueButton.IsEnabled = false;
        SimpleCancelQueueButton.IsEnabled = false;
        QueueExecutionModeComboBox.IsEnabled = !isConverting;
        OutputPresetComboBox.IsEnabled = !isConverting;
        AspectModeComboBox.IsEnabled = !isConverting && GetSelectedConversionPreset().SupportsAspectMode;
        KeepOriginalDownloadedFilesCheckBox.IsEnabled = !isConverting;
        NumberPrefixTextBox.IsEnabled = !isConverting;
        PeakBoostCheckBox.IsEnabled = !isConverting;
        AudioAdjustmentComboBox.IsEnabled = !isConverting;
        ApplyAudioAdjustmentButton.IsEnabled = !isConverting;
        UpdateAudioAdjustmentControls();
        QueueMoveUpButton.IsEnabled = !isConverting;
        QueueMoveDownButton.IsEnabled = !isConverting;
        QueueRemoveButton.IsEnabled = !isConverting;
        QueueClearButton.IsEnabled = !isConverting;
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        UpdateNumberPrefixControls();
    }

    private void SetQueueConversionState(bool isConverting)
    {
        CheckExternalToolsMenuItem.IsEnabled = !isConverting;
        InstallExternalToolsMenuItem.IsEnabled = !isConverting;
        SettingsMenuItem.IsEnabled = !isConverting;
        FetchVideoListButton.IsEnabled = !isConverting;
        DirectUrlModeRadioButton.IsEnabled = !isConverting;
        YouTubeSearchModeRadioButton.IsEnabled = !isConverting;
        UrlTextBox.IsEnabled = !isConverting;
        SearchQueryTextBox.IsEnabled = !isConverting;
        SearchResultCountComboBox.IsEnabled = !isConverting;
        AddSelectedToQueueButton.IsEnabled = !isConverting;
        MoveUpButton.IsEnabled = !isConverting;
        MoveDownButton.IsEnabled = !isConverting;
        RemoveButton.IsEnabled = !isConverting;
        DownloadSelectedButton.IsEnabled = !isConverting;
        ConvertDownloadedButton.IsEnabled = !isConverting;
        SelectAllVideosButton.IsEnabled = !isConverting;
        DeselectAllVideosButton.IsEnabled = !isConverting;
        InvertVideoSelectionButton.IsEnabled = !isConverting;
        QueueMoveUpButton.IsEnabled = !isConverting;
        QueueMoveDownButton.IsEnabled = !isConverting;
        QueueRemoveButton.IsEnabled = !isConverting;
        QueueClearButton.IsEnabled = !isConverting;
        QueueExecutionModeComboBox.IsEnabled = !isConverting;
        OutputPresetComboBox.IsEnabled = !isConverting;
        AspectModeComboBox.IsEnabled = !isConverting && GetSelectedConversionPreset().SupportsAspectMode;
        KeepOriginalDownloadedFilesCheckBox.IsEnabled = !isConverting;
        NumberPrefixTextBox.IsEnabled = !isConverting;
        PeakBoostCheckBox.IsEnabled = !isConverting;
        AudioAdjustmentComboBox.IsEnabled = !isConverting;
        ApplyAudioAdjustmentButton.IsEnabled = !isConverting;
        UpdateAudioAdjustmentControls();
        RetryFailedQueueButton.IsEnabled = !isConverting;
        CancelQueueButton.IsEnabled = isConverting;
        SimpleCancelQueueButton.IsEnabled = isConverting;
        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        UpdateNumberPrefixControls();
    }

    private void RefreshOrderNumbers()
    {
        for (var index = 0; index < _videos.Count; index++)
        {
            _videos[index].Order = index + 1;
        }
    }

    private void RefreshQueueOrderNumbers()
    {
        for (var index = 0; index < _conversionQueue.Count; index++)
        {
            _conversionQueue[index].Order = index + 1;
        }
        if (!_isInitializingUi && !_isUpdatingPlaylist)
        {
            ReconcileAllResults();
        }
    }

    private void UpdateSectionHeaders()
    {
        CandidatesExpander.Header = $"動画ソース ({_videos.Count})";
        var queueHeader = $"変換キュー ({_conversionQueue.Count} 件)";
        if (_queueProgressTotal > 0)
        {
            queueHeader += $"  {_queueProgressProcessed}/{_queueProgressTotal}  {GetQueueProgressPercent()}%";
        }

        ConversionQueueExpander.Header = queueHeader;
    }

    private void ResetQueueProgress(int total)
    {
        _queueProgressTotal = Math.Max(0, total);
        _queueProgressProcessed = 0;
        _queueProgressValue = 0;
        UpdateQueueProgressDisplay();
    }

    private void ClearQueueProgress()
    {
        _queueProgressTotal = 0;
        _queueProgressProcessed = 0;
        _queueProgressValue = 0;
        UpdateQueueProgressDisplay();
    }

    private void RefreshQueueProgressFromStatuses(IReadOnlyCollection<ConversionQueueItem> queueItems)
    {
        if (_queueProgressTotal <= 0)
        {
            return;
        }

        var completedCount = Math.Min(queueItems.Count(IsProcessedQueueStatus), _queueProgressTotal);
        var activePartial = queueItems
            .Where(static item => !IsProcessedQueueStatus(item))
            .Select(static item => Math.Clamp((item.ProgressPercent ?? 0) / 100.0, 0, 0.999))
            .DefaultIfEmpty(0)
            .Max();

        _queueProgressProcessed = completedCount;
        _queueProgressValue = Math.Min(completedCount + activePartial, _queueProgressTotal);
        UpdateQueueProgressDisplay();
    }

    private void UpdateQueueProgressDisplay()
    {
        if (QueueProgressBar is null || QueueProgressTextBlock is null)
        {
            return;
        }

        QueueProgressBar.Maximum = _queueProgressTotal > 0 ? _queueProgressTotal : 1;
        QueueProgressBar.Value = Math.Min(_queueProgressValue, QueueProgressBar.Maximum);
        QueueProgressTextBlock.Text = _queueProgressTotal > 0
            ? $"{_queueProgressProcessed}/{_queueProgressTotal}  {GetQueueProgressPercent()}%"
            : "準備完了";
        UpdateSectionHeaders();
    }

    private int GetQueueProgressPercent()
    {
        return _queueProgressTotal > 0
            ? (int)Math.Round(_queueProgressValue * 100.0 / _queueProgressTotal)
            : 0;
    }

    private static bool IsProcessedQueueStatus(ConversionQueueItem item)
    {
        return item.Status is "Converted" or "Downloaded" or "Completed" or "Failed" or "Skipped" or QueueStatusUnsupported;
    }

    private bool IsQueueProcessingActive()
    {
        return _isQueueConverting || _isSimpleModeRunning;
    }

    private static bool IsActiveQueueItem(ConversionQueueItem item)
    {
        if (item.Status == QueueStatusMetadataLoading)
        {
            return true;
        }

        return item.Status.Contains("処理中", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("ダウンロード中", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("変換中", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("キャンセル", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("Downloading", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("Converting", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("Processing", StringComparison.OrdinalIgnoreCase)
            || item.Status.Contains("Cancel", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateMainWorkspaceLayout()
    {
        if (CandidatesExpander is null || ConversionQueueExpander is null)
        {
            return;
        }

        var candidatesVisible = CandidatesExpander.Visibility == Visibility.Visible && CandidatesExpander.IsExpanded;
        var queueVisible = ConversionQueueExpander.Visibility == Visibility.Visible && ConversionQueueExpander.IsExpanded;

        VideoListWorkRow.MinHeight = candidatesVisible ? 120 : 0;
        VideoListWorkRow.Height = candidatesVisible
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        QueueWorkRow.MinHeight = queueVisible ? 120 : 0;
        QueueWorkRow.Height = queueVisible
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        CandidateQueueGridSplitter.Visibility = candidatesVisible && queueVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LogProcessOutput(string output, string label)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        _log.Info($"{label}: {output.Trim()}");
    }

    private string? BuildVideoSourceInput()
    {
        if (YouTubeSearchModeRadioButton.IsChecked == true)
        {
            var query = SearchQueryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                _log.Error("Enter a search query before searching videos.");
                return null;
            }

            var resultCount = GetSearchResultCount();
            var ytDlpInput = $"ytsearch{resultCount}:{query}";
            _log.Info($"Searching YouTube for: {query}");
            _log.Info($"yt-dlp input: {ytDlpInput}");
            return ytDlpInput;
        }

        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            _log.Error("Enter a video, playlist, channel, or other yt-dlp-supported URL before loading.");
            return null;
        }

        var validation = NormalizeSingleVideoUrl(url);
        if (!validation.IsAllowed)
        {
            _log.Error(validation.Reason);
            return null;
        }

        if (!string.Equals(url, validation.Url, StringComparison.Ordinal))
        {
            UrlTextBox.Text = validation.Url;
            UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
            _log.Info($"Normalized URL input: {validation.Url}");
        }

        _log.Info($"Loading video list from URL: {validation.Url}");
        return validation.Url;
    }

    private int GetSearchResultCount()
    {
        if (SearchResultCountComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && int.TryParse(item.Content?.ToString(), out var resultCount))
        {
            return resultCount;
        }

        return 20;
    }

    private int? GetNumberPrefixStartNumber()
    {
        var prefixText = NumberPrefixTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prefixText))
        {
            return null;
        }

        if (int.TryParse(prefixText, out var startNumber) && startNumber >= 1)
        {
            return startNumber;
        }

        _log.Error("Number Prefix must be empty or a positive integer. Numbering is off for this run.");
        return null;
    }

    private void UpdateNumberPrefixControls()
    {
        if (NumberPrefixTextBox is null)
        {
            return;
        }

        NumberPrefixTextBox.IsEnabled = !_isDownloading && !_isConverting && !_isQueueConverting;
    }

    private void UpdateSourceInputMode()
    {
        if (DirectUrlInputGrid is null || YouTubeSearchInputGrid is null || FetchVideoListButton is null)
        {
            return;
        }

        var isSearchMode = YouTubeSearchModeRadioButton.IsChecked == true;
        DirectUrlInputGrid.Visibility = isSearchMode ? Visibility.Collapsed : Visibility.Visible;
        YouTubeSearchInputGrid.Visibility = isSearchMode ? Visibility.Visible : Visibility.Collapsed;
        FetchVideoListButton.Content = isSearchMode ? "検索" : "URL読込";
        FetchVideoListButton.ToolTip = isSearchMode
            ? "入力した検索語で候補を取得します。Enterキーでも実行できます。"
            : "入力したURLから動画情報を取得します。Enterキーでも実行できます。";
    }

    private int SetVideoSelection(bool isSelected)
    {
        var changedCount = 0;
        foreach (var video in _videos)
        {
            if (video.IsSelected == isSelected)
            {
                continue;
            }

            video.IsSelected = isSelected;
            changedCount++;
        }

        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        return changedCount;
    }

    private async Task LoadCandidateThumbnailsAsync(IEnumerable<VideoListItem> videos)
    {
        var thumbnailTasks = videos
            .Where(static video => !string.IsNullOrWhiteSpace(video.ThumbnailUrl))
            .Select(LoadCandidateThumbnailAsync)
            .ToList();
        if (thumbnailTasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(thumbnailTasks);
    }

    private async Task LoadCandidateThumbnailAsync(VideoListItem video)
    {
        try
        {
            video.ThumbnailImage = await GetThumbnailImageAsync(video.ThumbnailUrl);
        }
        catch (Exception ex)
        {
            video.ThumbnailImage = null;
            _log.Info($"Thumbnail could not be loaded for {video.Title}. {ex.Message}");
        }
    }

    private async Task<ImageSource?> GetThumbnailImageAsync(string thumbnailUrl)
    {
        if (_thumbnailCache.TryGetValue(thumbnailUrl, out var cachedThumbnail))
        {
            return cachedThumbnail;
        }

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            _thumbnailCache[thumbnailUrl] = null;
            return null;
        }

        var imageBytes = await ThumbnailHttpClient.GetByteArrayAsync(uri);
        await using var stream = new MemoryStream(imageBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 144;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        _thumbnailCache[thumbnailUrl] = bitmap;
        return bitmap;
    }

    private async Task AddDroppedTextUrlsToQueueAsync(string droppedText, bool isSimpleModeItem = false)
    {
        var urls = GetDroppedUrls(droppedText).ToList();
        if (urls.Count == 0)
        {
            _log.Info($"Dropped text is not recognized as URL: {GetLogPreview(droppedText)}");
            return;
        }

        await AddOnlineUrlsToQueueAsync(urls, "drag and drop", isSimpleModeItem);
    }

    private async Task AddOnlineUrlsToQueueAsync(IEnumerable<string> urls, string sourceLabel, bool isSimpleModeItem = false)
    {
        var addedCount = 0;
        var duplicateCount = 0;
        var fallbackCount = 0;
        var droppedUrls = urls
            .Select(originalUrl => new { OriginalUrl = originalUrl, Validation = NormalizeSingleVideoUrl(originalUrl) })
            .GroupBy(url => url.Validation.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var pendingMetadataItems = new List<(ConversionQueueItem QueueItem, string NormalizedUrl)>();

        foreach (var droppedUrl in droppedUrls)
        {
            if (!droppedUrl.Validation.IsAllowed)
            {
                _log.Error(droppedUrl.Validation.Reason);
                continue;
            }

            var normalizedUrl = droppedUrl.Validation.Url;
            LogDroppedUrlNormalization(droppedUrl.OriginalUrl, normalizedUrl);

            if (QueueContainsSource(normalizedUrl))
            {
                duplicateCount++;
                _log.Info($"Skipped duplicate queue item: {normalizedUrl}");
                continue;
            }

            var queueItem = CreateQueueItem(
                sourceType: "OnlineVideo",
                title: "読み込み中...",
                sourcePathOrUrl: normalizedUrl,
                status: QueueStatusMetadataLoading,
                isSimpleModeItem: isSimpleModeItem);
            _conversionQueue.Add(queueItem);
            addedCount++;
            pendingMetadataItems.Add((queueItem, normalizedUrl));
            ApplyQueueSupportStatusForAdd(queueItem, isSimpleModeItem);
            _log.Info($"Added dropped URL to queue for metadata loading: {normalizedUrl}");
        }

        RefreshQueueOrderNumbers();

        foreach (var (queueItem, normalizedUrl) in pendingMetadataItems)
        {
            var video = await FetchDroppedUrlMetadataAsync(normalizedUrl);
            if (video is null)
            {
                fallbackCount++;
                var fallbackTitle = CreateFallbackOnlineVideoTitle(normalizedUrl);
                _log.Warn($"Using fallback title for dropped URL: {fallbackTitle}");
                queueItem.Title = fallbackTitle;
                queueItem.Status = QueueStatusReadyWithWarning;
                ApplyQueueSupportStatusForAdd(queueItem, isSimpleModeItem);
                _log.Info($"Dropped URL metadata fallback is ready: {normalizedUrl}");
                continue;
            }

            queueItem.Title = string.IsNullOrWhiteSpace(video.Title)
                ? CreateFallbackOnlineVideoTitle(normalizedUrl)
                : video.Title;
            queueItem.Status = QueueStatusReady;
            ApplyQueueSupportStatusForAdd(queueItem, isSimpleModeItem);
            _log.Info($"Dropped URL metadata resolved: {queueItem.Title}");
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Added {addedCount} online URL item(s) to the conversion queue from {sourceLabel}. Skipped {duplicateCount} duplicate item(s). Used {fallbackCount} fallback title(s).");
    }

    private async Task<VideoListItem?> FetchDroppedUrlMetadataAsync(string normalizedUrl)
    {
        _log.Info($"Fetching metadata for dropped URL: {normalizedUrl}");
        try
        {
            var result = await _videoMetadataService.FetchVideoListAsync(normalizedUrl, message => _log.Info(message));
            if (!result.IsSuccess || result.Videos.Count == 0)
            {
                _log.Warn($"Metadata fetch failed for dropped URL. Exit code: {result.ExitCode?.ToString() ?? "unknown"}.");
                LogProcessOutput(result.StandardError, "yt-dlp stderr");
                LogProcessOutput(result.StandardOutput, "yt-dlp stdout");
                return null;
            }

            var video = result.Videos[0];
            _log.Info($"Fetched dropped URL title: {video.Title}");
            return video;
        }
        catch (Exception ex)
        {
            _log.Warn($"Metadata fetch failed for dropped URL: {ex.Message}");
            return null;
        }
    }

    private static string CreateFallbackOnlineVideoTitle(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Online Video";
        }

        var id = TryGetYouTubeVideoId(uri) ?? GetLastPathSegment(uri);
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        var fallback = string.IsNullOrWhiteSpace(id)
            ? host
            : $"{host} {id}";

        return SafeFileName.Create(fallback, "Online Video");
    }

    private static UrlValidationResult NormalizeSingleVideoUrl(string text)
    {
        var originalText = text.Trim();
        if (!Uri.TryCreate(originalText, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return UrlValidationResult.Allowed(text);
        }

        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be")
        {
            var videoId = GetLastPathSegment(uri);
            return string.IsNullOrWhiteSpace(videoId)
                ? UrlValidationResult.Rejected(text, UnsafeOnlineUrlReason)
                : UrlValidationResult.Allowed(BuildYouTubeWatchUrl(videoId));
        }

        if (!IsYouTubeHost(host))
        {
            return UrlValidationResult.Allowed(text);
        }

        var path = uri.AbsolutePath.Trim('/');
        if (string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase))
        {
            var queryValues = ParseQueryValues(uri.Query);
            return queryValues.TryGetValue("v", out var watchVideoId) && !string.IsNullOrWhiteSpace(watchVideoId)
                ? UrlValidationResult.Allowed(BuildYouTubeWatchUrl(watchVideoId))
                : UrlValidationResult.Rejected(text, UnsafeOnlineUrlReason);
        }

        if (path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase))
        {
            var videoId = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            return string.IsNullOrWhiteSpace(videoId)
                ? UrlValidationResult.Rejected(text, UnsafeOnlineUrlReason)
                : UrlValidationResult.Allowed(BuildYouTubeWatchUrl(videoId));
        }

        if (path.Equals("playlist", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("channel/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("@", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("c/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("radio", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("radio/", StringComparison.OrdinalIgnoreCase))
        {
            return UrlValidationResult.Rejected(text, UnsafeOnlineUrlReason);
        }

        return UrlValidationResult.Rejected(text, UnsafeOnlineUrlReason);
    }

    private static string? TryGetYouTubeVideoId(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be")
        {
            return GetLastPathSegment(uri);
        }

        if (!IsYouTubeHost(host) || !string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var queryValues = ParseQueryValues(uri.Query);
        return queryValues.TryGetValue("v", out var videoId) && !string.IsNullOrWhiteSpace(videoId)
            ? videoId
            : null;
    }

    private static string? GetLastPathSegment(Uri uri)
    {
        return uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static bool IsYouTubeHost(string host)
    {
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildYouTubeWatchUrl(string videoId)
    {
        return $"https://www.youtube.com/watch?v={Uri.EscapeDataString(Uri.UnescapeDataString(videoId))}";
    }

    private sealed record UrlValidationResult(bool IsAllowed, string Url, string Reason)
    {
        public static UrlValidationResult Allowed(string url)
        {
            return new UrlValidationResult(true, url, string.Empty);
        }

        public static UrlValidationResult Rejected(string url, string reason)
        {
            return new UrlValidationResult(false, url, reason);
        }
    }

    private static Dictionary<string, string> ParseQueryValues(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0].Replace("+", " "));
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            values[key] = pieces.Length > 1
                ? Uri.UnescapeDataString(pieces[1].Replace("+", " "))
                : string.Empty;
        }

        return values;
    }

    private static IEnumerable<string> GetDroppedUrls(string droppedText)
    {
        foreach (var token in droppedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim().Trim('<', '>', '"', '\'');
            if (IsHttpUrl(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? GetDroppedText(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.UnicodeText)
            && data.GetData(DataFormats.UnicodeText) is string unicodeText
            && !string.IsNullOrWhiteSpace(unicodeText))
        {
            return unicodeText.Trim();
        }

        if (data.GetDataPresent(DataFormats.Text)
            && data.GetData(DataFormats.Text) is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        if (data.GetDataPresent("UniformResourceLocatorW"))
        {
            return DecodeDroppedUrlData(data.GetData("UniformResourceLocatorW"), isUnicode: true);
        }

        if (data.GetDataPresent("UniformResourceLocator"))
        {
            return DecodeDroppedUrlData(data.GetData("UniformResourceLocator"), isUnicode: false);
        }

        return null;
    }

    private static string? DecodeDroppedUrlData(object? data, bool isUnicode)
    {
        byte[]? bytes = data switch
        {
            byte[] byteArray => byteArray,
            MemoryStream memoryStream => memoryStream.ToArray(),
            _ => null,
        };
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var text = isUnicode
            ? System.Text.Encoding.Unicode.GetString(bytes)
            : System.Text.Encoding.ASCII.GetString(bytes);
        text = text.Trim('\0', '\r', '\n', ' ', '\t');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool IsHttpUrl(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private void LogDroppedUrlNormalization(string originalUrl, string normalizedUrl)
    {
        _log.Info($"Dropped URL: {originalUrl}");
        if (!string.Equals(originalUrl, normalizedUrl, StringComparison.Ordinal))
        {
            _log.Info($"Normalized dropped URL: {normalizedUrl}");
        }
    }

    private static string GetLogPreview(string text)
    {
        var preview = text.Replace(Environment.NewLine, " ").Trim();
        return preview.Length <= 120 ? preview : $"{preview[..120]}...";
    }

    private void UpdateQueueUnsupportedStatusesForCurrentMode()
    {
        if (_isQueueConverting || QueueExecutionModeComboBox is null)
        {
            return;
        }

        var executionMode = GetQueueExecutionMode();
        foreach (var item in _conversionQueue)
        {
            if (IsIdleQueueStatus(item.Status))
            {
                ApplyQueueSupportStatus(item, executionMode);
            }
        }
    }

    private static void ApplyQueueSupportStatus(ConversionQueueItem item, string executionMode)
    {
        var reason = GetUnsupportedReason(item, executionMode);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            item.UnsupportedReason = reason;
            item.Status = QueueStatusUnsupported;
            return;
        }

        item.UnsupportedReason = string.Empty;
        if (item.Status is "Pending" or QueueStatusUnsupported)
        {
            item.Status = QueueStatusReady;
        }
    }

    private void ApplyQueueSupportStatusForAdd(
        ConversionQueueItem item,
        bool isSimpleModeItem,
        string? executionMode = null)
    {
        if (isSimpleModeItem)
        {
            ApplySimpleModeSupportStatus(item);
            return;
        }

        ApplyQueueSupportStatus(item, executionMode ?? GetQueueExecutionMode());
    }

    private static void ApplySimpleModeSupportStatus(ConversionQueueItem item)
    {
        if (item.SourceType is "OnlineVideo" or "LocalFile")
        {
            item.UnsupportedReason = string.Empty;
            if (item.Status is "Pending" or QueueStatusUnsupported)
            {
                item.Status = QueueStatusReady;
            }

            return;
        }

        item.UnsupportedReason = string.IsNullOrWhiteSpace(item.UnsupportedReason)
            ? UnsupportedCurrentModeReason
            : item.UnsupportedReason;
        item.Status = QueueStatusUnsupported;
    }

    private static string GetUnsupportedReason(ConversionQueueItem item, string executionMode)
    {
        if (item.SourceType == "Unsupported")
        {
            return string.IsNullOrWhiteSpace(item.UnsupportedReason)
                ? UnsupportedCurrentModeReason
                : item.UnsupportedReason;
        }

        if (item.SourceType == "LocalFile" && !File.Exists(item.SourcePathOrUrl))
        {
            return MissingLocalFileReason;
        }

        return executionMode switch
        {
            "Download Only" when item.SourceType == "LocalFile" => UnsupportedLocalFileModeReason,
            "Convert Only" when item.SourceType == "OnlineVideo" => UnsupportedUrlModeReason,
            "Copy Files" when item.SourceType == "OnlineVideo" => UnsupportedUrlModeReason,
            _ => string.Empty,
        };
    }

    private static bool IsIdleQueueStatus(string status)
    {
        return status is "Pending"
            or QueueStatusReady
            or QueueStatusMetadataLoading
            or QueueStatusReadyWithWarning
            or QueueStatusUnsupported;
    }

    private void AddLocalFilesToQueue(IEnumerable<string> paths, string sourceLabel, bool isSimpleModeItem = false)
    {
        var addedCount = 0;
        var unsupportedCount = 0;
        var duplicateCount = 0;
        var ignoredFolderCount = 0;
        var executionMode = GetQueueExecutionMode();

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(path))
            {
                ignoredFolderCount++;
                AddUnsupportedDroppedItem(path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), UnsupportedFileFormatReason, isSimpleModeItem);
                _log.Info($"Added unsupported folder drop to queue. Folder scanning is not enabled in this version: {path}");
                continue;
            }

            if (!File.Exists(path))
            {
                unsupportedCount++;
                AddUnsupportedDroppedItem(path, Path.GetFileNameWithoutExtension(path), UnsupportedFileFormatReason, isSimpleModeItem);
                _log.Error($"Added missing dropped file to queue as unsupported: {path}");
                continue;
            }

            if (!IsSupportedLocalVideoFile(path))
            {
                unsupportedCount++;
                AddUnsupportedDroppedItem(path, Path.GetFileNameWithoutExtension(path), UnsupportedFileFormatReason, isSimpleModeItem);
                _log.Info($"Added unsupported file to queue: {path}");
                continue;
            }

            if (QueueContainsSource(path))
            {
                duplicateCount++;
                _log.Info($"Skipped duplicate queue item: {path}");
                continue;
            }

            _conversionQueue.Add(CreateQueueItem(
                sourceType: "LocalFile",
                title: Path.GetFileNameWithoutExtension(path),
                sourcePathOrUrl: path,
                status: QueueStatusReady,
                isSimpleModeItem: isSimpleModeItem));
            ApplyQueueSupportStatusForAdd(_conversionQueue[^1], isSimpleModeItem, executionMode);
            addedCount++;
            _log.Info($"Added local file to queue: {path}");
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Added {addedCount} local file(s) to the conversion queue from {sourceLabel}. Skipped {unsupportedCount} unsupported/missing, {duplicateCount} duplicate, {ignoredFolderCount} folder item(s).");
    }

    private void AddUnsupportedDroppedItem(string sourcePathOrUrl, string title, string reason, bool isSimpleModeItem = false)
    {
        if (QueueContainsSource(sourcePathOrUrl))
        {
            _log.Info($"Skipped duplicate unsupported queue item: {sourcePathOrUrl}");
            return;
        }

        _conversionQueue.Add(CreateQueueItem(
            sourceType: "Unsupported",
            title: string.IsNullOrWhiteSpace(title) ? "対象外の項目" : title,
            sourcePathOrUrl: sourcePathOrUrl,
            status: QueueStatusUnsupported,
            unsupportedReason: reason,
            isSimpleModeItem: isSimpleModeItem));
    }

    private bool QueueContainsSource(string sourcePathOrUrl)
    {
        return _conversionQueue.Any(item =>
            string.Equals(item.SourcePathOrUrl, sourcePathOrUrl, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildToolStatusMessage(ExternalToolResult result)
    {
        if (result.IsAvailable)
        {
            return result.Message;
        }

        return string.Join(
            Environment.NewLine,
            result.Message,
            "設定画面で手動指定するか、メニューの「外部ツールを自動取得」を実行してください。");
    }

    private void CopyCommandToClipboard(string command)
    {
        Clipboard.SetText(command);
        _log.Info($"Copied command to clipboard: {command.Replace(Environment.NewLine, " ; ")}");
    }

    private void Videos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (VideoListItem item in e.OldItems)
            {
                item.PropertyChanged -= Video_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (VideoListItem item in e.NewItems)
            {
                item.PropertyChanged += Video_PropertyChanged;
            }
        }

        UpdateDownloadButtonState();
        UpdateSectionHeaders();
    }

    private void ConversionQueue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ConversionQueueItem item in e.OldItems)
            {
                item.PropertyChanged -= ConversionQueueItem_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ConversionQueueItem item in e.NewItems)
            {
                item.PropertyChanged += ConversionQueueItem_PropertyChanged;
            }
        }

        UpdateConvertQueueButtonState();
        MarkPlaylistDirty();
        if (!_isQueueConverting)
        {
            ClearQueueProgress();
            return;
        }

        UpdateSectionHeaders();
    }

    private void Video_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoListItem.IsSelected))
        {
            UpdateDownloadButtonState();
            UpdateConvertButtonState();
        }

        if (e.PropertyName is nameof(VideoListItem.Status) or nameof(VideoListItem.DownloadedFilePath))
        {
            UpdateConvertButtonState();
        }
    }

    private void ConversionQueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConversionQueueItem.Title)
            or nameof(ConversionQueueItem.Order)
            or nameof(ConversionQueueItem.AudioAdjustmentMode))
        {
            MarkPlaylistDirty();
        }
        if (e.PropertyName is nameof(ConversionQueueItem.IsSelected)
            or nameof(ConversionQueueItem.Status)
            or nameof(ConversionQueueItem.SourcePathOrUrl))
        {
            UpdateConvertQueueButtonState();
        }

        if (_isQueueConverting
            && (e.PropertyName is nameof(ConversionQueueItem.ProgressPercent)
                or nameof(ConversionQueueItem.Status)))
        {
            RefreshQueueProgressFromStatuses(_activeQueueProgressItems.Count > 0 ? _activeQueueProgressItems : _conversionQueue);
        }
    }

    private void UpdateDownloadButtonState()
    {
        DownloadSelectedButton.IsEnabled = !_isDownloading
            && !_isConverting
            && !_isQueueConverting
            && _videos.Any(static video => video.IsSelected && video.SourceType == "YouTube")
            && !string.IsNullOrWhiteSpace(_settings.WorkingFolder);
    }

    private void UpdateConvertButtonState()
    {
        ConvertDownloadedButton.IsEnabled = !_isDownloading
            && !_isConverting
            && !_isQueueConverting
            && _videos.Any(static video => video.IsSelected
                && ((!string.IsNullOrWhiteSpace(video.DownloadedFilePath))
                    || (video.SourceType == "LocalFile" && !string.IsNullOrWhiteSpace(video.SourcePath))))
            && !string.IsNullOrWhiteSpace(_settings.ConvertedFolder);
    }

    private void UpdateConvertQueueButtonState()
    {
        ConvertQueueButton.IsEnabled = !_isDownloading
            && !_isConverting
            && !_isQueueConverting
            && _conversionQueue.Count > 0
            && !string.IsNullOrWhiteSpace(_settings.ConvertedFolder);
        RetryFailedQueueButton.IsEnabled = !_isDownloading
            && !_isConverting
            && !_isQueueConverting
            && _conversionQueue.Any(static item => item.Status == "Failed");
    }

    private static bool IsSupportedLocalVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            || IsSupportedLocalAudioFile(filePath);
    }

    private static bool IsSupportedLocalAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".aac", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".flac", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wma", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConversionInputFilePath(VideoListItem video)
    {
        if (video.SourceType == "LocalFile")
        {
            return video.SourcePath;
        }

        return video.DownloadedFilePath;
    }

    private void OpenConfiguredFolder(string folderPath, string label)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            _log.Error($"{label} is not configured. Open Settings and choose a folder.");
            return;
        }

        try
        {
            Directory.CreateDirectory(folderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
            });
            _log.Info($"Opened {label}: {folderPath}");
        }
        catch (Exception ex)
        {
            _log.Error($"{label} could not be opened. {ex.Message}");
            MessageBox.Show(this, ex.Message, $"Open {label} Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void MarkRemainingAsSkipped(List<VideoListItem> selectedVideos, VideoListItem currentVideo)
    {
        var currentIndex = selectedVideos.IndexOf(currentVideo);
        for (var index = currentIndex + 1; index < selectedVideos.Count; index++)
        {
            if (selectedVideos[index].Status is not "Downloaded" and not "Failed")
            {
                selectedVideos[index].Status = "Skipped";
            }
        }
    }

    private static void MarkRemainingQueueItemsAsSkipped(
        List<ConversionQueueItem> selectedItems,
        ConversionQueueItem currentItem)
    {
        var currentIndex = selectedItems.IndexOf(currentItem);
        for (var index = currentIndex; index < selectedItems.Count; index++)
        {
            if (selectedItems[index].Status is not "Downloaded" and not "Converted" and not "Failed")
            {
                selectedItems[index].Status = "Skipped";
            }
        }
    }
}
