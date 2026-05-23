using AssetsTools.NET;
using AssetsTools.NET.Extra;
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
using System.Text.RegularExpressions;
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
    public ObservableCollection<SearchResultItem> _searchResults = [];
    [ObservableProperty]
    public bool _isCaseInsensitive = false;

    private HashSet<TypeFilterTypeEntry> _selectedFilterEntries = [];
    private List<TypeFilterTypeEntry>? _filterTypes = null;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressStatus = "";

    [ObservableProperty]
    private bool _isBusy;

    public record TextEncodingInfo(string Name, Encoding Encoding);
    public record SearchKindInfo(AssetDataSearchKind Kind, string Name);

    [ObservableProperty]
    public List<TextEncodingInfo> _availableEncodings = new()
    {
        new TextEncodingInfo("UTF-8", Encoding.UTF8),
        new TextEncodingInfo("UTF-16 LE", Encoding.Unicode),
        new TextEncodingInfo("UTF-16 BE", Encoding.BigEndianUnicode),
        new TextEncodingInfo("ASCII", Encoding.ASCII),
    };

    [ObservableProperty]
    public List<SearchKindInfo> _searchKinds = new()
    {
        new(AssetDataSearchKind.Bytes, "Bytes"),
        new(AssetDataSearchKind.Text, "Text"),
        new(AssetDataSearchKind.Signed4Byte, "Signed 4-byte"),
        new(AssetDataSearchKind.Signed8Byte, "Signed 8-byte"),
        new(AssetDataSearchKind.Unsigned4Byte, "Unsigned 4-byte"),
        new(AssetDataSearchKind.Unsigned8Byte, "Unsigned 8-byte"),
        new(AssetDataSearchKind.Float4Byte, "Float 4-byte"),
        new(AssetDataSearchKind.Float8Byte, "Float 8-byte"),
        new(AssetDataSearchKind.Font, "Font")
    };

    [ObservableProperty]
    public TextEncodingInfo _selectedEncoding;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffsetVisible))]
    [NotifyPropertyChangedFor(nameof(IsTextSearchOptionsVisible))]
    public SearchKindInfo _selectedSearchKind;

    public bool IsOffsetVisible => SelectedSearchKind?.Kind != AssetDataSearchKind.Font;
    public bool IsTextSearchOptionsVisible => SelectedSearchKind?.Kind == AssetDataSearchKind.Text;

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
        _selectedEncoding = AvailableEncodings[0];
        _selectedSearchKind = SearchKinds[0];
    }

    public AssetDataSearchViewModel(Workspace workspace, List<AssetsFileInstance> items)
    {
        _workspace = workspace;
        _items = items;
        _selectedEncoding = AvailableEncodings[0];
        _selectedSearchKind = SearchKinds[0];
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
        if (IsBusy)
            return;

        _workspace.ModifyMutex.WaitOne();
        IsBusy = true;
        ProgressValue = 0;
        SearchResults.Clear();

        try
        {
            if (SelectedSearchKind.Kind == AssetDataSearchKind.Font)
            {
                await SearchFonts();
            }
            else
            {
                await SearchByContent();
            }
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Search Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _workspace.ModifyMutex.ReleaseMutex();
        }
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

        switch (SelectedSearchKind.Kind)
        {
            case AssetDataSearchKind.Bytes:
                searchBytes = Convert.FromHexString(SearchText.Replace(" ", ""));
                break;

            case AssetDataSearchKind.Text:
                if (string.IsNullOrEmpty(SearchText))
                    return Array.Empty<byte>();

                string searchStr = IsCaseInsensitive ? SearchText.ToLowerInvariant() : SearchText;
                return SelectedEncoding.Encoding.GetBytes(searchStr);

            case AssetDataSearchKind.Signed4Byte:
                if (!int.TryParse(SearchText, out int searchInt))
                    return null;
                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteInt32BigEndian(searchBytes, searchInt);
                else
                    BinaryPrimitives.WriteInt32LittleEndian(searchBytes, searchInt);
                break;

            case AssetDataSearchKind.Signed8Byte:
                if (!long.TryParse(SearchText, out long searchLong))
                    return null;
                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteInt64BigEndian(searchBytes, searchLong);
                else
                    BinaryPrimitives.WriteInt64LittleEndian(searchBytes, searchLong);
                break;

            case AssetDataSearchKind.Unsigned4Byte:
                if (!uint.TryParse(SearchText, out uint searchUint))
                    return null;
                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteUInt32BigEndian(searchBytes, searchUint);
                else
                    BinaryPrimitives.WriteUInt32LittleEndian(searchBytes, searchUint);
                break;

            case AssetDataSearchKind.Unsigned8Byte:
                if (!ulong.TryParse(SearchText, out ulong searchUlong))
                    return null;
                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteUInt64BigEndian(searchBytes, searchUlong);
                else
                    BinaryPrimitives.WriteUInt64LittleEndian(searchBytes, searchUlong);
                break;

            case AssetDataSearchKind.Float4Byte:
                if (!float.TryParse(SearchText, out float searchFloat))
                    return null;
                searchBytes = new byte[4];
                if (bigEndian)
                    BinaryPrimitives.WriteSingleBigEndian(searchBytes, searchFloat);
                else
                    BinaryPrimitives.WriteSingleLittleEndian(searchBytes, searchFloat);
                break;

            case AssetDataSearchKind.Float8Byte:
                if (!double.TryParse(SearchText, out double searchDouble))
                    return null;
                searchBytes = new byte[8];
                if (bigEndian)
                    BinaryPrimitives.WriteDoubleBigEndian(searchBytes, searchDouble);
                else
                    BinaryPrimitives.WriteDoubleLittleEndian(searchBytes, searchDouble);
                break;

            default:
                return null;
        }

        return searchBytes;
    }

    private async Task SearchByContent()
    {
        byte[]? searchBytes = GetSearchBytes();
        if (searchBytes is null)
        {
            await MessageBoxUtil.ShowDialog("Invalid input", "Input is invalid for this search kind.");
            return;
        }

        await Task.Run(() =>
        {
            var allFound = new List<SearchResultItem>();
            int currentCount = 0;
            int itemCount = _items.Count;
            const int maxResults = 40000;

            foreach (var fileInst in _items)
            {
                var assetInfos = fileInst.file.AssetInfos;
                var dataOffset = fileInst.file.Header.DataOffset;

                Dispatcher.UIThread.Post(() => ProgressStatus = $"Searching in {fileInst.name}...");

                var stream = fileInst.AssetsStream;
                lock (fileInst.LockReader)
                {
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

                            var currentEntry = new TypeFilterTypeEntry
                            {
                                TypeId = info.TypeId,
                                ScriptRef = sRef,
                                DisplayText = ""
                            };

                            if (!_selectedFilterEntries.Contains(currentEntry))
                            {
                                continue;
                            }

                            byte[] assetData = new byte[info.ByteSize];
                            stream.Position = info.ByteOffset + dataOffset;
                            stream.Read(assetData, 0, (int)info.ByteSize);

                            int matchIdx = -1;

                            if (IsCaseInsensitive && SelectedSearchKind.Kind == AssetDataSearchKind.Text)
                            {
                                string assetText = SelectedEncoding.Encoding.GetString(assetData);
                                int charIdx = assetText.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase);
                                if (charIdx != -1)
                                {
                                    matchIdx = SelectedEncoding.Encoding.GetByteCount(assetText.Substring(0, charIdx));
                                }
                            }
                            else
                            {
                                matchIdx = SearchLogic.IndexOfBytes(assetData, searchBytes);
                            }

                            if (matchIdx != -1)
                            {
                                string offsetHex = $"0x{info.ByteOffset + dataOffset + matchIdx:X}";
                                allFound.Add(CreateResult(fileInst, info, offsetHex));
                            }
                            if (allFound.Count >= maxResults)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        var foundPathIds = new HashSet<long>();
                        var matches = SearchLogic.FindAllSubstringsInStream(stream, searchBytes, IsCaseInsensitive && SelectedSearchKind.Kind == AssetDataSearchKind.Text,
                            SelectedEncoding.Encoding, SearchText);
                        var enumerator = matches.GetEnumerator();

                        while (enumerator.MoveNext())
                        {
                            long pos = enumerator.Current;
                            long relativeOffset = pos - dataOffset;

                            int searchIdx = assetInfos.BinarySearch(new AssetFileInfo
                            {
                                ByteOffset = relativeOffset
                            },
                            (i, j) => i.ByteOffset.CompareTo(j.ByteOffset));
                            AssetFileInfo? info = null;
                            if (searchIdx >= 0)
                            {
                                info = assetInfos[searchIdx];
                            }
                            else if (~searchIdx - 1 >= 0)
                            {
                                info = assetInfos[~searchIdx - 1];
                            }

                            if (info != null && relativeOffset < (info.ByteOffset + info.ByteSize))
                            {
                                if (foundPathIds.Add(info.PathId))
                                {
                                    string offsetHex = $"0x{pos:X}";
                                    allFound.Add(CreateResult(fileInst, info, offsetHex));
                                    stream.Position = info.ByteOffset + info.ByteSize + dataOffset;
                                }
                            }
                            if (allFound.Count >= maxResults)
                            {
                                break;
                            }
                        }
                    }
                }

                currentCount++;
                ProgressValue = (double)currentCount / itemCount * 100;
                if (allFound.Count >= maxResults)
                {
                    break;
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                SearchResults = new ObservableCollection<SearchResultItem>(allFound);
                ProgressStatus = $"Done. Found {allFound.Count} assets.";
                _workspace.SetProgressThreadSafe(1f, ProgressStatus);
            });
        });
    }

    private async Task SearchFonts()
    {
        string searchFilter = SearchText.Trim();
        bool hasFilter = !string.IsNullOrWhiteSpace(searchFilter);

        await Task.Run(() =>
        {
            var allFound = new List<SearchResultItem>();
            int currentCount = 0;
            int itemCount = _items.Count;
            const int maxResults = 40000;

            foreach (var fileInst in _items)
            {
                if (IsExcludedFile(fileInst.name))
                {
                    currentCount++;
                    ProgressValue = (double)currentCount / itemCount * 100;
                    continue;
                }
                var assetInfos = fileInst.file.AssetInfos;
                Dispatcher.UIThread.Post(() => ProgressStatus = $"Searching in {fileInst.name}...");

                lock (fileInst.LockReader)
                {
                    foreach (var info in assetInfos)
                    {
                        if (info.TypeId == (int)AssetClassID.Font || info.TypeId == (int)AssetClassID.MonoBehaviour)
                        {
                            AssetInst? asset = info as AssetInst;

                            if (hasFilter)
                            {
                                string assetName = asset?.DisplayName ?? "";
                                if (!assetName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                            }

                            if (info.TypeId == (int)AssetClassID.Font)
                            {
                                allFound.Add(CreateResult(fileInst, info));
                            }
                            else if (info.TypeId == (int)AssetClassID.MonoBehaviour)
                            {
                                if (asset != null)
                                {
                                    var baseField = _workspace.GetBaseField(asset);
                                    if (baseField != null)
                                    {
                                        if (CheckFieldIsFont(baseField))
                                        {
                                            allFound.Add(CreateResult(fileInst, info));

                                            var textures = FindReferenceTextures(asset, baseField);
                                            allFound.AddRange(textures);
                                        }
                                    }
                                }
                            }
                        }

                        if (allFound.Count >= maxResults)
                        {
                            break;
                        }
                    }
                }

                currentCount++;
                ProgressValue = (double)currentCount / itemCount * 100;
                if (allFound.Count >= maxResults)
                {
                    break;
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                SearchResults = new ObservableCollection<SearchResultItem>(allFound);
                ProgressStatus = $"Done. Found {allFound.Count} assets.";
                _workspace.SetProgressThreadSafe(1f, ProgressStatus);
            });
        });
    }

    private bool CheckFieldIsFont(AssetTypeValueField field)
    {
        if (field.FieldName.Contains("m_FaceInfo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int i = 0; i < field.Children.Count; i++)
        {
            if (CheckFieldIsFont(field.Children[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedFile(string name)
    {
        Regex levelFileRegex = new(@"^level\d", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        if (string.IsNullOrEmpty(name))
            return false;

        if (name.StartsWith("globalgamemanagers", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("unity default resources", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return levelFileRegex.IsMatch(name);
    }

    private List<SearchResultItem> FindReferenceTextures(AssetInst asset, AssetTypeValueField field)
    {
        var found = new List<SearchResultItem>();
        AssetTypeValueField? textureRef = field.Get("m_AtlasTextures");

        if (textureRef != null)
        {
            foreach (var child in textureRef.Children)
            {
                if (child.Value.AsObject is AssetTypeArrayInfo assetArray)
                {
                    foreach (var assetRef in child.Children)
                    {
                        AssetInst? cont = _workspace.GetAssetInst(asset.FileInstance, assetRef);
                        if (cont == null)
                        {
                            continue;
                        }
                        found.Add(CreateResult(cont.FileInstance, cont));
                    }
                }
            }
        }
        return found;
    }

    private SearchResultItem CreateResult(AssetsFileInstance fileInst, AssetFileInfo info, string? offset = null)
    {
        var asset = info as AssetInst;
        return new SearchResultItem
        {
            FileName = fileInst.name,
            AssetName = asset != null ? asset.DisplayName : $"{info.TypeId} asset",
            PathId = info.PathId,
            Type = (AssetClassID)info.TypeId,
            Offset = offset,
            Asset = asset
        };
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
        Float8Byte,
        Font
    }
}