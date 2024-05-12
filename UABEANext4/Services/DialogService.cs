using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Services;
public class DialogService(Window mainWindow, ViewLocator viewLocator) : IDialogService
{
    public async Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel)
    {
        var view = viewLocator.Build(viewModel);
        view.DataContext = viewModel;

        if (view is not UserControl uc)
        {
            throw new Exception("View is not a UserControl");
        }

        var window = new Window
        {
            Content = uc,
            Icon = mainWindow.Icon,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = viewModel.Title,
            Width = viewModel.Width,
            Height = viewModel.Height,
        };

        void eventHandler(TResult? result) => window.Close(result);

        viewModel.RequestClose += eventHandler;
        var result = await window.ShowDialog<TResult?>(mainWindow);
        viewModel.RequestClose -= eventHandler;

        return result;
    }
}
