using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture.TextureDecoders.CrnUnity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Logic;
using UABEANext4.Logic.Search;
using UABEANext4.Services;
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

    private HashSet<TypeFilterTypeEntry> _selectedFilterEntries = [];
    private List<TypeFilterTypeEntry>? _filterTypes = null;
    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressStatus = "";

    [ObservableProperty]
    private bool _isBusy;

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

    [RelayCommand]
    public async Task SetTypeFilter()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        if (_filterTypes == null)
        {
            _filterTypes = SelectTypeFilterViewModel.MakeTypeFilterTypes(_workspace, _items);
        }

        var result = await dialogService.ShowDialog(new SelectTypeFilterViewModel(_filterTypes));
        if (result != null)
        {
            _selectedFilterEntries = result.ToHashSet();

        }
    }

    public async Task BtnSearch_Click()
    {
        byte[]? searchBytes = GetSearchBytes();
        if (searchBytes is null || IsBusy) return;

        IsBusy = true;
        ProgressValue = 0;
        SearchResults.Clear();

        await Task.Run(() =>
        {
            var allFound = new List<SearchResultItem>();
            int currentCount = 0;
            int itemCount = _items.Count;

            foreach (var fileInst in _items)
            {
                var assetInfos = fileInst.file.AssetInfos;
                var dataOffset = fileInst.file.Header.DataOffset;

                Dispatcher.UIThread.Post(() => ProgressStatus = $"Searching in {fileInst.name}...");
                _workspace.SetProgressThreadSafe(0f, "Searching...");

                if (_selectedFilterEntries.Count > 0)
                {
                    foreach (var info in assetInfos)
                    {
                        AssetTypeReference? sRef = null;
                        if (info.TypeId == 0x72 || info.TypeId < 0)
                        {
                            ushort sIdx = info.GetScriptIndex(fileInst.file);
                            if (sIdx != ushort.MaxValue)
                            {
                                var sPtr = fileInst.file.Metadata.ScriptTypes[sIdx];
                                sPtr.SetFilePathFromFile(_workspace.Manager, fileInst);
                                sRef = SelectTypeFilterViewModel.GetAssetsFileScriptInfo(_workspace.Manager, sPtr);
                            }
                        }

                        var currentEntry = new TypeFilterTypeEntry { 
                            TypeId = info.TypeId, 
                            ScriptRef = sRef,
                            DisplayText = ""
                        };
                        if (!_selectedFilterEntries.Contains(currentEntry))
                            continue;

                        byte[] assetData = new byte[info.ByteSize];
                        lock (fileInst.LockReader)
                        {
                            fileInst.AssetsStream.Position = info.ByteOffset + dataOffset;
                            fileInst.AssetsStream.Read(assetData, 0, (int)info.ByteSize);
                        }

                        int matchIdx = SearchLogic.IndexOfBytes(assetData, searchBytes);
                        if (matchIdx != -1)
                        {
                            allFound.Add(new SearchResultItem
                            {
                                FileName = fileInst.name,
                                AssetName = (info is AssetInst a) ? a.DisplayName : $"{info.TypeId} asset",
                                PathId = info.PathId,
                                Type = (AssetClassID)info.TypeId,
                                Offset = $"0x{info.ByteOffset + dataOffset + matchIdx:X}",
                                Asset = info as AssetInst
                            });
                        }
                       /* if (allFound.Count >= 40000)
                            break;*/
                    }
                }
                else
                {
                    using (var stream = fileInst.AssetsStream)
                    {
                        var matches = SearchLogic.FindAllSubstringsInStream(stream, searchBytes);
                        var enumerator = matches.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            long pos = enumerator.Current;
                            long relativeOffset = pos - dataOffset;
                            int searchIdx = assetInfos.BinarySearch(new AssetFileInfo {
                                ByteOffset = relativeOffset },
                                (i, j) => i.ByteOffset.CompareTo(j.ByteOffset));

                            AssetFileInfo? info = null;
                            if (searchIdx >= 0) 
                                info = assetInfos[searchIdx];
                            else if (~searchIdx - 1 >= 0)
                                info = assetInfos[~searchIdx - 1];

                            if (info != null && relativeOffset < (info.ByteOffset + info.ByteSize))
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
                                stream.Position = info.ByteOffset + info.ByteSize + dataOffset;
                            }
                            if (allFound.Count >= 40000)
                                break;
                        }
                    }
                }

                currentCount++;
                ProgressValue = (double)currentCount / itemCount * 100;
            }

            Dispatcher.UIThread.Post(() =>
            {
                SearchResults = new ObservableCollection<SearchResultItem>(allFound);
                ProgressStatus = $"Done. Found {allFound.Count} assets.";
                _workspace.SetProgressThreadSafe(1f, ProgressStatus);
                IsBusy = false;
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
