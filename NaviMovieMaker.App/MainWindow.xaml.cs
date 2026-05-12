using System.Windows;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NaviMovieMaker.App.Services;

namespace NaviMovieMaker.App;

public partial class MainWindow : Window
{
    private const int MaxLogEntryCount = 3000;
    private const int MaxDownloadAttempts = 3;
    private const string YtDlpInstallCommand = "winget install yt-dlp.yt-dlp";
    private const string FfmpegSearchCommand = "winget search ffmpeg";
    private const string FfmpegInstallCommand = "winget install Gyan.FFmpeg";
    private const string YtDlpUpdateCommand = "winget upgrade yt-dlp.yt-dlp";
    private const string FfmpegUpdateCommand = "winget upgrade Gyan.FFmpeg";
    private const string QueueReorderDragFormat = "NaviMovieMaker.QueueReorder";
    private const string AudioOnlyPresetFolderName = "Audio_MP4_AAC_Only";
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
    private readonly AppLog _log = new();
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly ObservableCollection<VideoListItem> _videos = new();
    private readonly ObservableCollection<ConversionQueueItem> _conversionQueue = new();
    private readonly Dictionary<string, ImageSource?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings;
    private string _sessionOutputFolder = string.Empty;
    private ExternalToolResult? _lastYtDlpResult;
    private ExternalToolResult? _lastFfmpegResult;
    private CancellationTokenSource? _downloadCancellationTokenSource;
    private CancellationTokenSource? _queueCancellationTokenSource;
    private Point? _queueDragStartPoint;
    private List<ConversionQueueItem> _draggedQueueItems = [];
    private int _queueProgressProcessed;
    private int _queueProgressTotal;
    private int? _activeNumberPrefixStartNumber;
    private bool _isDownloading;
    private bool _isConverting;
    private bool _isQueueConverting;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load(out var settingsWarning);
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
        _log.Info("Application started.");
        _log.Info("SD card copying and playback order sorting are handled outside NaviMovie-Maker, for example with Explorer and UMSSort.");
    }

    private async void CheckExternalToolsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckExternalToolsMenuItem.IsEnabled = false;
        ExternalToolsStatusTextBlock.Text = "External tools: Checking...";
        _log.Info("Checking external tools from PATH.");

        try
        {
            var ytDlpTask = _externalToolService.CheckYtDlpAsync();
            var ffmpegTask = _externalToolService.CheckFfmpegAsync();

            var ytDlpResult = await ytDlpTask;
            var ffmpegResult = await ffmpegTask;

            _lastYtDlpResult = ytDlpResult;
            _lastFfmpegResult = ffmpegResult;
            LogToolResult(ytDlpResult);
            LogToolResult(ffmpegResult);
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

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
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
        _sessionOutputFolder = _settings.ConvertedFolder;
        PopulateOutputPresetComboBox();
        UpdateDownloadButtonState();
        UpdateConvertButtonState();
        UpdateConvertQueueButtonState();
        _log.Info($"Settings saved: {_settingsService.SettingsFilePath}");
        _log.Info("Configured folders were created if they did not already exist.");
    }

    private void OpenWorkingFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.WorkingFolder, "Working Folder");
    }

    private void OpenConvertedFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.ConvertedFolder, "Converted Folder");
    }

    private void OpenTemporaryFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.TemporaryFolder, "Temporary Folder");
    }

    private void OpenLocalVideoFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredFolder(_settings.LocalVideoFolder, "Local Video Folder");
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
            _sessionOutputFolder = dialog.FolderName;
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

            _conversionQueue.Add(new ConversionQueueItem
            {
                SourceType = "OnlineVideo",
                Title = video.Title,
                SourcePathOrUrl = sourcePathOrUrl,
                DownloadedFilePath = video.DownloadedFilePath,
                ConvertedFilePath = video.ConvertedFilePath,
                Status = "Pending",
            });
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
        UpdateAspectModeSelector();
    }

    private void AudioAdjustmentComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateAudioAdjustmentControls();
    }

    private void PeakBoostCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateAudioAdjustmentControls();
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
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ConversionQueue_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        AddLocalFilesToQueue(paths, "drag and drop");
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

        var data = new DataObject();
        data.SetData(QueueReorderDragFormat, true);
        DragDrop.DoDragDrop(ConversionQueueDataGrid, data, DragDropEffects.Move);
        _queueDragStartPoint = null;
    }

    private void ConversionQueueDataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(QueueReorderDragFormat) && _draggedQueueItems.Count > 0
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ConversionQueueDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddLocalFilesToQueue(paths, "drag and drop");
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(QueueReorderDragFormat) || _draggedQueueItems.Count == 0)
        {
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
        if (ConversionQueueDataGrid.SelectedItems.Count > 1)
        {
            _log.Info("Move Up supports one selected queue row at a time.");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItem is not ConversionQueueItem selectedItem)
        {
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
        if (ConversionQueueDataGrid.SelectedItems.Count > 1)
        {
            _log.Info("Move Down supports one selected queue row at a time.");
            return;
        }

        if (ConversionQueueDataGrid.SelectedItem is not ConversionQueueItem selectedItem)
        {
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

    private void RemoveSelectedQueueItems()
    {
        var selectedItems = ConversionQueueDataGrid.SelectedItems
            .OfType<ConversionQueueItem>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            return;
        }

        foreach (var selectedItem in selectedItems)
        {
            _conversionQueue.Remove(selectedItem);
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Removed {selectedItems.Count} item(s) from the conversion queue.");
    }

    private void QueueClearButton_Click(object sender, RoutedEventArgs e)
    {
        var removedCount = _conversionQueue.Count;
        if (removedCount == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Remove all {removedCount} item(s) from the conversion queue?",
            "Clear Queue",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _conversionQueue.Clear();
        RefreshQueueOrderNumbers();
        _log.Info($"Cleared {removedCount} item(s) from the conversion queue.");
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

        _isQueueConverting = true;
        _queueCancellationTokenSource = new CancellationTokenSource();
        var selectedPreset = GetSelectedConversionPreset();
        var downloadProfile = RequiresDownloadProfile(executionMode)
            ? ResolveDownloadProfile(executionMode, selectedPreset)
            : null;
        var convertedOutputStartNumber = GetNumberPrefixStartNumber();
        _activeNumberPrefixStartNumber = convertedOutputStartNumber;
        foreach (var item in selectedItems)
        {
            item.Status = "Pending";
        }

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
            _log.Info($"Global Peak Boost: {(PeakBoostCheckBox.IsChecked == true ? "On" : "Off")}");
            if (PeakBoostCheckBox.IsChecked == true)
            {
                _log.Info($"Peak Boost target peak: {GetSelectedTargetPeakDb():0.0} dBFS");
            }

            _log.Info("Per-item Loudness Normalize overrides global Peak Boost.");
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
            _isQueueConverting = false;
            SetQueueConversionState(isConverting: false);
            _log.Info("Queue task finished.");
        }
    }

    private void CancelQueueButton_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Cancel requested. Stopping current queue process...");
        _queueCancellationTokenSource?.Cancel();
    }

    private Task RunCopyFilesQueueItemAsync(ConversionQueueItem item, int outputOrder)
    {
        if (item.SourceType == "OnlineVideo")
        {
            item.Status = "Skipped";
            _log.Info($"Skipping {item.Order:000}: Online videos are skipped in Copy Files mode.");
            return Task.CompletedTask;
        }

        if (item.SourceType != "LocalFile")
        {
            item.Status = "Skipped";
            _log.Error($"Skipping {item.Order:000}: unsupported queue source type for Copy Files mode: {item.SourceType}");
            return Task.CompletedTask;
        }

        var sourcePath = item.SourcePathOrUrl;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            item.Status = "Failed";
            _log.Error($"Copy failed for queue item {item.Order:000}: source file is missing: {sourcePath}");
            return Task.CompletedTask;
        }

        try
        {
            item.Status = "Copying";
            var outputFolder = GetBaseOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var safeTitle = SafeFileName.Create(item.Title, Path.GetFileNameWithoutExtension(sourcePath));
            var extension = Path.GetExtension(sourcePath);
            var outputStem = _activeNumberPrefixStartNumber is not null
                ? $"{outputOrder:000}_{safeTitle}"
                : safeTitle;
            var destinationPath = PathHelper.GetUniqueFilePath(outputFolder, outputStem, extension);
            LogOutputConflictIfNeeded(outputFolder, outputStem, extension, destinationPath);

            _log.Info($"Copy Files output folder: {outputFolder}");
            _log.Info($"Copy source path: {sourcePath}");
            File.Copy(sourcePath, destinationPath);
            item.ConvertedFilePath = destinationPath;
            item.Status = "Completed";
            _log.Info($"Copy destination path: {destinationPath}");
        }
        catch (Exception ex)
        {
            item.Status = "Failed";
            _log.Error($"Copy failed for queue item {item.Order:000}: {item.Title}. {ex.Message}");
        }

        return Task.CompletedTask;
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
            item.Status = "Skipped";
            return;
        }

        if (result.IsSuccess)
        {
            item.DownloadedFilePath = result.DownloadedFilePath ?? string.Empty;
            item.Status = "Downloaded";
            _log.Info($"Download output path: {item.DownloadedFilePath}");
            return;
        }

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
            item.Status = "Skipped";
            if (!keepOriginalDownloadedFile && !string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
            {
                DeleteTemporaryDownload(downloadResult.DownloadedFilePath);
            }

            return;
        }

        if (!downloadResult.IsSuccess || string.IsNullOrWhiteSpace(downloadResult.DownloadedFilePath))
        {
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
        item.Status = "Downloading";
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

            item.Status = "Downloading";
            _log.Info($"yt-dlp download attempt {attempt}/{MaxDownloadAttempts} for: {item.Title}");

            lastResult = await _videoDownloadService.DownloadAsync(
                downloadItem,
                outputFolder,
                outputOrder,
                message => _log.Info(message),
                downloadProfile,
                addNumberPrefix,
                cancellationToken);

            _log.Info($"yt-dlp attempt {attempt}/{MaxDownloadAttempts} finished for {item.Title}. Exit code: {lastResult.ExitCode?.ToString() ?? "unknown"}");

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

    private async Task ConvertQueueItemAsync(
        ConversionQueueItem item,
        string inputFilePath,
        int outputOrder,
        ConversionPreset preset,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
        {
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
        var outputFilePath = PathHelper.GetUniqueFilePath(outputFolder, outputStem, outputExtension);
        LogOutputConflictIfNeeded(outputFolder, outputStem, outputExtension, outputFilePath);

        item.Status = "Converting";
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
            item.Status = cancellationToken.IsCancellationRequested ? "Skipped" : "Failed";
            return;
        }

        var result = useAudioOutputPreset
            ? await _videoConversionService.ConvertAudioPresetAsync(
                inputFilePath,
                outputFilePath,
                message => _log.Info(message),
                preset,
                audioFilter,
                cancellationToken)
            : isAudioOnlyInput
            ? await _videoConversionService.ConvertAudioOnlyMp4Async(
                inputFilePath,
                outputFilePath,
                message => _log.Info(message),
                audioFilter,
                cancellationToken)
            : await _videoConversionService.ConvertAsync(
                inputFilePath,
                outputFilePath,
                message => _log.Info(message),
                preset,
                GetSelectedAspectMode(),
                audioFilter,
                cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            item.Status = "Skipped";
            _log.Info($"Queue conversion canceled for {item.Order:000}: {item.Title}");
            return;
        }

        if (result.IsSuccess)
        {
            item.ConvertedFilePath = result.OutputFilePath;
            item.Status = "Converted";
            _log.Info($"Conversion output path: {result.OutputFilePath}");
            return;
        }

        item.Status = "Failed";
        _log.Error($"Queue conversion failed for {item.Order:000}: {item.Title}");
        LogProcessOutput(result.StandardError, "ffmpeg stderr");
        LogProcessOutput(result.StandardOutput, "ffmpeg stdout");
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
            && item.Content is string mode)
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

        var previousPresetId = OutputPresetComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem
            ? selectedItem.Tag?.ToString()
            : null;

        OutputPresetComboBox.SelectionChanged -= OutputPresetComboBox_SelectionChanged;
        OutputPresetComboBox.Items.Clear();

        foreach (var preset in GetVisibleOutputPresets())
        {
            OutputPresetComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = preset.DisplayName,
                Tag = preset.Id,
            });
        }

        var itemToSelect = OutputPresetComboBox.Items
            .OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), previousPresetId, StringComparison.OrdinalIgnoreCase))
            ?? OutputPresetComboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault();

        OutputPresetComboBox.SelectedItem = itemToSelect;
        OutputPresetComboBox.SelectionChanged += OutputPresetComboBox_SelectionChanged;
        UpdateAspectModeSelector();
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
            ? item.Content?.ToString() ?? "Keep aspect ratio + padding"
            : "Keep aspect ratio + padding";
    }

    private AudioAdjustmentMode GetSelectedAudioAdjustmentMode()
    {
        var adjustmentText = AudioAdjustmentComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            ? item.Content?.ToString() ?? "Off"
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

    private async Task<string?> BuildAudioFilterAsync(
        ConversionQueueItem item,
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        _log.Info($"Audio adjustment for {item.Title}: {GetAudioAdjustmentDisplay(item.AudioAdjustmentMode)}");
        switch (item.AudioAdjustmentMode)
        {
            case AudioAdjustmentMode.LoudnessNormalize:
                _log.Info($"Peak Boost skipped for {item.Title}: per-item Loudness Normalize is selected.");
                _log.Info("Audio adjustment filter: loudnorm=I=-16:LRA=11:TP=-1.5");
                return "loudnorm=I=-16:LRA=11:TP=-1.5";
            case AudioAdjustmentMode.Off when PeakBoostCheckBox.IsChecked == true:
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

        var gainText = gainDb.ToString("0.###", CultureInfo.InvariantCulture);
        var filter = $"volume={gainText}dB,alimiter=limit=0.98";
        _log.Info($"Audio boost applied: {filter}");
        return filter;
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
        if (AudioAdjustmentComboBox is null || TargetPeakComboBox is null || TargetPeakLabel is null)
        {
            return;
        }

        var isPeakMode = PeakBoostCheckBox.IsChecked == true;
        TargetPeakLabel.IsEnabled = isPeakMode;
        TargetPeakComboBox.IsEnabled = isPeakMode;
    }

    private static string GetAudioAdjustmentDisplay(AudioAdjustmentMode mode)
    {
        return mode switch
        {
            AudioAdjustmentMode.LoudnessNormalize => "Loudness normalize",
            _ => "Off",
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
            _log.Info($"Output already exists. Using: {selectedPath}");
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
        if (_lastYtDlpResult is null || _lastFfmpegResult is null)
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
        QueueExecutionModeComboBox.IsEnabled = !isDownloading;
        OutputPresetComboBox.IsEnabled = !isDownloading;
        AspectModeComboBox.IsEnabled = !isDownloading && GetSelectedConversionPreset().SupportsAspectMode;
        KeepOriginalDownloadedFilesCheckBox.IsEnabled = !isDownloading;
        NumberPrefixTextBox.IsEnabled = !isDownloading;
        PeakBoostCheckBox.IsEnabled = !isDownloading;
        AudioAdjustmentComboBox.IsEnabled = !isDownloading;
        ApplyAudioAdjustmentButton.IsEnabled = !isDownloading;
        TargetPeakLabel.IsEnabled = !isDownloading && PeakBoostCheckBox.IsChecked == true;
        TargetPeakComboBox.IsEnabled = !isDownloading && PeakBoostCheckBox.IsChecked == true;
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
        QueueExecutionModeComboBox.IsEnabled = !isConverting;
        OutputPresetComboBox.IsEnabled = !isConverting;
        AspectModeComboBox.IsEnabled = !isConverting && GetSelectedConversionPreset().SupportsAspectMode;
        KeepOriginalDownloadedFilesCheckBox.IsEnabled = !isConverting;
        NumberPrefixTextBox.IsEnabled = !isConverting;
        PeakBoostCheckBox.IsEnabled = !isConverting;
        AudioAdjustmentComboBox.IsEnabled = !isConverting;
        ApplyAudioAdjustmentButton.IsEnabled = !isConverting;
        TargetPeakLabel.IsEnabled = !isConverting && PeakBoostCheckBox.IsChecked == true;
        TargetPeakComboBox.IsEnabled = !isConverting && PeakBoostCheckBox.IsChecked == true;
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
        TargetPeakLabel.IsEnabled = !isConverting && PeakBoostCheckBox.IsChecked == true;
        TargetPeakComboBox.IsEnabled = !isConverting && PeakBoostCheckBox.IsChecked == true;
        RetryFailedQueueButton.IsEnabled = !isConverting;
        CancelQueueButton.IsEnabled = isConverting;
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
    }

    private void UpdateSectionHeaders()
    {
        CandidatesExpander.Header = $"Video Source ({_videos.Count})";
        var queueHeader = $"Conversion Queue ({_conversionQueue.Count} item{(_conversionQueue.Count == 1 ? string.Empty : "s")})";
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
        UpdateQueueProgressDisplay();
    }

    private void ClearQueueProgress()
    {
        _queueProgressTotal = 0;
        _queueProgressProcessed = 0;
        UpdateQueueProgressDisplay();
    }

    private void RefreshQueueProgressFromStatuses(IReadOnlyCollection<ConversionQueueItem> queueItems)
    {
        if (_queueProgressTotal <= 0)
        {
            return;
        }

        _queueProgressProcessed = Math.Min(queueItems.Count(IsProcessedQueueStatus), _queueProgressTotal);
        UpdateQueueProgressDisplay();
    }

    private void UpdateQueueProgressDisplay()
    {
        if (QueueProgressBar is null || QueueProgressTextBlock is null)
        {
            return;
        }

        QueueProgressBar.Maximum = _queueProgressTotal > 0 ? _queueProgressTotal : 1;
        QueueProgressBar.Value = Math.Min(_queueProgressProcessed, QueueProgressBar.Maximum);
        QueueProgressTextBlock.Text = _queueProgressTotal > 0
            ? $"{_queueProgressProcessed}/{_queueProgressTotal}  {GetQueueProgressPercent()}%"
            : "Ready";
        UpdateSectionHeaders();
    }

    private int GetQueueProgressPercent()
    {
        return _queueProgressTotal > 0
            ? (int)Math.Round(_queueProgressProcessed * 100.0 / _queueProgressTotal)
            : 0;
    }

    private static bool IsProcessedQueueStatus(ConversionQueueItem item)
    {
        return item.Status is "Converted" or "Downloaded" or "Completed" or "Failed" or "Skipped";
    }

    private void UpdateMainWorkspaceLayout()
    {
        if (CandidatesExpander is null || ConversionQueueExpander is null)
        {
            return;
        }

        VideoListWorkRow.MinHeight = CandidatesExpander.IsExpanded ? 120 : 0;
        VideoListWorkRow.Height = CandidatesExpander.IsExpanded
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        QueueWorkRow.MinHeight = ConversionQueueExpander.IsExpanded ? 120 : 0;
        QueueWorkRow.Height = ConversionQueueExpander.IsExpanded
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        CandidateQueueGridSplitter.Visibility = CandidatesExpander.IsExpanded && ConversionQueueExpander.IsExpanded
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

        _log.Info($"Loading video list from URL: {url}");
        return url;
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
        FetchVideoListButton.Content = isSearchMode ? "Search Videos" : "Load from URL";
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

    private void AddLocalFilesToQueue(IEnumerable<string> paths, string sourceLabel)
    {
        var addedCount = 0;
        var unsupportedCount = 0;
        var duplicateCount = 0;
        var ignoredFolderCount = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                ignoredFolderCount++;
                _log.Info($"Skipped folder drop. Folder scanning is not enabled in this version: {path}");
                continue;
            }

            if (!File.Exists(path))
            {
                unsupportedCount++;
                _log.Error($"Skipped missing file: {path}");
                continue;
            }

            if (!IsSupportedLocalVideoFile(path))
            {
                unsupportedCount++;
                _log.Info($"Skipped unsupported file: {path}");
                continue;
            }

            if (QueueContainsSource(path))
            {
                duplicateCount++;
                _log.Info($"Skipped duplicate queue item: {path}");
                continue;
            }

            _conversionQueue.Add(new ConversionQueueItem
            {
                SourceType = "LocalFile",
                Title = Path.GetFileNameWithoutExtension(path),
                SourcePathOrUrl = path,
                Status = "Pending",
            });
            addedCount++;
            _log.Info($"Added local file to queue: {path}");
        }

        RefreshQueueOrderNumbers();
        _log.Info($"Added {addedCount} local file(s) to the conversion queue from {sourceLabel}. Skipped {unsupportedCount} unsupported/missing, {duplicateCount} duplicate, {ignoredFolderCount} folder item(s).");
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
            var updateCommand = result.ToolName.Equals("yt-dlp", StringComparison.OrdinalIgnoreCase)
                ? YtDlpUpdateCommand
                : FfmpegUpdateCommand;

            return string.Join(
                Environment.NewLine,
                result.Message,
                $"Update command: {updateCommand}");
        }

        return result.ToolName.Equals("yt-dlp", StringComparison.OrdinalIgnoreCase)
            ? string.Join(
                Environment.NewLine,
                "yt-dlp was not found in PATH.",
                $"Suggested install command: {YtDlpInstallCommand}",
                $"Update command after install: {YtDlpUpdateCommand}")
            : string.Join(
                Environment.NewLine,
                "ffmpeg was not found in PATH.",
                $"Suggested search command: {FfmpegSearchCommand}",
                $"Suggested install command: {FfmpegInstallCommand}",
                $"Update command after install: {FfmpegUpdateCommand}");
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
        if (e.PropertyName is nameof(ConversionQueueItem.IsSelected)
            or nameof(ConversionQueueItem.Status)
            or nameof(ConversionQueueItem.SourcePathOrUrl))
        {
            UpdateConvertQueueButtonState();
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
