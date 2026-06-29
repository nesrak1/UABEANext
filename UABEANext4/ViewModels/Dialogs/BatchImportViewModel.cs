using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Logic.Configuration;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;

public partial class BatchImportViewModel : ViewModelBase, IDialogAware<List<ImportBatchInfo>?>
{
    private string _directory;

    private bool _ignoreListEvents;

    public List<ImportBatchDataGridItem> DataGridItems { get; set; }

    [ObservableProperty]
    public List<string> _matchingFilesItems;
    [ObservableProperty]
    public object? _dataGridSelectedItem;
    [ObservableProperty]
    public int _matchingFilesSelectedIndex;

    public string Title => "Batch Import";
    public int Width => 700;
    public int Height => 350;
    public bool IsModal => false;

    public event Action<List<ImportBatchInfo>?>? RequestClose;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public BatchImportViewModel()
    {
        _directory = string.Empty;

        DataGridItems = new List<ImportBatchDataGridItem>();
        MatchingFilesItems = new List<string>();
    }

    public BatchImportViewModel(Workspace workspace, List<AssetInst> selection, string directory,
        List<string> extensions)
    {
        _directory = directory;

        var anyExtension = extensions.Contains("*");

        List<string> filesInDir;
        if (!anyExtension)
            filesInDir = FileUtils.GetFilesInDirectory(directory, extensions);
        else
            filesInDir = Directory.GetFiles(directory).ToList();

        // Build a suffix index for O(1) lookup of EndsWith matches.
        //
        // The original code did filesInDir.Where(f => f.EndsWith(matchName)) per asset,
        // which is O(F) per asset — prohibitive for 100k+ files.
        //
        // Key insight: the match name (when !importJustNames) always starts with '-':
        //   GetMatchName(ext) = "-{File}-{PathId}.{ext}"
        //
        // For each file name, we generate all suffixes starting from each '-'
        // position and add them to a dictionary. A match name starting with '-'
        // will match one of these suffixes, giving O(1) lookup.
        //
        // Example: "Texture2D-CAB-1234-5678.json" generates suffixes:
        //   "Texture2D-CAB-1234-5678.json" (full name, for exact match)
        //   "-CAB-1234-5678.json" (from '-' at position 9)
        //   "-1234-5678.json" (from '-' at position 13)
        //   "-5678.json" (from '-' at position 22)
        //
        // Match name "-CAB-1234-5678.json" finds the file via the second suffix.
        // This preserves the original EndsWith semantics exactly, including for
        // files with asset-name prefixes.
        var suffixIndex = new Dictionary<string, List<string>>(filesInDir.Count * 4, StringComparer.Ordinal);
        var allFileNames = new List<string>(filesInDir.Count);
        foreach (var f in filesInDir)
        {
            var fileName = Path.GetFileName(f);
            allFileNames.Add(fileName);

            // Add full file name (for exact match and importJustNames case)
            if (!suffixIndex.TryGetValue(fileName, out var bucket))
                suffixIndex[fileName] = bucket = new List<string>();
            bucket.Add(fileName);

            // Add suffixes starting from each '-' position (skip if same as full name)
            int start = 0;
            while (start < fileName.Length)
            {
                int dashPos = fileName.IndexOf('-', start);
                if (dashPos < 0) break;
                var suffix = fileName.Substring(dashPos);
                if (suffix != fileName)
                {
                    if (!suffixIndex.TryGetValue(suffix, out var bucket2))
                        suffixIndex[suffix] = bucket2 = new List<string>();
                    bucket2.Add(fileName);
                }
                start = dashPos + 1;
            }
        }

        List<ImportBatchDataGridItem> gridItems = new();
        int maxNameLen = ConfigurationManager.Settings.ExportNameLength;
        bool importJustNames = ConfigurationManager.Settings.ExportImportJustNames;

        foreach (var asset in selection)
        {
            var assetName = workspace.Namer.GetAssetName(asset, true, maxNameLen);
            assetName = AssetNamer.GetFallbackName(asset, assetName);

            var gridItem = new ImportBatchDataGridItem(
                new ImportBatchInfo(
                    asset, Path.GetFileName(asset.FileInstance.path), assetName, asset.PathId)
            );

            List<string> matchingFiles;
            if (!importJustNames && !anyExtension)
            {
                // Fast path: suffix index lookup for match names starting with '-'
                matchingFiles = new List<string>();
                foreach (var ext in extensions)
                {
                    if (suffixIndex.TryGetValue(gridItem.GetMatchName(ext), out var matches))
                        matchingFiles.AddRange(matches);
                }
            }
            else if (!importJustNames && anyExtension)
            {
                // anyExtension: match name has no extension. Brute force on stems.
                var matchStem = gridItem.GetMatchName("*");
                matchingFiles = allFileNames
                    .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith(matchStem))
                    .ToList();
            }
            else if (importJustNames && !anyExtension)
            {
                // importJustNames: match name is "AssetName.ext", doesn't start with '-'.
                // Try exact match first, then brute force for prefixed files.
                matchingFiles = new List<string>();
                foreach (var ext in extensions)
                {
                    var matchName = gridItem.Description + "." + ext;
                    if (suffixIndex.TryGetValue(matchName, out var matches))
                        matchingFiles.AddRange(matches);
                }
                if (matchingFiles.Count == 0)
                {
                    foreach (var ext in extensions)
                    {
                        var matchName = gridItem.Description + "." + ext;
                        foreach (var fn in allFileNames)
                        {
                            if (fn.EndsWith(matchName))
                                matchingFiles.Add(fn);
                        }
                    }
                }
            }
            else // importJustNames && anyExtension
            {
                matchingFiles = allFileNames
                    .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith(gridItem.Description))
                    .ToList();
            }

            gridItem.MatchingFiles = matchingFiles;
            gridItem.SelectedIndex = matchingFiles.Count > 0 ? 0 : -1;
            if (gridItem.MatchingFiles.Count > 0)
                gridItems.Add(gridItem);
        }

        DataGridItems = gridItems;
        MatchingFilesItems = new List<string>();
    }

    partial void OnDataGridSelectedItemChanged(object? value)
    {
        if (value is ImportBatchDataGridItem gridItem)
        {
            MatchingFilesItems = gridItem.MatchingFiles;
            if (gridItem.SelectedIndex != -1)
            {
                //there's gotta be a better way to do this .-. oh well
                _ignoreListEvents = true;
                MatchingFilesSelectedIndex = gridItem.SelectedIndex;
                _ignoreListEvents = false;
            }
        }
    }

    partial void OnMatchingFilesSelectedIndexChanged(int value)
    {
        if (DataGridSelectedItem is ImportBatchDataGridItem gridItem && !_ignoreListEvents)
        {
            gridItem.SelectedIndex = value;
        }
    }

    public void BtnOk_Click()
    {
        List<ImportBatchInfo> importInfos = new List<ImportBatchInfo>();
        foreach (ImportBatchDataGridItem gridItem in DataGridItems)
        {
            if (gridItem.SelectedIndex != -1)
            {
                ImportBatchInfo importInfo = gridItem.importInfo;
                importInfo.ImportFile = Path.Combine(_directory, gridItem.MatchingFiles[gridItem.SelectedIndex]);
                importInfos.Add(importInfo);
            }
        }

        RequestClose?.Invoke(importInfos);
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}

public class ImportBatchInfo
{
    public readonly AssetInst Asset;
    public readonly string AssetFile;
    public readonly string AssetName;
    public readonly long PathId;
    public string? ImportFile;

    public ImportBatchInfo(AssetInst asset, string assetFile, string assetName, long pathId)
    {
        Asset = asset;
        AssetFile = assetFile;
        AssetName = assetName;
        PathId = pathId;
    }
}

public class ImportBatchDataGridItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ImportBatchInfo importInfo;

    public List<string> MatchingFiles = new();
    public int SelectedIndex;

    public string Description => importInfo.AssetName;
    public string File => importInfo.AssetFile;
    public long PathId => importInfo.PathId;

    public ImportBatchDataGridItem(ImportBatchInfo importInfo)
    {
        this.importInfo = importInfo;
    }

    public string GetMatchName(string ext)
    {
        if (ext != "*")
            return $"-{File}-{PathId}.{ext}";

        return $"-{File}-{PathId}";
    }

    public void Update(string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}