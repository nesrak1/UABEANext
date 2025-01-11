using Avalonia.Controls;
using System.Linq;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.Views.Dialogs;

public partial class AssetInfoView : UserControl
{
    public AssetInfoView()
    {
        InitializeComponent();
        Loaded += AssetInfoView_Loaded;
    }

    private void AssetInfoView_Loaded(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetInfoViewModel aivm)
        {
            SelectedAssetComboBox.ItemsSource = aivm.Items;
            SelectedAssetComboBox.SelectedIndex = 0;
            aivm.SelectedItem = aivm.Items.FirstOrDefault();


        }
    }
}
