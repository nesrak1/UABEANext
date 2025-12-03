using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.ViewModels;
#if DEBUG
using Avalonia.Diagnostics;
using UABEANext4.Logic.DevTools;
#endif

namespace UABEANext4.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
#if DEBUG
        InitializeComponent(attachDevTools: false);
        DevToolsAdblock.Attach(this, new DevToolsOptions());
#else
        InitializeComponent();
#endif
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private async Task Drop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { } files && DataContext is MainViewModel viewModel)
        {
            var fileNames = files.Select(sf => sf.TryGetLocalPath()).Where(p => p != null);
            if (fileNames is not null)
            {
                await viewModel.OpenFiles(fileNames);
            }
        }
    }
}
