using Avalonia.Collections;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI.Fody.Helpers;
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
        [Reactive]
        public AvaloniaList<object> SelectedItems { get; set; }

        // preview only
        public WorkspaceExplorerToolViewModel()
        {
            Workspace = new();
            SelectedItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public WorkspaceExplorerToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            SelectedItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public void InvokeSelectedWorkspaceItemChanged(List<WorkspaceItem> wsItems)
        {
            SelectedWorkspaceItemChanged?.Invoke(wsItems);
        }
    }
}
