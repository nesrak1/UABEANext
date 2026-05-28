using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
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
        var isLightTheme = (PlatformThemeVariant?)ActualThemeVariant == PlatformThemeVariant.Light;
        var registryOptions = new RegistryOptions(isLightTheme ? ThemeName.LightPlus : ThemeName.DarkPlus);
        var textMateInstallation = textEditor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".json").Id));
    }
}
