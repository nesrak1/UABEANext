using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEANext3.Util;

namespace UABEANext3.AssetWorkspace
{
    public partial class Workspace
    {
        private static async Task<IStorageFile?> ShowSaveAsDialog(IStorageProvider storageProvider, string suggestedFileName)
        {
            return await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save file",
                FileTypeChoices = new FilePickerFileType[]
                {
                    new FilePickerFileType("All files (*.*)") { Patterns = new[] {"*.*"} }
                },
                SuggestedFileName = suggestedFileName
            });
        }

        private void WriteAssetsFile(WorkspaceItem item, Stream stream)
        {
            var fileInst = (AssetsFileInstance)item.Object!;
            fileInst.file.Write(new AssetsFileWriter(stream));
        }

        // warning! OriginalName needs to be updated if save overwrite is used
        private void WriteBundleFile(WorkspaceItem item, Stream stream)
        {
            var bunInst = (BundleFileInstance)item.Object!;

            // files that are both unsaved and part of this bundle
            var childrenFiles = item.Children
                .Intersect(UnsavedItems)
                .ToDictionary(f => f.OriginalName);

            // sync up directory infos
            var infos = bunInst.file.BlockAndDirInfo.DirectoryInfos;
            foreach (var info in infos)
            {
                if (childrenFiles.TryGetValue(info.Name, out var unsavedAssetsFile))
                {
                    if (unsavedAssetsFile.Name != unsavedAssetsFile.OriginalName)
                    {
                        info.Name = unsavedAssetsFile.Name;
                    }

                    if (unsavedAssetsFile.Object is AssetsFileInstance fileInst)
                    {
                        info.SetNewData(fileInst.file);
                    }
                    else if (unsavedAssetsFile.Object is AssetBundleDirectoryInfo matchingInfo && info == matchingInfo)
                    {
                        // do nothing, already handled by replacer
                    }
                    else
                    {
                        // shouldn't happen
                        info.Replacer = null;
                    }
                }
                else
                {
                    // remove replacer (if there was one ever set)
                    info.Replacer = null;
                }
            }

            bunInst.file.Write(new AssetsFileWriter(stream));
        }

        private void WriteResource(WorkspaceItem item, Stream stream)
        {
            var dirInfo = (AssetBundleDirectoryInfo)item.Object!;
            if (dirInfo.Replacer != null)
            {
                dirInfo.Replacer.Write(new AssetsFileWriter(stream));
            }
            else if (item.Parent != null)
            {
                // shouldn't happen
                // var parentBundle = (BundleFileInstance)item.Parent.Object!;
                // var reader = parentBundle.file.DataReader;
                // reader.Position = dirInfo.Offset;
                // reader.BaseStream.CopyToCompat(stream, dirInfo.DecompressedSize);
            }
            else
            {
                // we can't do anything, not enough information
            }
        }

        public async Task SaveAs(WorkspaceItem item)
        {
            if (!UnsavedItems.Contains(item))
            {
                return;
            }

            var storageProvider = StorageService.GetStorageProvider();
            if (storageProvider is null)
            {
                return;
            }

            WorkspaceItemType type = item.ObjectType;
            if (type == WorkspaceItemType.AssetsFile && item.Parent != null)
            {
                // save bundle instead
                item = item.Parent;
                type = item.ObjectType;
            }

            if (type == WorkspaceItemType.AssetsFile)
            {
                var result = await ShowSaveAsDialog(storageProvider, item.Name);
                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteAssetsFile(item, stream);
                }
            }
            else if (type == WorkspaceItemType.BundleFile)
            {
                var result = await ShowSaveAsDialog(storageProvider, item.Name);
                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteBundleFile(item, stream);
                }
            }
            else if (type == WorkspaceItemType.ResourceFile)
            {
                var result = await ShowSaveAsDialog(storageProvider, item.Name);
                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteResource(item, stream);
                }
            }
        }

        public async Task SaveAllAs()
        {
            var storageProvider = StorageService.GetStorageProvider();
            if (storageProvider is null)
            {
                return;
            }

            var unsavedAssetsFiles = UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.AssetsFile);
            foreach (var unsavedAssetsFile in unsavedAssetsFiles)
            {
                // skip assets files in bundles
                if (unsavedAssetsFile.Parent != null)
                    continue;

                var result = await ShowSaveAsDialog(storageProvider, unsavedAssetsFile.Name);
                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteAssetsFile(unsavedAssetsFile, stream);
                }
            }

            var unsavedBundleFiles = UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.BundleFile);
            foreach (var unsavedBundleFile in unsavedBundleFiles)
            {
                var result = await ShowSaveAsDialog(storageProvider, unsavedBundleFile.Name);
                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteBundleFile(unsavedBundleFile, stream);
                }
            }

            // this is impossible right now since resource files can't normally be opened
            var unsavedResourceFiles = UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.ResourceFile);
            foreach (var unsavedResourceFile in unsavedResourceFiles)
            {
                // skip resource files in bundles
                if (unsavedResourceFile.Parent != null)
                    continue;

                var result = await ShowSaveAsDialog(storageProvider, unsavedResourceFile.Name);
                if (result != null)
                {
                    using var stream = await result!.OpenWriteAsync();
                    WriteResource(unsavedResourceFile, stream);
                }
            }
        }
    }
}
