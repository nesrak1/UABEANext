using System;
using System.Collections.Generic;
using System.IO;

namespace UABEANext4.Util;

public static class FileUtils
{
    private static readonly string[] BYTE_SIZE_SUFFIXES = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
    public static string GetFormattedByteSize(long size)
    {
        int log = (int)Math.Log(size, 1024);
        double div = log == 0 ? 1 : Math.Pow(1024, log);
        double num = size / div;
        return $"{num:f2}{BYTE_SIZE_SUFFIXES[log]}";
    }

    public static List<string> GetFilesInDirectory(string path, List<string> extensions)
    {
        List<string> files = new List<string>();
        foreach (string extension in extensions)
        {
            files.AddRange(Directory.EnumerateFiles(path, "*." + extension));
        }
        return files;
    }

    /// <summary>
    /// Builds a dictionary mapping each file's name (without extension) to its full path.
    /// Used by batch import for O(1) suffix-based matching instead of O(N) linear scans.
    /// </summary>
    public static Dictionary<string, List<string>> GetFilesByNameWithoutExtension(string path, List<string> extensions)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string extension in extensions)
        {
            foreach (var filePath in Directory.EnumerateFiles(path, "*." + extension))
            {
                var fileName = Path.GetFileName(filePath)!;
                var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                if (!lookup.TryGetValue(nameWithoutExt, out var list))
                {
                    list = new List<string>(1);
                    lookup[nameWithoutExt] = list;
                }
                list.Add(fileName);
            }
        }
        return lookup;
    }

    /// <summary>
    /// Gets all file names (just the filename, not full path) in a directory as a HashSet for O(1) contains checks.
    /// For 'any extension' mode.
    /// </summary>
    public static Dictionary<string, List<string>> GetAllFilesByNameWithoutExtension(string path)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.EnumerateFiles(path))
        {
            var fileName = Path.GetFileName(filePath)!;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            if (!lookup.TryGetValue(nameWithoutExt, out var list))
            {
                list = new List<string>(1);
                lookup[nameWithoutExt] = list;
            }
            list.Add(fileName);
        }
        return lookup;
    }
}
