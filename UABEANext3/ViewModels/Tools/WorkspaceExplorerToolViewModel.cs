using Dock.Model.ReactiveUI.Controls;
using System.Collections.Generic;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.ViewModels.Tools
{
    public class WorkspaceExplorerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Workspace Explorer";

        public delegate void SelectedWorkspaceItemChangedEvent(List<WorkspaceItem> workspaceItems);
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

        public void InvokeSelectedWorkspaceItemChanged(List<WorkspaceItem> wsItems)
        {
            SelectedWorkspaceItemChanged?.Invoke(wsItems);
        }
    }
}
