using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Logic;
using UABEANext4.Logic.Search;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;
public partial class AssetDataSearchViewModel : ViewModelBase, IDialogAware<string?>
{
    [ObservableProperty]
    public string _searchText = "";
    [ObservableProperty]
    public AssetDataSearchKind _searchKind = AssetDataSearchKind.Bytes;
    [ObservableProperty]
    public ObservableCollection<SearchResultItem> _searchResults = [];

    private readonly Workspace _workspace;
    private readonly List<AssetsFileInstance> _items;

    public string Title => "Search by Content";
    public int Width => 600;
    public int Height => 400;
    public bool IsModal => false;

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
        byte[]? searchBytes = GetSearchBytes();
        if (searchBytes is null)
            return;

        SearchResults.Clear();
        _workspace.SetProgressThreadSafe(0f, "Searching...");

        await Task.Run(() =>
        {
            var allFound = new List<SearchResultItem>();
            int currentCount = 0;
            int itemCount = _items.Count;

            foreach (var fileInst in _items)
            {
                var assetInfos = fileInst.file.AssetInfos;
                var assetsCount = assetInfos.Count;

                foreach (long pos in SearchLogic.FindAllSubstringsInStream(fileInst.AssetsStream, searchBytes))
                {
                    long relativeOffset = pos - fileInst.file.Header.DataOffset;

                    int searchIdx = assetInfos.BinarySearch(
                        new AssetFileInfo { ByteOffset = relativeOffset },
                        (i, j) => i.ByteOffset.CompareTo(j.ByteOffset)
                    );

                    AssetFileInfo? info = null;
                    if (searchIdx >= 0)
                    {
                        info = assetInfos[searchIdx];
                    }
                    else if (~searchIdx - 1 >= 0)
                    {
                        info = assetInfos[~searchIdx - 1];
                    }

                    if (info != null)
                    {
                        allFound.Add(new SearchResultItem
                        {
                            FileName = fileInst.name,
                            AssetName = (info is AssetInst a) ? a.DisplayName : $"{info.TypeId} asset",
                            PathId = info.PathId,
                            Type = (AssetClassID)info.TypeId,
                            Offset = $"0x{pos:X}",
                            Asset = info as AssetInst
                        });
                    }

                    // limit results to 40,000 for performance reasons
                    if (allFound.Count > 40000)
                        break;
                }

                currentCount++;
                _workspace.SetProgressThreadSafe((float)currentCount / itemCount, $"Searching {fileInst.name}...");
            }

            Dispatcher.UIThread.Post(() =>
            {
                SearchResults = new ObservableCollection<SearchResultItem>(allFound);
       
                _workspace.SetProgressThreadSafe(1f, $"Found {allFound.Count} results");
            });
        });
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }

    [RelayCommand]
    public void VisitAsset(SearchResultItem? item)
    {
        if (item?.Asset is AssetInst asset)
        {
            WeakReferenceMessenger.Default.Send(new RequestVisitAssetMessage(asset));
        }
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
