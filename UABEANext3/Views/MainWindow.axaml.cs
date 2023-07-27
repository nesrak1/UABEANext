using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using System.Linq;
using System.Threading.Tasks;
using UABEANext3.ViewModels;

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
        }

        private async Task Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.GetFiles() is { } files && ViewModel is not null)
            {
                var fileNames = files.Select(sf => sf.TryGetLocalPath()).Where(p => p != null);
                await ViewModel.OpenFiles(fileNames!);
            }
        }
    }
}
