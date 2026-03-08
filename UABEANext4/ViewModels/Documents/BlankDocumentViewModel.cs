using Dock.Model.Mvvm.Controls;
using UABEANext4.Assets.Localization;

namespace UABEANext4.ViewModels.Documents;
public partial class BlankDocumentViewModel : Document
{
    string TOOL_TITLE = Localization.NewTab;

    public BlankDocumentViewModel()
    {
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }
}
