using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.Views.Dialogs
{
    public partial class AssetInfoView : Window
    {
        public AssetInfoView()
        {
            InitializeComponent();

            Loaded += AssetInfoView_Loaded;
        }

        private void AssetInfoView_Loaded(
            object? sender,
            Avalonia.Interactivity.RoutedEventArgs e
        ) 
        {  
            if (DataContext is AssetInfoViewModel aivm)
            {
                this.SelectedAssetComboBox.ItemsSource = aivm.Items;
                this.SelectedAssetComboBox.SelectedIndex = 0;
                aivm.SelectedItem = aivm.Items.FirstOrDefault();
            }
        }
    }
}
