using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using UABEANext3.ViewModels;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.Views.Dialogs;

namespace UABEANext3.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            //this.AttachDevTools();
#endif
            RequestedThemeVariant = ThemeVariant.Dark;

            AddHandler(DragDrop.DropEvent, Drop);
            this.WhenActivated(action => 
                action(ViewModel!.ShowAssetInfo.RegisterHandler(DoShowAssetInfoAsync)));
        }

        private async Task Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.GetFiles() is { } files && ViewModel is not null)
            {
                var fileNames = files.Select(sf => sf.TryGetLocalPath()).Where(p => p != null);
                await ViewModel.OpenFiles(fileNames!);
            }
        }
        
        private async Task DoShowAssetInfoAsync(InteractionContext<AssetInfoViewModel,
            AssetInfoViewModel?> interaction)
        {
            var dialog = new AssetInfoView
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<AssetInfoViewModel?>(this);
            interaction.SetOutput(result);
        }
    }
}
