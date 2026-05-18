using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NaviMovieMaker.App;

public sealed class ConversionQueueItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private int _order;
    private string _title = string.Empty;
    private string _downloadedFilePath = string.Empty;
    private string _convertedFilePath = string.Empty;
    private string _status = "Pending";
    private string _unsupportedReason = string.Empty;
    private double? _progressPercent;
    private string _progressText = string.Empty;
    private string _detailText = string.Empty;
    private string _speedText = string.Empty;
    private string _etaText = string.Empty;
    private bool _isIndeterminate;
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

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

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
        set
        {
            if (SetField(ref _status, value))
            {
                NotifyStatusDisplayChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnsupported)));
            }
        }
    }

    public string UnsupportedReason
    {
        get => _unsupportedReason;
        set
        {
            if (SetField(ref _unsupportedReason, value))
            {
                NotifyStatusDisplayChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnsupported)));
            }
        }
    }

    public bool IsUnsupported => !string.IsNullOrWhiteSpace(UnsupportedReason);

    public double? ProgressPercent
    {
        get => _progressPercent;
        set
        {
            if (SetField(ref _progressPercent, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (SetField(ref _progressText, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public string DetailText
    {
        get => _detailText;
        set
        {
            if (SetField(ref _detailText, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public string SpeedText
    {
        get => _speedText;
        set
        {
            if (SetField(ref _speedText, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public string EtaText
    {
        get => _etaText;
        set
        {
            if (SetField(ref _etaText, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (SetField(ref _isIndeterminate, value))
            {
                NotifyStatusDisplayChanged();
            }
        }
    }

    public string StatusDisplay
    {
        get
        {
            var status = IsUnsupported ? $"⚠ {Status}" : Status;
            var parts = new[] { ProgressText, DetailText, SpeedText, EtaText }
                .Where(static part => !string.IsNullOrWhiteSpace(part));
            var progress = string.Join("  ", parts);
            return string.IsNullOrWhiteSpace(progress) ? status : $"{status}  {progress}";
        }
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

    private void NotifyStatusDisplayChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusDisplay)));
    }
}

public enum AudioAdjustmentMode
{
    Off,
    LoudnessNormalize,
}
