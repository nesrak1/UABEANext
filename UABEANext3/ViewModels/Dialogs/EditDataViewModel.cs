using AssetsTools.NET;
using AvaloniaEdit.Document;
using ReactiveUI;
using System;
using System.IO;
using System.Text;
using UABEANext3.Logic;
using UABEANext3.Util;

namespace UABEANext3.ViewModels.Dialogs
{
    public class EditDataViewModel : ViewModelBase
    {
        private TextDocument? _document;
        private AssetTypeValueField _baseField;

        public TextDocument? Document
        {
            get => _document;
            set => this.RaiseAndSetIfChanged(ref _document, value);
        }

        public Action<byte[]?>? CloseAction { get; set; }

        [Obsolete("This is a previewer-only constructor")]
        public EditDataViewModel()
        {
        }

        public EditDataViewModel(AssetTypeValueField baseField)
        {
            _baseField = baseField;

            using MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);

            AssetImportExport impexp = new AssetImportExport();
            impexp.DumpJsonAsset(sw, _baseField);

            sw.Flush();
            ms.Position = 0;

            string str = Encoding.UTF8.GetString(ms.ToArray());
            Document = new TextDocument(str);
        }

        public async void BtnOk_Click()
        {
            string text = Document!.Text;
            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
            StreamReader sr = new StreamReader(ms);
            AssetImportExport impexp = new AssetImportExport();
            byte[]? data = impexp.ImportJsonAsset(_baseField.TemplateField, sr, out string? exceptionMessage);
            if (data == null)
            {
                await MessageBoxUtil.ShowDialog("Compile Error", "Problem with import:\n" + exceptionMessage);
                return;
            }

            CloseAction?.Invoke(data);
        }

        public void BtnCancel_Click()
        {
            CloseAction?.Invoke(null);
        }
    }
}
