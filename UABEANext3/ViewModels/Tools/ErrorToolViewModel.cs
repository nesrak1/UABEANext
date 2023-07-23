using Dock.Model.ReactiveUI.Controls;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.ViewModels.Tools
{
    internal class ErrorToolViewModel : Tool
    {
        const string TOOL_TITLE = "Errors";

        Workspace Workspace { get; }

        // preview only
        public ErrorToolViewModel()
        {
            Workspace = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public ErrorToolViewModel(Workspace workspace)
        {
            Workspace = workspace;

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }
    }
}
