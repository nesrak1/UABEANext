using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using UABEANext3.Views;

namespace UABEANext3.ViewModels.Tools
{
    internal class InspectorToolViewModel : Tool
    {
        const string TOOL_TITLE = "Inspector";

        private Workspace _workspace;
        public Workspace Workspace
        {
            get => _workspace;
            set => this.RaiseAndSetIfChanged(ref _workspace, value);
        }

        private AvaloniaList<AssetInst>? _activeAssets;
        public AvaloniaList<AssetInst>? ActiveAssets
        {
            get => _activeAssets;
            set => this.RaiseAndSetIfChanged(ref _activeAssets, value);
        }

        // preview only
        public InspectorToolViewModel()
        {
            Workspace = new();
            ActiveAssets = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public InspectorToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            ActiveAssets = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }
    }
}
