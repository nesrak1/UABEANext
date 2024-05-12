using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace UABEANext4.Views.Dialogs;
public partial class EditDataView : UserControl
{
    public EditDataView()
    {
        InitializeComponent();

        Loaded += EditDataView_Loaded;
    }

    private void EditDataView_Loaded(object? sender, RoutedEventArgs e)
    {
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMateInstallation = textEditor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".json").Id));
    }
}
