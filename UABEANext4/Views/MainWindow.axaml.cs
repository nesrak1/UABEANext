using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.ViewModels;

namespace UABEANext4.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        RequestedThemeVariant = ThemeVariant.Dark;
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private async Task Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.GetFiles() is { } files && DataContext is MainViewModel viewModel)
        {
            var fileNames = files.Select(sf => sf.TryGetLocalPath()).Where(p => p != null);
            if (fileNames is not null)
            {
                await viewModel.OpenFiles(fileNames);
            }
        }
    }
}
