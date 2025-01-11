using AssetsTools.NET;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using UABEANext4.Interfaces;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AddExternalViewModel : ViewModelBaseValidator, IDialogAware<AssetsFileExternal>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOriginalName))]
    private string _fileName = "";
    [ObservableProperty]
    private string _originalFileName = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuid))]
    private AssetsFileExternalType _externalType = AssetsFileExternalType.Normal;
    [ObservableProperty]
    [CustomValidation(typeof(AddExternalViewModel), nameof(ValidateGuid))]
    private string _guidString = "00000000000000000000000000000000";

    public bool HasOriginalName => FileName.StartsWith("Resources/");
    public bool HasGuid => ExternalType != AssetsFileExternalType.Normal;

    public string Title => "Edit External";
    public int Width => 350;
    public int Height => 170;
    public event Action<AssetsFileExternal?>? RequestClose;

    public AddExternalViewModel(AssetsFileExternal? external)
    {
        if (external != null)
        {
            FileName = external.PathName;
            OriginalFileName = external.OriginalPathName;
            ExternalType = external.Type;
            GuidString = external.Guid.ToString();
        }
    }

    public static ValidationResult? ValidateGuid(string guidString, ValidationContext context)
    {
        if (!GUID128.TryParse(guidString, out GUID128 _))
        {
            return new("GUID is invalid");
        }

        return ValidationResult.Success;
    }

    public async void BtnOk_Click()
    {
        GUID128 guid;
        if (HasGuid)
        {
            var guidSuccess = GUID128.TryParse(GuidString, out guid);
            if (!guidSuccess)
            {
                await ShowInvalidOptionsBox();
                return;
            }
        }
        else
        {
            guid = new GUID128();
        }

        var result = new AssetsFileExternal()
        {
            PathName = FileName,
            OriginalPathName = HasOriginalName ? OriginalFileName : FileName,
            Type = ExternalType,
            Guid = guid
        };
        RequestClose?.Invoke(result);
    }

    private async Task ShowInvalidOptionsBox()
    {
        await MessageBoxUtil.ShowDialog("Error", "Invalid options provided.");
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}