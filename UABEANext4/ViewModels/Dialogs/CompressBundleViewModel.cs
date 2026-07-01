using AssetsTools.NET;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using UABEANext4.Interfaces;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;

public partial class CompressBundleViewModel : ViewModelBase, IDialogAware<CompressBundleResult?>
{
    [ObservableProperty]
    private string _outputPath;

    [ObservableProperty]
    private CompressionChoice? _selectedCompressionChoice;

    public List<CompressionChoice> CompressionChoices { get; } =
    [
        new CompressionChoice("LZ4", "Faster write speed, larger output file", AssetBundleCompressionType.LZ4),
        new CompressionChoice("LZMA", "Slower write speed, smaller output file", AssetBundleCompressionType.LZMA)
    ];

    public string Title => "Compress Bundle";
    public int Width => 580;
    public int Height => 160;
    public bool IsModal => true;

    public event Action<CompressBundleResult?>? RequestClose;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public CompressBundleViewModel()
    {
        OutputPath = string.Empty;
        SelectedCompressionChoice = CompressionChoices[0];
    }

    public CompressBundleViewModel(string sourcePath)
    {
        OutputPath = GetSuggestedOutputPath(sourcePath);
        SelectedCompressionChoice = CompressionChoices[0];
    }

    public async void BrowseOutputPath()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider == null)
        {
            return;
        }

        IStorageFile? result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Select compressed bundle output file",
            FileTypeChoices =
            [
                new FilePickerFileType("All files (*.*)") { Patterns = [ "*" ] }
            ],
            SuggestedFileName = Path.GetFileName(OutputPath)
        });

        string? filePath = FileDialogUtils.GetSaveFileDialogFile(result);
        if (filePath != null)
        {
            OutputPath = filePath;
        }
    }

    public void BtnCompress_Click()
    {
        var compressionType = SelectedCompressionChoice?.CompressionType ?? AssetBundleCompressionType.LZ4;
        RequestClose?.Invoke(new CompressBundleResult(OutputPath.Trim(), compressionType));
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }

    private static string GetSuggestedOutputPath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        string dirName = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string fileExt = Path.GetExtension(sourcePath);

        return Path.Combine(dirName, $"{fileName}.compressed{fileExt}");
    }
}

public sealed class CompressionChoice
{
    public string Name { get; }
    public string Description { get; }
    public AssetBundleCompressionType CompressionType { get; }

    public CompressionChoice(string name, string description, AssetBundleCompressionType compressionType)
    {
        Name = name;
        Description = description;
        CompressionType = compressionType;
    }
}

public sealed class CompressBundleResult
{
    public string OutputPath { get; }
    public AssetBundleCompressionType CompressionType { get; }

    public CompressBundleResult(string outputPath, AssetBundleCompressionType compressionType)
    {
        OutputPath = outputPath;
        CompressionType = compressionType;
    }
}
