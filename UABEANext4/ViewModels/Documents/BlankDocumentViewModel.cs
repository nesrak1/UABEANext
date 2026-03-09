using Dock.Model.Mvvm.Controls;
using UABEANext4.Assets.Localization;
using UABEANext4.Services;

namespace UABEANext4.ViewModels.Documents;
public partial class BlankDocumentViewModel : Document
{
    string TOOL_TITLE => Localization.NewTab;

    public BlankDocumentViewModel()
    {
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
        
        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            Title = TOOL_TITLE;
        };
    }
}
