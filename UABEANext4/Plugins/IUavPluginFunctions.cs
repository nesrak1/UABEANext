using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Plugins;
public interface IUavPluginFunctions
{
    public Task<string[]> ShowOpenFileDialog(FilePickerOpenOptions options);
    public Task<string?> ShowSaveFileDialog(FilePickerSaveOptions options);
    public Task<string?> ShowOpenFolderDialog(FolderPickerOpenOptions options);
    public Task<T?> ShowDialog<T>(IDialogAware<T> dialogAware);
    public Task ShowMessageDialog(string title, string message);
}
