using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.ViewModels.Tools
{
    public class InspectorToolViewModel : Tool
    {
        const string TOOL_TITLE = "Inspector";

        public ServiceContainer Container { get; }
        public Workspace Workspace { get; }

        [Reactive]
        public ObservableCollection<AssetInst> ActiveAssets { get; set; }

        public ICommand EditAssetRequestedCommand { get; }
        public ICommand VisitAssetRequestedCommand { get; }
        public Interaction<EditDataViewModel, byte[]?> ShowEditData { get; }

        // preview only
        public InspectorToolViewModel()
        {
            Container = new();
            Workspace = new();
            ActiveAssets = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            EditAssetRequestedCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditAssetRequested);
            VisitAssetRequestedCommand = ReactiveCommand.CreateFromTask<AssetInst>(VisitAssetRequested);
        }

        public InspectorToolViewModel(ServiceContainer container, Workspace workspace)
        {
            Container = container;
            Workspace = workspace;
            ActiveAssets = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            EditAssetRequestedCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditAssetRequested);
            VisitAssetRequestedCommand = ReactiveCommand.CreateFromTask<AssetInst>(VisitAssetRequested);
        }

        public async Task EditAssetRequested(AssetInst asset)
        {
            var baseField = Workspace.GetBaseField(asset);
            if (baseField == null)
            {
                return;
            }

            var data = await ShowEditData.Handle(new EditDataViewModel(baseField));
            if (data == null)
            {
                return;
            }

            asset.UpdateAssetDataAndRow(Workspace, data);

            var workspaceItem = Workspace.ItemLookup[asset.FileInstance.name];
            Workspace.Dirty(workspaceItem);
        }

        public async Task VisitAssetRequested(AssetInst asset)
        {

        }
    }
}
