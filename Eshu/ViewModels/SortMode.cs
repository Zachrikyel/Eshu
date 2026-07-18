namespace Eshu.ViewModels
{
    // El orden de estos valores tiene que coincidir con el orden de los
    // ComboBoxItem en MainWindow.xaml — el code-behind los cruza por índice.
    public enum SortMode
    {
        Alphabetical,
        InstallDate,
        AcquisitionDate,
        HoursPlayed,
        LastPlayed
    }
}
