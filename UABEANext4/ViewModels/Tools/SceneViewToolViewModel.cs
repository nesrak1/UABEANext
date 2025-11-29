using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Scene;

namespace UABEANext4.ViewModels.Tools;

public partial class SceneViewToolViewModel : Tool
{
    const string TOOL_TITLE = "Scene View";

    public Workspace Workspace { get; }

    [ObservableProperty]
    private SceneData? _sceneData;

    [ObservableProperty]
    private SceneObject? _selectedObject;

    [ObservableProperty]
    private string _statusText = "No scene loaded. Select an assets file to load the scene.";

    [ObservableProperty]
    private bool _isSceneLoaded = false;

    private List<AssetsFileInstance>? _currentFileInsts;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public SceneViewToolViewModel()
    {
        Workspace = new();
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public SceneViewToolViewModel(Workspace workspace)
    {
        Workspace = workspace;
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, OnWorkspaceItemSelected);
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
    }

    private void OnWorkspaceItemSelected(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        var items = message.Value;
        if (items.Count == 0) return;

        var fileInsts = new List<AssetsFileInstance>();
        foreach (var item in items)
        {
            if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Object is AssetsFileInstance fileInst)
            {
                fileInsts.Add(fileInst);
            }
        }

        if (fileInsts.Count > 0)
        {
            _currentFileInsts = fileInsts;
        }
    }

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        SceneData = null;
        SelectedObject = null;
        IsSceneLoaded = false;
        StatusText = "No scene loaded. Select an assets file to load the scene.";
        _currentFileInsts = null;
    }

    [RelayCommand]
    private void LoadScene()
    {
        if (_currentFileInsts == null || _currentFileInsts.Count == 0)
        {
            StatusText = "No assets file selected. Please select an assets file in the Workspace Explorer first.";
            return;
        }

        StatusText = "Loading scene...";

        var sceneData = new SceneData(Workspace);

        // Load from first file for now
        var fileInst = _currentFileInsts[0];
        sceneData.LoadFromFile(fileInst);

        SceneData = sceneData;
        IsSceneLoaded = true;

        var objectCount = sceneData.AllObjects.Count;
        var meshCount = sceneData.AllObjects.Count(o => o.HasMesh);
        var texturedCount = sceneData.AllObjects.Count(o => o.HasTexture);

        StatusText = $"Loaded {objectCount} objects ({meshCount} with meshes, {texturedCount} with textures)";
    }

    [RelayCommand]
    private void ResetCamera()
    {
        // This will be handled by the view through binding or direct call
        StatusText = "Camera reset to default position.";
    }

    public void OnObjectSelected(SceneObject? obj)
    {
        SelectedObject = obj;

        if (obj != null)
        {
            StatusText = $"Selected: {obj.Name} (PathId: {obj.PathId})";

            // Send message to select corresponding asset in inspector
            if (obj.GameObjectAsset != null)
            {
                WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage([obj.GameObjectAsset]));
            }
        }
        else
        {
            StatusText = IsSceneLoaded
                ? "No object selected. Click on an object to select it."
                : "No scene loaded.";
        }
    }
}
