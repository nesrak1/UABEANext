using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace UABEANext3.Views
{
    public partial class MainWindow : Window
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
