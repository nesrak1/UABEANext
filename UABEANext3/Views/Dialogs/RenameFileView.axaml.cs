using Avalonia.Controls;
using System;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.Views.Dialogs
{
    public partial class RenameFileView : Window
    {
        public RenameFileView()
        {
            InitializeComponent();

            Loaded += RenameFileView_Loaded;
        }

        private void RenameFileView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is RenameFileViewModel rfvm)
            {
                rfvm.CloseAction = new Action<string?>(a => Close(a));
            }
        }
    }
}
