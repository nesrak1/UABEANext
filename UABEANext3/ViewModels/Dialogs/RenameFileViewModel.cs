using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;

namespace UABEANext3.ViewModels.Dialogs
{
    public class RenameFileViewModel : ViewModelBase
    {
        [Reactive]
        public string NewName { get; set; }

        public Action<string?>? CloseAction { get; set; }

        public RenameFileViewModel(string originalName)
        {
            NewName = originalName;
        }

        public void BtnOk_Click()
        {
            CloseAction?.Invoke(NewName);
        }

        public void BtnCancel_Click()
        {
            CloseAction?.Invoke(null);
        }
    }
}
