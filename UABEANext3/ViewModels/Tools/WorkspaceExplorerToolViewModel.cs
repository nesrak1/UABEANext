using AssetsTools.NET.Extra;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.ViewModels.Tools
{
    public class WorkspaceExplorerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Workspace Explorer";

        public delegate void SelectedWorkspaceItemChangedEvent(WorkspaceItem workspaceItem);
        public event SelectedWorkspaceItemChangedEvent? SelectedWorkspaceItemChanged;

        public Workspace Workspace { get; }

        // preview only
        public WorkspaceExplorerToolViewModel()
        {
            Workspace = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public WorkspaceExplorerToolViewModel(Workspace workspace)
        {
            Workspace = workspace;

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public void InvokeSelectedWorkspaceItemChanged(WorkspaceItem wsItem)
        {
            SelectedWorkspaceItemChanged?.Invoke(wsItem);
        }
    }
}
