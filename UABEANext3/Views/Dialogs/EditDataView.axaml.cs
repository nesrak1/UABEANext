using AssetsTools.NET;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Microsoft.Win32;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Text;
using TextMateSharp.Grammars;
using UABEANext3.Logic;
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
