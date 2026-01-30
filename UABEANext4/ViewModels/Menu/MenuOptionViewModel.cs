using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace UABEANext4.ViewModels.Menu;
public partial class MenuOptionViewModel : ViewModelBase
{
    [ObservableProperty] private string _header;
    [ObservableProperty] private ICommand? _command;
    [ObservableProperty] private object? _commandParameter;
    [ObservableProperty] private ObservableCollection<MenuOptionViewModel>? _items;
    [ObservableProperty] private string? _iconPath;

    public MenuOptionViewModel(string header, ICommand? command = null, object? parameter = null, string? iconPath = null)
    {
        Header = header;
        Command = command;
        CommandParameter = parameter;
        IconPath = iconPath;
    }
}
