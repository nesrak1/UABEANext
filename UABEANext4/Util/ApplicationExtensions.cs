using System.Threading.Tasks;

namespace UABEANext4.Util;
public static class ApplicationExtensions
{

    public static async Task CopyToClipboard(string text)
    {
        var mainWindow = WindowUtils.GetMainWindow();
        if (mainWindow?.Clipboard != null)
        {
            await mainWindow.Clipboard.SetTextAsync(text);
        }
    }

    public static string GetIconPath(string iconName)
    {
        return $"avares://UABEANext4/Assets/Icons/{iconName}";
    }
}
