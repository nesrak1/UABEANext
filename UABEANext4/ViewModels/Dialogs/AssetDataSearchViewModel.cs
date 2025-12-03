using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;
public partial class AssetDataSearchViewModel : ViewModelBase, IDialogAware<string?>
{
    [ObservableProperty]
    public string _searchText = "";
    [ObservableProperty]
    public AssetDataSearchKind _searchKind = AssetDataSearchKind.Bytes;
    [ObservableProperty]
    public ObservableCollection<string> _searchResults = [];

    private readonly Workspace _workspace;
    private readonly List<AssetsFileInstance> _items;

    public string Title => "Search hex (WIP)";
    public int Width => 350;
    public int Height => 400;
    public event Action<string?>? RequestClose;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public AssetDataSearchViewModel()
    {
        _workspace = new();
        _items = [];
    }

    public AssetDataSearchViewModel(Workspace workspace, List<AssetsFileInstance> items)
    {
        _workspace = workspace;
        _items = items;
    }

    public async Task BtnSearch_Click()
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount - 1, 4)
        };

        byte[]? searchBytes = GetSearchBytes();
        if (searchBytes is null)
        {
            await MessageBoxUtil.ShowDialog("Invalid input", "Input is invalid for this search kind.");
            return;
        }

        _workspace.SetProgressThreadSafe(0f, "Searching files...");
        await Task.Run(() =>
        {
            _workspace.ModifyMutex.WaitOne();
            _workspace.ProgressValue = 0;
            SearchResults.Clear();
            int currentCount = 0;
            int itemCount = _items.Count;
            Parallel.ForEach(_items, options, (fileInst, state, index) =>
            {
                // this is always an ObservableCollection for us which means
                // we have to use the special extension BinarySearch method.
                var assetInfos = (ObservableCollection<AssetFileInfo>)fileInst.file.AssetInfos;

                foreach (long pos in FindAllSubstringsInStream(fileInst.AssetsStream, searchBytes))
                {
                    int searchIdx = assetInfos.BinarySearch(
                        new AssetFileInfo()
                        {
                            ByteOffset = pos - fileInst.file.Header.DataOffset,
                        },
                        (i, j) => i.ByteOffset.CompareTo(j.ByteOffset)
                    );

                    if (searchIdx == -1)
                    {
                        // didn't find anything? string was probably found outside of an asset.
                        // let's just put the address so the user can look for it themselves.
                        if (fileInst.parentBundle is not null)
                            SearchResults.Add($"{fileInst.parentBundle.name} {fileInst.name} @ {pos:x2}");
                        else
                            SearchResults.Add($"{fileInst.name} @ {pos:x2}");
                    }
                    else
                    {
                        AssetFileInfo? info = (searchIdx < 0)
                            ? assetInfos[~searchIdx - 1]
                            : assetInfos[searchIdx];

                        var name = (info is AssetInst asset)
                            ? asset.DisplayName
                            : $"{info.TypeId} asset";

                        if (fileInst.parentBundle is not null)
                            SearchResults.Add($"{fileInst.parentBundle.name} {fileInst.name} -> {name}, {info.PathId}");
                        else
                            SearchResults.Add($"{fileInst.name} -> {name}, {info.PathId}");
                    }

                }

                var currentCountNow = Interlocked.Increment(ref currentCount);
                _workspace.SetProgressThreadSafe((float)currentCountNow / itemCount, $"Searching file {fileInst.name}...");
            });
            _workspace.SetProgressThreadSafe(1f, "Done");
            _workspace.ModifyMutex.ReleaseMutex();
        });
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }

    private byte[]? GetSearchBytes()
    {
        var bigEndian = _items.Count > 0 && _items[0].file.Header.Endianness;

        byte[] searchBytes;
        switch (SearchKind)
        {
            case AssetDataSearchKind.Bytes:
            {
                searchBytes = Convert.FromHexString(SearchText.Replace(" ", ""));
                break;
            }
            case AssetDataSearchKind.Text:
            {
                searchBytes = Encoding.UTF8.GetBytes(SearchText);
                break;
            }
            case AssetDataSearchKind.Signed4Byte:
            {
                if (!int.TryParse(SearchText, out int searchInt))
                    return null;

                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteInt32BigEndian(searchBytes, searchInt);
                else
                    BinaryPrimitives.WriteInt32LittleEndian(searchBytes, searchInt);

                break;
            }
            case AssetDataSearchKind.Signed8Byte:
            {
                if (!long.TryParse(SearchText, out long searchLong))
                    return null;

                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteInt64BigEndian(searchBytes, searchLong);
                else
                    BinaryPrimitives.WriteInt64LittleEndian(searchBytes, searchLong);

                break;
            }
            case AssetDataSearchKind.Unsigned4Byte:
            {
                if (!uint.TryParse(SearchText, out uint searchUint))
                    return null;

                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteUInt32BigEndian(searchBytes, searchUint);
                else
                    BinaryPrimitives.WriteUInt32LittleEndian(searchBytes, searchUint);

                break;
            }
            case AssetDataSearchKind.Unsigned8Byte:
            {
                if (!ulong.TryParse(SearchText, out ulong searchUlong))
                    return null;

                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteUInt64BigEndian(searchBytes, searchUlong);
                else
                    BinaryPrimitives.WriteUInt64LittleEndian(searchBytes, searchUlong);

                break;
            }
            case AssetDataSearchKind.Float4Byte:
            {
                if (!float.TryParse(SearchText, out float searchFloat))
                    return null;

                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteSingleBigEndian(searchBytes, searchFloat);
                else
                    BinaryPrimitives.WriteSingleLittleEndian(searchBytes, searchFloat);

                break;
            }
            case AssetDataSearchKind.Float8Byte:
            {
                if (!double.TryParse(SearchText, out double searchDouble))
                    return null;

                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteDoubleBigEndian(searchBytes, searchDouble);
                else
                    BinaryPrimitives.WriteDoubleLittleEndian(searchBytes, searchDouble);

                break;
            }
            default:
            {
                return null;
            }
        }

        return searchBytes;
    }

    private static IEnumerable<long> FindAllSubstringsInStream(Stream fs, byte[] patternBytes)
    {
        const int ChunkSize = 65536;

        int patternLength = patternBytes.Length;
        int overlap = patternLength > 1 ? patternLength - 1 : 0;

        byte[] buffer = new byte[ChunkSize];
        long currentPosition = 0;
        int bytesRead;

        fs.Position = 0;
        while ((bytesRead = fs.Read(buffer, 0, ChunkSize)) > 0)
        {
            int indexInChunk;
            int searchStart = 0;
            while ((indexInChunk = IndexOfBytes(buffer, patternBytes, searchStart)) != -1)
            {
                long absolutePosition = currentPosition + indexInChunk;

                yield return absolutePosition;

                searchStart = indexInChunk + patternLength;

                if (searchStart >= bytesRead)
                {
                    break;
                }
            }

            if (bytesRead == ChunkSize && fs.Position < fs.Length)
            {
                fs.Seek(-overlap, SeekOrigin.Current);
            }

            currentPosition += bytesRead - overlap;
        }
    }

    public static int IndexOfBytes(byte[] buffer, byte[] pattern, int start = 0)
    {
        if (buffer == null || pattern == null || pattern.Length == 0) return -1;
        if (start < 0 || start > buffer.Length - pattern.Length) return -1;

        var span = buffer.AsSpan(start);
        for (int i = 0; i <= span.Length - pattern.Length; i++)
        {
            if (span.Slice(i, pattern.Length).SequenceEqual(pattern))
                return i + start;
        }
        return -1;
    }

    public enum AssetDataSearchKind
    {
        Bytes,
        Text,
        Signed4Byte,
        Signed8Byte,
        Unsigned4Byte,
        Unsigned8Byte,
        Float4Byte,
        Float8Byte
    }
}
