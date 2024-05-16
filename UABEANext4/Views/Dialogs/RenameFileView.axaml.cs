using Avalonia.Controls;

namespace UABEANext4.Views.Dialogs;
public partial class RenameFileView : UserControl
{
    public RenameFileView()
    {
        InitializeComponent();
        Loaded += RenameFileView_Loaded;
    }

    private void RenameFileView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        defaultBox.Focus();
        defaultBox.SelectAll();
    }
}
