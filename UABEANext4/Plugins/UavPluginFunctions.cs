using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Threading.Tasks;
using UABEANext4.Interfaces;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.Plugins;
public class UavPluginFunctions : IUavPluginFunctions
{
    private readonly IDialogService _dialogService;
    private readonly IStorageProvider _storageProvider;

    public UavPluginFunctions()
    {
        _dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        var storageProvider = StorageService.GetStorageProvider() ??
            throw new InvalidOperationException("The requested service type was not registered.");

        _storageProvider = storageProvider;
    }

    public async Task<string[]> ShowOpenFileDialog(FilePickerOpenOptions options)
    {
        var result = await _storageProvider.OpenFilePickerAsync(options);
        return FileDialogUtils.GetOpenFileDialogFiles(result);
    }

    public async Task<string?> ShowSaveFileDialog(FilePickerSaveOptions options)
    {
        var result = await _storageProvider.SaveFilePickerAsync(options);
        return FileDialogUtils.GetSaveFileDialogFile(result);
    }

    public async Task<string?> ShowOpenFolderDialog(FolderPickerOpenOptions options)
    {
        var result = await _storageProvider.OpenFolderPickerAsync(options);
        var folders = FileDialogUtils.GetOpenFolderDialogFolders(result);
        if (folders.Length != 1)
            return null;

        return folders[0];
    }

    public async Task<T?> ShowDialog<T>(IDialogAware<T> dialogAware)
    {
        return await _dialogService.ShowDialog(dialogAware);
    }

    public async Task ShowMessageDialog(string title, string message)
    {
        var messageBoxVm = new MessageBoxViewModel(title, message, MessageBoxType.OK);
        await _dialogService.ShowDialog(messageBoxVm);
    }
}
