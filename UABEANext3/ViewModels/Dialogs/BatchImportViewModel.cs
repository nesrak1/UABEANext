using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UABEANext3.AssetWorkspace;
using UABEANext3.Util;

namespace UABEANext3.ViewModels.Dialogs
{
    public class BatchImportViewModel : ViewModelBase
    {
        private string _directory;
        private Workspace _workspace;

        private bool _ignoreListEvents;

        public List<ImportBatchDataGridItem> DataGridItems { get; set; }

        [Reactive]
        public List<string> MatchingFilesItems { get; set; }
        [Reactive]
        public object? DataGridSelectedItem { get; set; }
        [Reactive]
        public int MatchingFilesSelectedIndex { get; set; }

        public Action<List<ImportBatchInfo>?>? CloseAction { get; set; }

        [Obsolete("This is a previewer-only constructor")]
        public BatchImportViewModel()
        {
            _workspace = new Workspace();
            _directory = string.Empty;

            DataGridItems = new List<ImportBatchDataGridItem>();
            MatchingFilesItems = new List<string>();
        }

        public BatchImportViewModel(Workspace workspace, List<AssetInst> selection, string directory,
            List<string> extensions)
        {
            _workspace = workspace;
            _directory = directory;

            this.WhenAnyValue(x => x.DataGridSelectedItem)
                .Subscribe(DataGrid_SelectionChanged);

            this.WhenAnyValue(x => x.MatchingFilesSelectedIndex)
                .Subscribe(MatchingFiles_SelectedIndexChanged);

            var anyExtension = extensions.Contains("*");

            List<string> filesInDir;
            if (!anyExtension)
                filesInDir = FileUtils.GetFilesInDirectory(directory, extensions);
            else
                filesInDir = Directory.GetFiles(directory).ToList();

            List<ImportBatchDataGridItem> gridItems = new();
            foreach (var asset in selection)
            {
                AssetNameUtils.GetDisplayNameFast(workspace, asset, true, out var assetName, out var _);

                var gridItem = new ImportBatchDataGridItem
                {
                    importInfo = new ImportBatchInfo(
                        asset, Path.GetFileName(asset.FileInstance.path), assetName, asset.PathId)
                };

                List<string> matchingFiles;

                if (!anyExtension)
                {
                    matchingFiles = filesInDir
                        .Where(f => extensions.Any(x => f.EndsWith(gridItem.GetMatchName(x))))
                        .Select(f => Path.GetFileName(f)!).ToList();
                }
                else
                {
                    matchingFiles = filesInDir
                        .Where(f => PathUtils.GetFilePathWithoutExtension(f).EndsWith(gridItem.GetMatchName("*")))
                        .Select(f => Path.GetFileName(f)!).ToList();
                }

                gridItem.MatchingFiles = matchingFiles;
                gridItem.SelectedIndex = matchingFiles.Count > 0 ? 0 : -1;
                if (gridItem.MatchingFiles.Count > 0)
                    gridItems.Add(gridItem);
            }

            DataGridItems = gridItems;
            MatchingFilesItems = new List<string>();
        }

        private void DataGrid_SelectionChanged(object? selectedItem)
        {
            if (selectedItem is ImportBatchDataGridItem gridItem)
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

        private void MatchingFiles_SelectedIndexChanged(int index)
        {
            if (DataGridSelectedItem is ImportBatchDataGridItem gridItem && !_ignoreListEvents)
                gridItem.SelectedIndex = index;
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

            CloseAction?.Invoke(importInfos);
        }

        public void BtnCancel_Click()
        {
            CloseAction?.Invoke(null);
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
    }
}
