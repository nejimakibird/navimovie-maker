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

        HiddenPresetsListBox.ItemsSource = _hiddenPresets;
        VisiblePresetsListBox.ItemsSource = _visiblePresets;
        LoadPresetLists();
    }

    public AppSettings Settings { get; private set; }

    private void BrowseWorkingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(WorkingFolderTextBox, "Select working folder");
    }

    private void BrowseTemporaryFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(TemporaryFolderTextBox, "Select temporary folder");
    }

    private void BrowseConvertedFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(ConvertedFolderTextBox, "Select converted output folder");
    }

    private void BrowseLocalVideoFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(LocalVideoFolderTextBox, "Select local video folder");
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
            VisibleOutputPresetIds = _visiblePresets.Select(static preset => preset.Id).ToList(),
        };

        var validationError = Validate(settings);
        if (validationError is not null)
        {
            MessageBox.Show(this, validationError, "Invalid Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                $"Settings could not be saved or folders could not be created. {ex.Message}",
                "Save Settings Failed",
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
            return "Working Folder is required.";
        }

        if (string.IsNullOrWhiteSpace(settings.TemporaryFolder))
        {
            return "Temporary Folder is required.";
        }

        if (string.IsNullOrWhiteSpace(settings.ConvertedFolder))
        {
            return "Converted Folder is required.";
        }

        if (string.IsNullOrWhiteSpace(settings.LocalVideoFolder))
        {
            return "Local Video Folder is required.";
        }

        return null;
    }
}
