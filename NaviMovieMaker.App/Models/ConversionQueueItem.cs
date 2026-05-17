using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NaviMovieMaker.App;

public sealed class ConversionQueueItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private int _order;
    private string _downloadedFilePath = string.Empty;
    private string _convertedFilePath = string.Empty;
    private string _status = "Pending";
    private AudioAdjustmentMode _audioAdjustmentMode = AudioAdjustmentMode.Off;

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

    public string SourceType { get; init; } = "LocalFile";

    public string Title { get; init; } = string.Empty;

    public string SourcePathOrUrl { get; init; } = string.Empty;

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

    public AudioAdjustmentMode AudioAdjustmentMode
    {
        get => _audioAdjustmentMode;
        set
        {
            if (SetField(ref _audioAdjustmentMode, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AudioAdjustmentDisplay)));
            }
        }
    }

    public string AudioAdjustmentDisplay
    {
        get
        {
            return AudioAdjustmentMode switch
            {
                AudioAdjustmentMode.LoudnessNormalize => "音量ノーマライズ",
                _ => "なし",
            };
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public enum AudioAdjustmentMode
{
    Off,
    LoudnessNormalize,
}
