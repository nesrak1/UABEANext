using AssetsTools.NET.Extra;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Hierarchy;

namespace UABEANext4.ViewModels.Tools;
public partial class HierarchyToolViewModel : Tool
{
    const string TOOL_TITLE = "Hierarchy";

    public Workspace Workspace { get; }

    [ObservableProperty]
    public ObservableCollection<AssetInst> _activeAssets = new();
    [ObservableProperty]
    public ObservableCollection<HierarchyItem> _rootItems = new();
    [ObservableProperty]
    public HierarchyItem? _selectedItem = null;
    [ObservableProperty]
    public bool _sortAlphabetically = false;
    [ObservableProperty]
    private bool _isLoadingNewItems = false;

    private List<WorkspaceItem>? _itemsToLoad = null;
    private readonly List<HierarchyItem> _pendingRootItems = new();
    private DispatcherTimer? _loadingNewItemsTimer = null;
    private CancellationTokenSource? _loadingNewItemsCts = null;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public HierarchyToolViewModel()
    {
        Workspace = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public HierarchyToolViewModel(Workspace workspace)
    {
        Workspace = workspace;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        WeakReferenceMessenger.Default.Register<AssetsSelectedMessage>(this, OnAssetsSelected);
        WeakReferenceMessenger.Default.Register<AssetsUpdatedMessage>(this, OnAssetsUpdated);
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
        WeakReferenceMessenger.Default.Register<RequestSceneViewMessage>(this, OnRequestSceneView);
        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, (r, h) => _ = OnSelectedWorkspaceItemsChanged(r, h));
    }

    private void OnAssetsSelected(object recipient, AssetsSelectedMessage message)
    {
        ActiveAssets.Clear();
        if (message.Value.Count > 0)
        {
            ActiveAssets.Add(message.Value[0]);
        }
    }

    private void OnAssetsUpdated(object recipient, AssetsUpdatedMessage message)
    {
        var asset = message.Value;
        var index = ActiveAssets.IndexOf(asset);
        if (index != -1)
        {
            ActiveAssets[index] = asset;
        }
    }

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        _loadingNewItemsCts?.Cancel();
        _itemsToLoad = null;
        IsLoadingNewItems = false;
        ActiveAssets.Clear();
        RootItems.Clear();
        _pendingRootItems.Clear();
    }

    private void OnRequestSceneView(object recipient, RequestSceneViewMessage message)
    {
        SelectItem(message.Value);
    }

    private async Task OnSelectedWorkspaceItemsChanged(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        _itemsToLoad = message.Value;
        await LoadRootItems();
    }

    partial void OnSortAlphabeticallyChanged(bool value)
    {
        _ = LoadRootItems();
    }

    private async Task LoadRootItems()
    {
        if (_itemsToLoad == null)
        {
            return;
        }

        if (IsLoadingNewItems)
        {
            _loadingNewItemsTimer?.Stop();
            if (_loadingNewItemsCts != null)
            {
                await _loadingNewItemsCts.CancelAsync();
            }
        }

        _loadingNewItemsTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (s, e) => AddPendingRootItems()
        );

        List<WorkspaceItem> items = _itemsToLoad;
        bool sortAlphabetically = SortAlphabetically;

        IsLoadingNewItems = true;
        RootItems.Clear();
        _loadingNewItemsCts = new CancellationTokenSource();
        _pendingRootItems.Clear();
        _loadingNewItemsTimer.Start();

        await Task.Run(() =>
        {
            foreach (var file in items)
            {
                _loadingNewItemsCts.Token.ThrowIfCancellationRequested();

                if (file.ObjectType != WorkspaceItemType.AssetsFile || file.Object is not AssetsFileInstance fileInst)
                    continue;

                var itemObjs = HierarchyItem.CreateRootItems(Workspace, fileInst, sortAlphabetically);
                var item = new HierarchyItem()
                {
                    Asset = null,
                    Name = fileInst.name
                };
                item.Children.AddRange(itemObjs);

                lock (_pendingRootItems)
                {
                    _pendingRootItems.Add(item);
                }
            }
        }, _loadingNewItemsCts.Token);

        // don't try to load the rest of the items if this method
        // is being called again for a different list of items.
        if (!_loadingNewItemsCts.IsCancellationRequested)
        {
            AddPendingRootItems();
            _loadingNewItemsTimer.Stop();
            IsLoadingNewItems = false;
        }
    }

    private void AddPendingRootItems()
    {
        lock (_pendingRootItems)
        {
            RootItems.AddRange(_pendingRootItems);
            _pendingRootItems.Clear();
        }
    }

    private void SelectItem(AssetInst asset)
    {
        // slow, but do we really want to make a large lookup
        // every time we load the tree? (depends how much this
        // feature will be used)
        var filteredRootItems = RootItems.Where(i => i.Name == asset.FileName);
        foreach (var item in filteredRootItems)
        {
            if (SearchForItemAndSelectRecursive(asset, item))
            {
                item.Expanded = true;
                break;
            }
        }
    }

    private bool SearchForItemAndSelectRecursive(AssetInst asset, HierarchyItem item)
    {
        if (item.Asset == asset)
        {
            SelectedItem = item;
            return true;
        }

        foreach (HierarchyItem child in item.Children)
        {
            if (SearchForItemAndSelectRecursive(asset, child))
            {
                item.Expanded = true;
                return true;
            }
        }

        return false;
    }

    public void SelectedItemsChanged(AssetInst? asset)
    {
        if (asset == null)
            return;

        var gameObjectBf = Workspace.GetBaseField(asset);
        if (gameObjectBf == null)
            return;

        var allAssets = new List<AssetInst>
        {
            asset
        };

        var componentPairs = gameObjectBf["m_Component.Array"];
        foreach (var componentPair in componentPairs)
        {
            var component = Workspace.GetAssetInst(asset.FileInstance, componentPair["component"]);
            if (component != null)
            {
                allAssets.Add(component);
            }
            else
            {
                // notify user when we fail?
            }
        }

        WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage(allAssets));
    }
}
