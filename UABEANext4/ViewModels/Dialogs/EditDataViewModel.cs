using AssetsTools.NET;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] 
    private bool _isBusy;

    private readonly AssetTypeValueField _baseField;
    private readonly RefTypeManager _refMan;

    public string Title => "Edit Data";
    public int Width => 700;
    public int Height => 550;
    public bool IsModal => false;

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
        _ = LoadDocumentAsync();
    }

    private async Task LoadDocumentAsync()
    {
        IsBusy = true;
        try
        {
            var jsonText = await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                var exporter = new AssetExport(ms);
                exporter.DumpJsonAsset(_baseField);
                return Encoding.UTF8.GetString(ms.ToArray());
            });

            Document = new TextDocument(jsonText);
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Error", "Failed to export asset: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task BtnOk_Click()
    {
        if (Document == null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var text = Document.Text;

            var (data, error) = await Task.Run(() =>
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
                var importer = new AssetImport(ms, _refMan);
                var result = importer.ImportJsonAsset(_baseField.TemplateField, out string? exceptionMessage);
                return (result, exceptionMessage);
            });

            if (data == null)
            {
                await MessageBoxUtil.ShowDialog("Compile Error", "Problem with import:\n" + error);
                return;
            }

            RequestClose?.Invoke(data);
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Error", "An unexpected error occurred: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}
