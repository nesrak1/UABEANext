using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using UABEANext3.ViewModels;

namespace UABEANext3.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
}
