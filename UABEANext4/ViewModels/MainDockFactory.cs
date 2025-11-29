using CommunityToolkit.Mvvm.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Documents;
using UABEANext4.ViewModels.Documents;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.ViewModels;
internal class MainDockFactory : Factory
{
    public ProportionalDock? MainPane;
    public DocumentManager DocMan;

    private IRootDock? _rootDock;
    private IDocumentDock? _fileDocumentDock;
    private WorkspaceExplorerToolViewModel? _workspaceExplorerTool;
    private InspectorToolViewModel? _inspectorToolViewModel;
    private PreviewerToolViewModel? _previewerToolViewModel;
    private HierarchyToolViewModel? _hierarchyToolViewModel;
    private SceneViewToolViewModel? _sceneViewToolViewModel;

    private Workspace _workspace;

    public MainDockFactory()
    {
        _workspace = new();
        DocMan = new();
    }

    public MainDockFactory(Workspace workspace)
    {
        _workspace = workspace;
        DocMan = new();
    }

    public override IRootDock CreateLayout()
    {
        _workspaceExplorerTool = new WorkspaceExplorerToolViewModel(_workspace);
        _inspectorToolViewModel = new InspectorToolViewModel(_workspace);
        _previewerToolViewModel = new PreviewerToolViewModel(_workspace);
        _hierarchyToolViewModel = new HierarchyToolViewModel(_workspace);
        _sceneViewToolViewModel = new SceneViewToolViewModel(_workspace);

        var assetDocumentDock = new BlankDocumentViewModel();
        var documentDock = _fileDocumentDock = new DocumentDock
        {
            ActiveDockable = assetDocumentDock,
            VisibleDockables = CreateList<IDockable>
            (
                assetDocumentDock
            ),
            CanCreateDocument = true,
            CreateDocument = new RelayCommand(AddNewBlankDocument),
            IsCollapsable = false,
            Proportion = double.NaN
        };

        var explorerDock = new ToolDock
        {
            ActiveDockable = _workspaceExplorerTool,
            VisibleDockables = CreateList<IDockable>
            (
                _workspaceExplorerTool,
                _hierarchyToolViewModel
            ),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.25
        };

        var inspectorDock = new ToolDock
        {
            ActiveDockable = _inspectorToolViewModel,
            VisibleDockables = CreateList<IDockable>
            (
                _inspectorToolViewModel,
                _previewerToolViewModel,
                _sceneViewToolViewModel
            ),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.25
        };

        MainPane = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>
            (
                explorerDock,
                new ProportionalDockSplitter(),
                documentDock,
                new ProportionalDockSplitter(),
                inspectorDock
            )
        };

        var windowLayout = CreateRootDock();
        windowLayout.Title = "Default";
        windowLayout.IsCollapsable = false;
        windowLayout.VisibleDockables = CreateList<IDockable>(MainPane);
        windowLayout.ActiveDockable = MainPane;

        _rootDock = CreateRootDock();
        _rootDock.IsCollapsable = false;
        _rootDock.VisibleDockables = CreateList<IDockable>(windowLayout);
        _rootDock.ActiveDockable = windowLayout;
        _rootDock.DefaultDockable = windowLayout;

        return _rootDock;
    }

    private void AddNewBlankDocument()
    {
        if (_fileDocumentDock is not null)
        {
            var newDoc = new BlankDocumentViewModel();
            AddDockable(_fileDocumentDock, newDoc);
            SetActiveDockable(newDoc);

            DocMan.Documents.Add(newDoc);
            DocMan.LastFocusedDocument = newDoc;
        }
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["WorkspaceExplorer"] = () => _workspaceExplorerTool,
            ["Hierarchy"] = () => _hierarchyToolViewModel,
            ["Files"] = () => _fileDocumentDock,
            ["Inspector"] = () => _inspectorToolViewModel,
            ["Previewer"] = () => _previewerToolViewModel,
            ["SceneView"] = () => _sceneViewToolViewModel,
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}
