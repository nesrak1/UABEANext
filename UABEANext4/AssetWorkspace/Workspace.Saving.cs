using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.Util;

namespace UABEANext4.AssetWorkspace;

// this contains saving logic for workspace
// dialogs are allowed in this class (instead
// of being handled in the view model they
// were called from)
public partial class Workspace
{
    private static async Task<IStorageFile?> ShowSaveAsDialog(IStorageProvider storageProvider, string suggestedFileName)
    {
        return await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save file",
            FileTypeChoices = new FilePickerFileType[]
            {
                new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } }
            },
            SuggestedFileName = suggestedFileName
        });
    }

    private static bool TryGetFileStream(WorkspaceItem item, [NotNullWhen(true)] out FileStream? fs)
    {
        if (item.Object is AssetsFileInstance fileInst)
        {
            if (fileInst.AssetsStream is FileStream assetsInstFs)
            {
                fs = assetsInstFs;
                return true;
            }
        }
        else if (item.Object is BundleFileInstance bunInst)
        {
            if (bunInst.BundleStream is FileStream bundleInstFs)
            {
                fs = bundleInstFs;
                return true;
            }
        }

        fs = null;
        return false;
    }

    private static bool TryOpenForWriting(string path, [NotNullWhen(true)] out FileStream? writeFs)
    {
        try
        {
            writeFs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            writeFs = null;
            return false;
        }
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
        // we need a resource type before we can do any saving
        var dirInfo = (AssetBundleDirectoryInfo)item.Object!;
        if (dirInfo.Replacer != null)
        {
            dirInfo.Replacer.Write(new AssetsFileWriter(stream), true);
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

    public async Task<bool> Save(WorkspaceItem item)
    {
        if (!UnsavedItems.Contains(item))
        {
            return false;
        }

        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return false;
        }

        var type = item.ObjectType;
        if (type == WorkspaceItemType.AssetsFile && item.Parent != null)
        {
            // pls don't do this
            throw new Exception("Tried to save an assets file that was in a bundle directly");
        }

        if (!TryGetFileStream(item, out var stream))
        {
            await MessageBoxUtil.ShowDialog("Error saving", "Workspace item isn't using a FileStream");
            return false;
        }

        var streamPath = stream.Name;
        // verify we can write to this file
        if (!TryOpenForWriting(streamPath, out var writeStream))
        {
            await MessageBoxUtil.ShowDialog("Error saving", "Couldn't open stream for writing");
            return false;
        }

        var newName = "~" + Path.GetFileName(streamPath);
        var dir = Path.GetDirectoryName(streamPath)!;
        var tempWriteStreamPath = Path.Combine(dir, newName);
        if (!TryOpenForWriting(tempWriteStreamPath, out var tempWriteStream))
        {
            await MessageBoxUtil.ShowDialog("Error saving", "Couldn't open temp file stream for writing");
            return false;
        }

        if (type == WorkspaceItemType.AssetsFile)
        {
            WriteAssetsFile(item, tempWriteStream);
        }
        else if (type == WorkspaceItemType.BundleFile)
        {
            WriteBundleFile(item, tempWriteStream);
        }
        else if (type == WorkspaceItemType.ResourceFile)
        {
            WriteResource(item, tempWriteStream);
        }

        stream.Close();
        writeStream.Close();
        tempWriteStream.Close();
        // technically there's a window here where the file could be reopened
        // and block write access, but let's just assume that won't happen
        // since that complicates things a bit...
        try
        {
            File.Move(tempWriteStreamPath, streamPath, true);
            var newStream = File.Open(streamPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (item.Object is AssetsFileInstance fileInst)
            {
                fileInst.file = new AssetsFile();
                fileInst.file.Read(new AssetsFileReader(newStream));
                item.OriginalName = item.Name;
                foreach (var asset in fileInst.file.AssetInfos)
                {
                    asset.Replacer = null;
                }
                UnsavedItems.Remove(item);
            }
            else if (item.Object is BundleFileInstance bunInst)
            {
                bunInst.file = new AssetBundleFile();
                bunInst.file.Read(new AssetsFileReader(newStream));
                item.OriginalName = item.Name;
                for (var i = 0; i < item.Children.Count; i++)
                {
                    var child = item.Children[i];
                    if (child.Object is AssetsFileInstance childInst)
                    {
                        // workaround to "disable" caching while we reload
                        // a second version of the inst and replace the
                        // AssetsFile inside of the first one with the second's
                        Manager.FileLookup.Remove(AssetsManager.GetFileLookupKey(child.OriginalName));
                        {
                            var afileObj = LoadAssetsFromBundle(bunInst, i);
                            if (afileObj.Object == null)
                            {
                                await MessageBoxUtil.ShowDialog("Error saving", "Reopened file appears to be corrupt");
                                continue;
                            }
                            var afile = ((AssetsFileInstance)afileObj.Object).file;
                            childInst.file = afile;
                        }
                        Manager.FileLookup[AssetsManager.GetFileLookupKey(child.Name)] = childInst;
                    }
                    else if (child.Object is AssetBundleDirectoryInfo)
                    {
                        child.Object = bunInst.file.BlockAndDirInfo.DirectoryInfos[i];
                    }
                    child.OriginalName = child.Name;
                }
                UnsavedItems.Remove(item);
            }
            else
            {
                // can't handle resource or any other files yet
                UnsavedItems.Remove(item);
            }
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Error saving", "Unknown error:\n" + ex);
            return false;
        }
        return true;
    }

    public async Task<bool> SaveAs(WorkspaceItem item)
    {
        if (!UnsavedItems.Contains(item))
        {
            return false;
        }

        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return false;
        }

        var type = item.ObjectType;
        if (type == WorkspaceItemType.AssetsFile && item.Parent != null)
        {
            // pls don't do this
            throw new Exception("Tried to save an assets file that was in a bundle directly");
        }

        var result = await ShowSaveAsDialog(storageProvider, item.Name);
        if (result == null)
        {
            return false;
        }

        try
        {
            using var stream = await result.OpenWriteAsync();
            if (type == WorkspaceItemType.AssetsFile)
            {
                WriteAssetsFile(item, stream);
                UnsavedItems.Remove(item);
            }
            else if (type == WorkspaceItemType.BundleFile)
            {
                WriteBundleFile(item, stream);
                UnsavedItems.Remove(item);
            }
            else if (type == WorkspaceItemType.ResourceFile)
            {
                WriteResource(item, stream);
                UnsavedItems.Remove(item);
            }
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Error saving", "Unknown error:\n" + ex);
            return false;
        }
        return true;
    }

    // todo
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
                try
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteAssetsFile(unsavedAssetsFile, stream);
                }
                catch
                {
                    // put error here
                    continue;
                }
            }
        }

        var unsavedBundleFiles = UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.BundleFile);
        foreach (var unsavedBundleFile in unsavedBundleFiles)
        {
            var result = await ShowSaveAsDialog(storageProvider, unsavedBundleFile.Name);
            if (result != null)
            {
                try
                {
                    using var stream = await result.OpenWriteAsync();
                    WriteBundleFile(unsavedBundleFile, stream);
                }
                catch
                {
                    // put error here
                    continue;
                }
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
                try
                {
                    using var stream = await result!.OpenWriteAsync();
                    WriteResource(unsavedResourceFile, stream);
                }
                catch
                {
                    // put error here
                    continue;
                }
            }
        }
    }
}
