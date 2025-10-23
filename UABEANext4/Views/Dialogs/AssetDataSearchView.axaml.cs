using Avalonia.Controls;

namespace UABEANext4.Views.Dialogs;
public partial class AssetDataSearchView : UserControl
{
    public AssetDataSearchView()
    {
        InitializeComponent();
        Loaded += AssetDataSearchView_Loaded;
    }

    private void AssetDataSearchView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        defaultBox.Focus();
        defaultBox.SelectAll();
    }
}
