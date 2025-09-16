using Dock.Model.Mvvm.Controls;

namespace UABEANext4.ViewModels.Documents;
public partial class BlankDocumentViewModel : Document
{
    const string TOOL_TITLE = "New Tab";

    public BlankDocumentViewModel()
    {
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }
}
