using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Services;
public class DialogService(Window mainWindow, ViewLocator viewLocator) : IDialogService
{
    public async Task ShowDialog(IDialogAware viewModel)
    {
        var window = CreateWindow(viewModel);

        await window.ShowDialog(mainWindow);
    }
    
    public async Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel)
    {
        var window = CreateWindow(viewModel);

        void eventHandler(TResult? result) => window.Close(result);

        viewModel.RequestClose += eventHandler;
        var result = await window.ShowDialog<TResult?>(mainWindow);
        viewModel.RequestClose -= eventHandler;

        return result;
    }

    private Window CreateWindow(IDialogAware viewModel)
    {
        var view = viewLocator.Build(viewModel);
        view.DataContext = viewModel;

        if (view is not UserControl uc)
        {
            throw new Exception("View is not a UserControl");
        }

        return new Window
        {
            Content = uc,
            Icon = mainWindow.Icon,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = viewModel.Title,
            Width = viewModel.Width,
            Height = viewModel.Height,
        };
    }
}
