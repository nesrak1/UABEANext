using CommunityToolkit.Mvvm.ComponentModel;
using System;
using UABEANext4.Interfaces;

namespace UABEANext4.ViewModels.Dialogs;
public partial class VersionSelectViewModel : ViewModelBase, IDialogAware<string?>
{
    [ObservableProperty]
    public string _version = "0.0.0f0";

    public string Title => "Version Select";
    public int Width => 300;
    public int Height => 140;
    public event Action<string?>? RequestClose;

    public void BtnOk_Click()
    {
        RequestClose?.Invoke(Version);
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}
