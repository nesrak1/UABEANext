using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using UABEANext4.Services;
using UABEANext4.ViewModels;
using UABEANext4.Views;

namespace UABEANext4;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        //BindingPlugins.DataValidators.RemoveAt(0);

        Window? mainWindow = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        var provider = ConfigureServices(mainWindow);
        Ioc.Default.ConfigureServices(provider);

        base.OnFrameworkInitializationCompleted();
    }

    private IServiceProvider ConfigureServices(Window? mainWindow)
    {
        var services = new ServiceCollection();

        var viewLocator = new ViewLocator();
        if (mainWindow != null)
            services.AddSingleton<IDialogService>(new DialogService(mainWindow, viewLocator));
        else
            services.AddSingleton<IDialogService, DummyDialogService>();

        return services.BuildServiceProvider();
    }
}
