using AssetsTools.NET;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UABEANext4.Interfaces;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;
public partial class EditDataViewModel : ViewModelBase, IDialogAware<byte[]?>
{
    [ObservableProperty]
    private TextDocument? _document;
    private AssetTypeValueField _baseField;
    private RefTypeManager _refMan;

    public string Title => "Edit Data";
    public int Width => 350;
    public int Height => 550;
    public event Action<byte[]?>? RequestClose;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public EditDataViewModel()
    {
        _document = new TextDocument();
        _baseField = new AssetTypeValueField();
        _refMan = new RefTypeManager();
    }

    public EditDataViewModel(AssetTypeValueField baseField, RefTypeManager refMan)
    {
        _baseField = baseField;
        _refMan = refMan;

        using var ms = new MemoryStream();
        var exporter = new AssetExport(ms);
        exporter.DumpJsonAsset(_baseField);

        ms.Position = 0;

        var str = Encoding.UTF8.GetString(ms.ToArray());
        Document = new TextDocument(str);
    }

    public async Task BtnOk_Click()
    {
        var text = Document!.Text;
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var importer = new AssetImport(ms, _refMan);
        var data = importer.ImportJsonAsset(_baseField.TemplateField, out string? exceptionMessage);
        if (data == null)
        {
            await MessageBoxUtil.ShowDialog("Compile Error", "Problem with import:\n" + exceptionMessage);
            return;
        }

        RequestClose?.Invoke(data);
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}
