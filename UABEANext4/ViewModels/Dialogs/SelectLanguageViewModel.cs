using UABEANext4.Assets.Localization;
using UABEANext4.Interfaces;

namespace UABEANext4.ViewModels.Dialogs;

public partial class SelectLanguageViewModel : ViewModelBase, IDialogAware
{
    public string Title => Localization.Select_Language;
    public int Width => 350;
    public int Height => 550;
    public bool IsModal => true;

    public SelectLanguageViewModel() { }
}