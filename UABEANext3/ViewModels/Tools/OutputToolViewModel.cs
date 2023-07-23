using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.ViewModels.Tools
{
    internal class OutputToolViewModel : Tool
    {
        const string TOOL_TITLE = "Output";

        Workspace Workspace { get; }

        private string _displayText;
        public string DisplayText
        {
            get => _displayText;
            set => this.RaiseAndSetIfChanged(ref _displayText, value);
        }

        // preview only
        public OutputToolViewModel()
        {
            Workspace = new();
            DisplayText = "Welcome to UABEA!\n";

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public OutputToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            DisplayText = "Welcome to UABEA!\n";

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }
    }
}
