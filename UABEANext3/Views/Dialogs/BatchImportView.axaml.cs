using Avalonia.Controls;
using System;
using System.Collections.Generic;
using UABEANext3.ViewModels.Dialogs;
using static UABEANext3.ViewModels.Dialogs.BatchImportViewModel;

namespace UABEANext3.Views.Dialogs
{
    public partial class BatchImportView : Window
    {
        public BatchImportView()
        {
            InitializeComponent();

            Loaded += BatchImportView_Loaded;
        }

        private void BatchImportView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is BatchImportViewModel bivm)
            {
                bivm.CloseAction = new Action<List<ImportBatchInfo>?>(a => Close(a));
            }
        }
    }
}
