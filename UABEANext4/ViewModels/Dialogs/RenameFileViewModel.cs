using CommunityToolkit.Mvvm.ComponentModel;
using System;
using UABEANext4.Interfaces;

namespace UABEANext4.ViewModels.Dialogs;
public partial class RenameFileViewModel : ViewModelBase, IDialogAware<string?>
{
    [ObservableProperty]
    public string _newName;

    public string Title => "Rename File";
    public int Width => 350;
    public int Height => 80;
    public event Action<string?>? RequestClose;

    public RenameFileViewModel(string originalName)
    {
        NewName = originalName;
    }

    public void BtnOk_Click()
    {
        RequestClose?.Invoke(NewName);
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}
