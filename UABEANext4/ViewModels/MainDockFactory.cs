using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;
using UABEANext4.ViewModels.Documents;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.ViewModels;
internal class MainDockFactory : Factory
{
    private IRootDock? _rootDock;
    private IDocumentDock? _fileDocumentDock;
    private WorkspaceExplorerToolViewModel? _workspaceExplorerTool;
    private InspectorToolViewModel? _inspectorToolViewModel;
    private PreviewerToolViewModel? _previewerToolViewModel;
    private HierarchyToolViewModel? _hierarchyToolViewModel;

    private Workspace _workspace;

    public MainDockFactory()
    {
        _workspace = new();
    }

    public MainDockFactory(Workspace workspace)
    {
        _workspace = workspace;
    }

    public override IRootDock CreateLayout()
    {
        _workspaceExplorerTool = new WorkspaceExplorerToolViewModel(_workspace);
        _inspectorToolViewModel = new InspectorToolViewModel(_workspace);
        _previewerToolViewModel = new PreviewerToolViewModel(_workspace);
        _hierarchyToolViewModel = new HierarchyToolViewModel(_workspace);

        var assetDocumentDock = new AssetDocumentViewModel(_workspace);
        var documentDock = _fileDocumentDock = new DocumentDock
        {
            ActiveDockable = assetDocumentDock,
            VisibleDockables = CreateList<IDockable>
            (
                assetDocumentDock
            ),
            CanCreateDocument = true,
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
                _previewerToolViewModel
            ),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.25
        };

        var topRightPane = new ProportionalDock
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
        windowLayout.VisibleDockables = CreateList<IDockable>(topRightPane);
        windowLayout.ActiveDockable = topRightPane;

        _rootDock = CreateRootDock();
        _rootDock.IsCollapsable = false;
        _rootDock.VisibleDockables = CreateList<IDockable>(windowLayout);
        _rootDock.ActiveDockable = windowLayout;
        _rootDock.DefaultDockable = windowLayout;

        return _rootDock;
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
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}
