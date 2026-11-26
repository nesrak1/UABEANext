using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.ViewModels;

namespace UABEANext4.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
#if DEBUG
        InitializeComponent(attachDevTools: true);
#else
        InitializeComponent();
#endif
        AddHandler(DragDrop.DropEvent, Drop);

        Opened += async (s, e) => await HandleCommandLineArgs();
    }

    private async Task HandleCommandLineArgs()
    {
        var args = System.Environment.GetCommandLineArgs();

        var filePaths = args.Skip(1)
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Where(arg => File.Exists(arg))
            .ToList();

        if (filePaths.Any() && DataContext is MainViewModel viewModel)
        {
            await viewModel.OpenFiles(filePaths);
        }
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
