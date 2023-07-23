using Autofac.Core;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.ComponentModel.Design;
using UABEANext3.Services;
using UABEANext3.ViewModels;
using UABEANext3.Views;

namespace UABEANext3
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;

                var serviceContainer = new ServiceContainer();
                AddServices(serviceContainer, window);

                window.DataContext = new MainWindowViewModel(serviceContainer);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void AddServices(IServiceContainer sc, MainWindow window)
        {
            sc.AddService(typeof(IDialogService), new DialogService(window));
        }
    }
}
