using Avalonia.Controls;

namespace UABEANext4.Views.Dialogs;

public partial class CompressBundleView : UserControl
{
    public CompressBundleView()
    {
        InitializeComponent();
        Loaded += CompressBundleView_Loaded;
    }

    private void CompressBundleView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OutputPathBox.Focus();
        OutputPathBox.CaretIndex = OutputPathBox.Text?.Length ?? 0;
    }
}
