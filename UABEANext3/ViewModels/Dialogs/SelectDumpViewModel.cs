using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;

namespace UABEANext3.ViewModels.Dialogs
{
    public class SelectDumpViewModel : ViewModelBase
    {
        [Reactive]
        public SelectedDumpType SelectedItem { get; set; }
        public List<string> DropdownItems { get; }

        public Action<SelectedDumpType?>? CloseAction { get; set; }

        public SelectDumpViewModel(bool hideAnyOption)
        {
            SelectedItem = SelectedDumpType.JsonDump;

            DropdownItems = new List<string>()
            {
                "UABEA json dump",
                "UABE text dump",
                "Raw dump",
                "Any"
            };

            if (hideAnyOption)
            {
                DropdownItems.RemoveAt(DropdownItems.Count - 1);
            }
        }

        public void BtnOk_Click()
        {
            CloseAction?.Invoke(SelectedItem);
        }

        public void BtnCancel_Click()
        {
            CloseAction?.Invoke(null);
        }
    }

    public enum SelectedDumpType
    {
        JsonDump,
        TxtDump,
        RawDump,
        Any
    }
}
