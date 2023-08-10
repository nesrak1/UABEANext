using Avalonia.Controls;
using System;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.Views.Dialogs
{
    public partial class SelectDumpView : Window
    {
        public SelectDumpView()
        {
            InitializeComponent();

            Loaded += SelectDumpView_Loaded;
        }

        private void SelectDumpView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is SelectDumpViewModel sdvm)
            {
                sdvm.CloseAction = new Action<SelectedDumpType?>(a => Close(a));
            }
        }
    }
}
