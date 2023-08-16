using Avalonia.Collections;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Reactive.Linq;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.ViewModels.Tools
{
    public class WorkspaceExplorerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Workspace Explorer";

        public delegate void SelectedWorkspaceItemChangedEvent(List<WorkspaceItem> workspaceItems);
        public event SelectedWorkspaceItemChangedEvent? SelectedWorkspaceItemChanged;

        public ServiceContainer Container { get; }
        public Workspace Workspace { get; }

        [Reactive]
        public AvaloniaList<object> SelectedItems { get; set; }

        public Interaction<RenameFileViewModel, string?> ShowRenameFile { get; }

        // preview only
        public WorkspaceExplorerToolViewModel()
        {
            Container = new();
            Workspace = new();
            SelectedItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;

            ShowRenameFile = new Interaction<RenameFileViewModel, string?>();
        }

        public WorkspaceExplorerToolViewModel(ServiceContainer sc, Workspace workspace)
        {
            Container = sc;
            Workspace = workspace;
            SelectedItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;

            ShowRenameFile = new Interaction<RenameFileViewModel, string?>();
        }

        public void InvokeSelectedWorkspaceItemChanged(List<WorkspaceItem> wsItems)
        {
            SelectedWorkspaceItemChanged?.Invoke(wsItems);
        }

        public async void InvokeSelectedWorkspaceItemRename(WorkspaceItem wsItem)
        {
            var newName = await ShowRenameFile.Handle(new RenameFileViewModel(wsItem.Name));
            if (newName == null)
            {
                return;
            }

            wsItem.Name = newName;
            wsItem.Update(nameof(wsItem.Name));
            Workspace.Dirty(wsItem);
        }
    }
}
