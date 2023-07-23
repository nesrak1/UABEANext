using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;

namespace UABEANext3.ViewModels
{
    public class MainDockFactory : Factory
    {
        private IRootDock? _rootDock;
        private DocumentDock? _fileDocumentDock;
        private WorkspaceExplorerToolViewModel? _workspaceExplorerTool;
        private SceneExplorerToolViewModel? _sceneExplorerTool;
        private ErrorToolViewModel? _errorTool;
        private OutputToolViewModel? _outputTool;
        private InspectorToolViewModel? _inspectorTool;
        private PreviewerToolViewModel? _previewerTool;

        private ServiceContainer _sc;
        private Workspace _workspace;

        // previewer
        public MainDockFactory()
        {
            _sc = new();
            _workspace = new();
        }
        // /////////

        public MainDockFactory(ServiceContainer sc, Workspace workspace)
        {
            _sc = sc;
            _workspace = workspace;
        }

        public override IRootDock CreateLayout()
        {
            _workspaceExplorerTool = new WorkspaceExplorerToolViewModel(_workspace);
            _sceneExplorerTool = new SceneExplorerToolViewModel(_workspace);
            _errorTool = new ErrorToolViewModel(_workspace);
            _outputTool = new OutputToolViewModel(_workspace);
            _inspectorTool = new InspectorToolViewModel(_workspace);
            _previewerTool = new PreviewerToolViewModel(_workspace);

            var helloAssetDocument = new WelcomeDocumentViewModel();
            helloAssetDocument.Title = "Welcome to UABEAvalonia!";

            var explorerDock = new ToolDock
            {
                ActiveDockable = _workspaceExplorerTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _workspaceExplorerTool,
                    _sceneExplorerTool
                ),
                Alignment = Alignment.Left,
                GripMode = GripMode.Visible,
                Proportion = 0.25
            };

            var documentDock = new DocumentDock
            {
                ActiveDockable = helloAssetDocument,
                VisibleDockables = CreateList<IDockable>
                (
                    helloAssetDocument
                ),
                CanCreateDocument = true,
                Proportion = double.NaN
            };

            _fileDocumentDock = documentDock;


            /*
            var inspectorDock = new ToolDock
            {
                ActiveDockable = _inspectorTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _inspectorTool
                ),
                Alignment = Alignment.Top,
                GripMode = GripMode.Visible,
                Proportion = 0.7
            };

            var previewDock = new ToolDock
            {
                ActiveDockable = _previewerTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _previewerTool
                ),
                Alignment = Alignment.Bottom,
                GripMode = GripMode.Visible,
                Proportion = 0.3
            };
            var topRightRightPane = new ProportionalDock
            {
                Orientation = Orientation.Vertical,
                VisibleDockables = CreateList<IDockable>
                (
                    inspectorDock,
                    new ProportionalDockSplitter(),
                    previewDock
                ),
                Proportion = 0.3
            };
            */

            var topRightRightPane = new ToolDock
            {
                ActiveDockable = _inspectorTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _inspectorTool,
                    _previewerTool
                ),
                GripMode = GripMode.Visible,
                Proportion = 0.3
            };

            var topRightPane = new ProportionalDock
            {
                Orientation = Orientation.Horizontal,
                IsCollapsable = false,
                VisibleDockables = CreateList<IDockable>
                (
                    documentDock,
                    new ProportionalDockSplitter(),
                    topRightRightPane
                )
            };

            var outputAndErrorDock = new ToolDock
            {
                ActiveDockable = _outputTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _outputTool,
                    _errorTool
                ),
                Alignment = Alignment.Bottom,
                GripMode = GripMode.Visible,
                Proportion = 0.3
            };

            var rightPane = new ProportionalDock
            {
                Orientation = Orientation.Vertical,
                IsCollapsable = false,
                VisibleDockables = CreateList<IDockable>
                (
                    topRightPane,
                    new ProportionalDockSplitter(),
                    outputAndErrorDock
                )
            };

            var windowLayoutContent = new ProportionalDock
            {
                Orientation = Orientation.Horizontal,
                IsCollapsable = false,
                VisibleDockables = CreateList<IDockable>
                (
                    explorerDock,
                    new ProportionalDockSplitter(),
                    rightPane
                )
            };

            var windowLayout = CreateRootDock();
            windowLayout.Title = "Default";
            windowLayout.IsCollapsable = false;
            windowLayout.VisibleDockables = CreateList<IDockable>(windowLayoutContent);
            windowLayout.ActiveDockable = windowLayoutContent;

            _rootDock = CreateRootDock();
            _rootDock.IsCollapsable = false;
            _rootDock.VisibleDockables = CreateList<IDockable>(windowLayout);
            _rootDock.ActiveDockable = windowLayout;
            _rootDock.DefaultDockable = windowLayout;

            /*
            new ToolDock
            {
                ActiveDockable = _workspaceExplorerTool,
                VisibleDockables = CreateList<IDockable>
                (
                    _workspaceExplorerTool
                ),
                Alignment = Alignment.Right,
                GripMode = GripMode.Visible
            }
            */

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
                ["Inspector"] = () => _inspectorTool,
                ["Previewer"] = () => _previewerTool,
                ["WorkspaceExplorer"] = () => _workspaceExplorerTool,
                ["SceneExplorer"] = () => _sceneExplorerTool,
                ["Files"] = () => _fileDocumentDock,
                ["Output"] = () => _outputTool,
            };

            HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
            {
                [nameof(IDockWindow)] = () => new HostWindow()
            };

            base.InitLayout(layout);
        }
    }
}
