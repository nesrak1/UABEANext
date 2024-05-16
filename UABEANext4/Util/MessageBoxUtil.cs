using CommunityToolkit.Mvvm.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.Services;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.Util;
public class MessageBoxUtil
{
    public static async Task<MessageBoxResult> ShowDialog(string header, string message)
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var messageBoxVm = new MessageBoxViewModel(header, message, MessageBoxType.OK);
        return await dialogService.ShowDialog(messageBoxVm) ?? MessageBoxResult.Unknown;
    }

    public static async Task<MessageBoxResult> ShowDialog(string header, string message, MessageBoxType buttons)
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var messageBoxVm = new MessageBoxViewModel(header, message, buttons);
        return await dialogService.ShowDialog(messageBoxVm) ?? MessageBoxResult.Unknown;
    }

    public static async Task<string> ShowDialogCustom(string header, string message, params string[] buttons)
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var messageBoxVm = new MessageBoxViewModel(header, message, MessageBoxType.Custom, buttons.ToList());
        var res = await dialogService.ShowDialog(messageBoxVm) ?? MessageBoxResult.Unknown;
        return res switch
        {
            MessageBoxResult.CustomButtonA => buttons[0],
            MessageBoxResult.CustomButtonB => buttons[1],
            MessageBoxResult.CustomButtonC => buttons[2],
            _ => string.Empty,
        };
    }
}
