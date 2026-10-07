using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.ViewModels.Documents;

namespace UABEANext4.Views.Documents;

public partial class ReferencesDocumentView : UserControl
{
    public ReferencesDocumentView()
    {
        InitializeComponent();

        Initialized += ReferencesDocumentView_Initialized;
    }

    private void ReferencesDocumentView_Initialized(object? sender, System.EventArgs e)
    {
        if (DataContext is ReferencesDocumentViewModel docVm)
        {
            docVm.SetSelectedItemsAction += SetSelectedItems;
        }
    }

    // necessary since SelectedItems isn't bindable
    private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReferencesDocumentViewModel docVm)
        {
            var selectedResults = dataGrid.SelectedItems.Cast<ReferenceSearchResult>().ToList();
            docVm.OnAssetOpened(selectedResults.Select(r => r.Asset).ToList());
        }
    }

    // probably not great for many items but fine for a few
    private void SetSelectedItems(List<AssetInst> assets)
    {
        if (DataContext is AssetDocumentViewModel docVm)
        {
            dataGrid.SelectedItems.Clear();
            foreach (var asset in assets)
            {
                if (docVm.Items.Contains(asset))
                    dataGrid.SelectedItems.Add(asset);
            }

            if (assets.Count > 0)
            {
                dataGrid.ScrollIntoView(assets[0], null);
            }
        }
    }

}