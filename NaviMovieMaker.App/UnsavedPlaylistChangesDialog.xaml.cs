using System.Windows;

namespace NaviMovieMaker.App;

public partial class UnsavedPlaylistChangesDialog : Window
{
    public UnsavedPlaylistChoice Choice { get; private set; } = UnsavedPlaylistChoice.Cancel;

    public UnsavedPlaylistChangesDialog()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedPlaylistChoice.Save;
        DialogResult = true;
    }

    private void DontSaveButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedPlaylistChoice.DontSave;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedPlaylistChoice.Cancel;
        DialogResult = false;
    }
}

public enum UnsavedPlaylistChoice
{
    Save,
    DontSave,
    Cancel,
}
