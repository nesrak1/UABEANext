using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Services;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.ViewModels.Tools
{
    public partial class WorkspaceExplorerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Workspace Explorer";

        public delegate void SelectedWorkspaceItemChangedEvent(List<WorkspaceItem> workspaceItems);
        public event SelectedWorkspaceItemChangedEvent? SelectedWorkspaceItemChanged;

        public Workspace Workspace { get; }

        [ObservableProperty]
        public AvaloniaList<object> _selectedItems;

        [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
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

        public void SelectedItemsChanged(List<WorkspaceItem> value)
        {
            WeakReferenceMessenger.Default.Send(new SelectedWorkspaceItemChangedMessage(value));
        }

        public async void RenameItem()
        {
            var wsItem = (WorkspaceItem)SelectedItems[0];
            if (!IsItemRenamable(wsItem))
            {
                return;
            }

            var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
            var newName = await dialogService.ShowDialog(new RenameFileViewModel(wsItem.Name));
            if (newName == null)
            {
                return;
            }

            Workspace.RenameFile(wsItem, newName);
        }

        private bool IsItemRenamable(WorkspaceItem wsItem)
        {
            return wsItem.Parent != null && wsItem.Parent.ObjectType == WorkspaceItemType.BundleFile;
        }
    }
}
