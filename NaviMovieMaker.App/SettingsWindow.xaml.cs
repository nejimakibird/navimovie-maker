using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using NaviMovieMaker.App.Services;

namespace NaviMovieMaker.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ObservableCollection<ConversionPreset> _hiddenPresets = [];
    private readonly ObservableCollection<ConversionPreset> _visiblePresets = [];

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        Settings = settings.Clone();

        WorkingFolderTextBox.Text = Settings.WorkingFolder;
        TemporaryFolderTextBox.Text = Settings.TemporaryFolder;
        ConvertedFolderTextBox.Text = Settings.ConvertedFolder;
        LocalVideoFolderTextBox.Text = Settings.LocalVideoFolder;
        CreatePresetSubfolderCheckBox.IsChecked = Settings.CreateSubfolderPerOutputPreset;
        StartupLayoutComboBox.SelectedValue = Settings.StartupLayout;
        YtDlpPathTextBox.Text = Settings.YtDlpPath;
        FfmpegPathTextBox.Text = Settings.FfmpegPath;
        FfprobePathTextBox.Text = Settings.FfprobePath;
        MpvPathTextBox.Text = Settings.MpvPath;
        YtDlpDownloadUrlTextBox.Text = string.IsNullOrWhiteSpace(Settings.YtDlpDownloadUrl)
            ? ExternalToolService.DefaultYtDlpDownloadUrl
            : Settings.YtDlpDownloadUrl;
        FfmpegDownloadUrlTextBox.Text = string.IsNullOrWhiteSpace(Settings.FfmpegDownloadUrl)
            ? ExternalToolService.DefaultFfmpegDownloadUrl
            : Settings.FfmpegDownloadUrl;
        DownloadProfileComboBox.ItemsSource = DownloadProfileCatalog.GetProfiles();
        DownloadProfileComboBox.SelectedValue = DownloadProfileCatalog.GetProfile(Settings.DownloadProfile).Id;

        HiddenPresetsListBox.ItemsSource = _hiddenPresets;
        VisiblePresetsListBox.ItemsSource = _visiblePresets;
        LoadPresetLists();
    }

    public AppSettings Settings { get; private set; }

    private void BrowseWorkingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(WorkingFolderTextBox, "作業フォルダを選択");
    }

    private void BrowseTemporaryFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(TemporaryFolderTextBox, "一時フォルダを選択");
    }

    private void BrowseConvertedFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(ConvertedFolderTextBox, "変換済みフォルダを選択");
    }

    private void BrowseLocalVideoFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(LocalVideoFolderTextBox, "ローカル動画フォルダを選択");
    }

    private void BrowseYtDlpPathButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForExecutable(YtDlpPathTextBox, "yt-dlp.exe を選択", "yt-dlp.exe");
    }

    private void BrowseFfmpegPathButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForExecutable(FfmpegPathTextBox, "ffmpeg.exe を選択", "ffmpeg.exe");
    }

    private void BrowseFfprobePathButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForExecutable(FfprobePathTextBox, "ffprobe.exe を選択", "ffprobe.exe");
    }

    private void BrowseMpvPathButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForExecutable(MpvPathTextBox, "mpv.exe を選択", "mpv.exe");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            WorkingFolder = WorkingFolderTextBox.Text.Trim(),
            TemporaryFolder = TemporaryFolderTextBox.Text.Trim(),
            ConvertedFolder = ConvertedFolderTextBox.Text.Trim(),
            LocalVideoFolder = LocalVideoFolderTextBox.Text.Trim(),
            CreateSubfolderPerOutputPreset = CreatePresetSubfolderCheckBox.IsChecked == true,
            DownloadProfile = DownloadProfileComboBox.SelectedValue?.ToString() ?? DownloadProfileCatalog.AutoId,
            RunMode = Settings.RunMode,
            OutputPresetId = Settings.OutputPresetId,
            AspectMode = Settings.AspectMode,
            KeepOriginalDownloadedFiles = Settings.KeepOriginalDownloadedFiles,
            PeakBoost = Settings.PeakBoost,
            SimpleModeEnabled = Settings.SimpleModeEnabled,
            TargetPeakDb = Settings.TargetPeakDb,
            StartupLayout = StartupLayoutComboBox.SelectedValue?.ToString() ?? "QueueFocus",
            LastCandidatesExpanded = Settings.LastCandidatesExpanded,
            LastLogExpanded = Settings.LastLogExpanded,
            LastWindowWidth = Settings.LastWindowWidth,
            LastWindowHeight = Settings.LastWindowHeight,
            LastVideoListRowHeight = Settings.LastVideoListRowHeight,
            LastQueueRowHeight = Settings.LastQueueRowHeight,
            LastLogRowHeight = Settings.LastLogRowHeight,
            YtDlpPath = YtDlpPathTextBox.Text.Trim(),
            FfmpegPath = FfmpegPathTextBox.Text.Trim(),
            FfprobePath = FfprobePathTextBox.Text.Trim(),
            MpvPath = MpvPathTextBox.Text.Trim(),
            YtDlpDownloadUrl = YtDlpDownloadUrlTextBox.Text.Trim(),
            FfmpegDownloadUrl = FfmpegDownloadUrlTextBox.Text.Trim(),
            VisibleOutputPresetIds = _visiblePresets.Select(static preset => preset.Id).ToList(),
            KnownOutputPresetIds = ConversionPresetCatalog.GetPresets().Select(static preset => preset.Id).ToList(),
        };

        if (settings.VisibleOutputPresetIds.Count > 0
            && !settings.VisibleOutputPresetIds.Contains(settings.OutputPresetId, StringComparer.OrdinalIgnoreCase))
        {
            settings.OutputPresetId = settings.VisibleOutputPresetIds[0];
        }

        var validationError = Validate(settings);
        if (validationError is not null)
        {
            MessageBox.Show(this, validationError, "設定エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _settingsService.EnsureFolders(settings);
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"設定を保存できないか、フォルダを作成できませんでした。{ex.Message}",
                "設定保存エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        Settings = settings;
        DialogResult = true;
    }

    private void BrowseForFolder(System.Windows.Controls.TextBox textBox, string title)
    {
        var initialDirectory = Directory.Exists(textBox.Text)
            ? textBox.Text
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory,
        };

        if (dialog.ShowDialog(this) == true)
        {
            textBox.Text = dialog.FolderName;
        }
    }

    private void BrowseForExecutable(System.Windows.Controls.TextBox textBox, string title, string fileName)
    {
        var initialDirectory = File.Exists(textBox.Text)
            ? Path.GetDirectoryName(textBox.Text)
            : AppContext.BaseDirectory;

        var dialog = new OpenFileDialog
        {
            Title = title,
            InitialDirectory = initialDirectory,
            Filter = $"{fileName}|{fileName}|実行ファイル|*.exe|すべてのファイル|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void ShowPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPresets = HiddenPresetsListBox.SelectedItems
            .OfType<ConversionPreset>()
            .ToList();

        foreach (var preset in selectedPresets)
        {
            _hiddenPresets.Remove(preset);
            _visiblePresets.Add(preset);
        }
    }

    private void HidePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPresets = VisiblePresetsListBox.SelectedItems
            .OfType<ConversionPreset>()
            .ToList();

        foreach (var preset in selectedPresets)
        {
            _visiblePresets.Remove(preset);
            _hiddenPresets.Add(preset);
        }
    }

    private void MoveVisiblePresetUpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveVisiblePresets(-1);
    }

    private void MoveVisiblePresetDownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveVisiblePresets(1);
    }

    private void LoadPresetLists()
    {
        var allPresets = ConversionPresetCatalog.GetPresets();
        var presetById = allPresets.ToDictionary(static preset => preset.Id, StringComparer.OrdinalIgnoreCase);
        var visibleIds = Settings.VisibleOutputPresetIds
            .Where(presetById.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibleIds.Count == 0)
        {
            visibleIds = ConversionPresetCatalog.GetDefaultVisiblePresetIds().ToList();
        }

        foreach (var presetId in visibleIds)
        {
            if (presetById.TryGetValue(presetId, out var preset))
            {
                _visiblePresets.Add(preset);
            }
        }

        var visibleIdSet = _visiblePresets
            .Select(static preset => preset.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in allPresets.Where(preset => !visibleIdSet.Contains(preset.Id)))
        {
            _hiddenPresets.Add(preset);
        }
    }

    private void MoveVisiblePresets(int direction)
    {
        var selectedPresets = VisiblePresetsListBox.SelectedItems
            .OfType<ConversionPreset>()
            .ToList();
        if (selectedPresets.Count == 0)
        {
            return;
        }

        var orderedPresets = direction < 0
            ? selectedPresets.OrderBy(preset => _visiblePresets.IndexOf(preset)).ToList()
            : selectedPresets.OrderByDescending(preset => _visiblePresets.IndexOf(preset)).ToList();

        foreach (var preset in orderedPresets)
        {
            var index = _visiblePresets.IndexOf(preset);
            var targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= _visiblePresets.Count)
            {
                continue;
            }

            _visiblePresets.Move(index, targetIndex);
        }

        VisiblePresetsListBox.SelectedItems.Clear();
        foreach (var preset in selectedPresets)
        {
            VisiblePresetsListBox.SelectedItems.Add(preset);
        }
    }

    private static string? Validate(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.WorkingFolder))
        {
            return "作業フォルダは必須です。";
        }

        if (string.IsNullOrWhiteSpace(settings.TemporaryFolder))
        {
            return "一時フォルダは必須です。";
        }

        if (string.IsNullOrWhiteSpace(settings.ConvertedFolder))
        {
            return "変換済みフォルダは必須です。";
        }

        if (string.IsNullOrWhiteSpace(settings.LocalVideoFolder))
        {
            return "ローカル動画フォルダは必須です。";
        }

        if (settings.VisibleOutputPresetIds.Count == 0)
        {
            return "少なくとも1つの出力プリセットを表示してください。";
        }

        if (!IsExistingExecutableOrEmpty(settings.YtDlpPath, "yt-dlp.exe"))
        {
            return "yt-dlp.exe のパスが正しくありません。";
        }

        if (!IsExistingExecutableOrEmpty(settings.FfmpegPath, "ffmpeg.exe"))
        {
            return "ffmpeg.exe のパスが正しくありません。";
        }

        if (!IsExistingExecutableOrEmpty(settings.FfprobePath, "ffprobe.exe"))
        {
            return "ffprobe.exe のパスが正しくありません。";
        }

        if (!IsExistingExecutableOrEmpty(settings.MpvPath, "mpv.exe"))
        {
            return "mpv.exe のパスが正しくありません。";
        }

        return null;
    }

    private static bool IsExistingExecutableOrEmpty(string path, string expectedFileName)
    {
        return string.IsNullOrWhiteSpace(path)
            || File.Exists(path)
                && string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase);
    }
}
