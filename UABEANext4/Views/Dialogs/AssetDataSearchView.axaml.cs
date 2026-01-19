using Avalonia.Controls;
using UABEANext4.Logic.Search;
using UABEANext4.ViewModels.Dialogs;

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

    private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is SearchResultItem item)
        {
            if (DataContext is AssetDataSearchViewModel vm)
            {
                vm.VisitAssetCommand.Execute(item);
            }
        }
    }
}
