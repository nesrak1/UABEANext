using Avalonia.Controls;

namespace UABEANext4.Views.Dialogs;
public partial class VersionSelectView : UserControl
{
    public VersionSelectView()
    {
        InitializeComponent();
        Loaded += VersionSelectView_Loaded;
    }

    private void VersionSelectView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        defaultBox.Focus();
        defaultBox.SelectAll();
    }
}
