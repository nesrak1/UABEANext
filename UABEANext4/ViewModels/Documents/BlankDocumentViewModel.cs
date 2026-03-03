using Dock.Model.Mvvm.Controls;
using UABEANext4.Assets.Localization;

namespace UABEANext4.ViewModels.Documents;
public partial class BlankDocumentViewModel : Document
{
    private static readonly string TOOL_TITLE = Localization.New_Tab;

    public BlankDocumentViewModel()
    {
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }
}
