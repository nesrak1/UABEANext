using Avalonia.Controls;
using UABEANext3.ViewModels.Tools;

namespace UABEANext3.Views.Tools
{
    public partial class SceneExplorerToolView : UserControl
    {
        public SceneExplorerToolView()
        {
            InitializeComponent();
        }

        private void GameObjectTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SceneExplorerToolViewModel scExpVm)
            {
                var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
                if (selectedItem is SceneExplorerItem explorerItem)
                {
                    scExpVm.InvokeSelectedSceneItemChanged(explorerItem.Asset);
                }
            }
        }
    }
}
