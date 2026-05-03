using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace NaviMovieMaker.App;

public sealed class VideoListItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private int _order;
    private string _status = "Pending";
    private string _sourcePath = string.Empty;
    private string _downloadedFilePath = string.Empty;
    private string _convertedFilePath = string.Empty;
    private ImageSource? _thumbnailImage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    public string Title { get; init; } = string.Empty;

    public string VideoId { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string DurationText { get; init; } = string.Empty;

    public string ThumbnailUrl { get; init; } = string.Empty;

    public ImageSource? ThumbnailImage
    {
        get => _thumbnailImage;
        set => SetField(ref _thumbnailImage, value);
    }

    public string SourceType { get; init; } = "YouTube";

    public string SourcePath
    {
        get => _sourcePath;
        set => SetField(ref _sourcePath, value);
    }

    public string DownloadedFilePath
    {
        get => _downloadedFilePath;
        set => SetField(ref _downloadedFilePath, value);
    }

    public string ConvertedFilePath
    {
        get => _convertedFilePath;
        set => SetField(ref _convertedFilePath, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
