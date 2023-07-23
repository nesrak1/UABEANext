using AssetsTools.NET;
using Avalonia.ReactiveUI;
using AvaloniaEdit.TextMate;
using System;
using TextMateSharp.Grammars;
using UABEANext3.TextHighlighting;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.Views.Dialogs
{
    public partial class EditDataView : ReactiveWindow<EditDataViewModel>
    {
        private AssetTypeValueField _baseField;

        public EditDataView()
        {
            InitializeComponent();

            Loaded += EditDataView_Loaded;
        }

        private void EditDataView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var registryOptions = new UABEDumpRegistryOptions(/*ThemeHandler.UseDarkTheme ? ThemeName.DarkPlus : ThemeName.LightPlus*/ThemeName.DarkPlus);
            var textMateInstallation = textEditor.InstallTextMate(registryOptions);
            textMateInstallation.SetGrammar("source.utxt");

            if (DataContext is EditDataViewModel edvm)
            {
                edvm.CloseAction = new Action<byte[]?>(Close);
                //edvm.Document = textEditor.Document; // bypass crash when binding in xaml for now
            }
        }
    }
}
