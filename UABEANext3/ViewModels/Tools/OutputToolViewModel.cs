using Dock.Model.ReactiveUI.Controls;
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

        // preview only
        public OutputToolViewModel()
        {
            Workspace = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public OutputToolViewModel(Workspace workspace)
        {
            Workspace = workspace;

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }
    }
}
