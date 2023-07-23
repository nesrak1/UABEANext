using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;

namespace UABEANext3.Util
{
    public static class FileDialogUtils
    {
        public static string[] GetOpenFileDialogFiles(IReadOnlyList<IStorageFile> files)
        {
            return files.Select(sf => sf.TryGetLocalPath()).Where(p => p != null).ToArray()!;
        }

        public static string[] GetOpenFolderDialogFiles(IReadOnlyList<IStorageFolder> folders)
        {
            return folders.Select(sf => sf.TryGetLocalPath()).Where(p => p != null).ToArray()!;
        }

        public static string? GetSaveFileDialogFile(IStorageFile? file)
        {
            return file?.TryGetLocalPath();
        }
    }
}
