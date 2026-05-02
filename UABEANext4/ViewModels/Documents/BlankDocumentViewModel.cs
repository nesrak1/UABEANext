using Dock.Model.Mvvm.Controls;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Documents;
public partial class BlankDocumentViewModel : Document
{
    const string TOOL_TITLE = "New Tab";

    public BlankDocumentViewModel()
    {
        Id = TOOL_TITLE.Replace(" ", "");
        Title = LocalizationHelper.GetString("Common.NewTab", TOOL_TITLE);
    }
}
