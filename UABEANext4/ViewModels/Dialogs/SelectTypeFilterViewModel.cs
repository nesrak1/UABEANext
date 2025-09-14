using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;
public partial class SelectTypeFilterViewModel : ViewModelBase, IDialogAware<IEnumerable<TypeFilterTypeEntry>?>
{
    [ObservableProperty]
    public ObservableCollection<TypeFilterTypeEntry> _filterTypes = [];

    public string Title => "Select Type Filter";
    public int Width => 300;
    public int Height => 500;
    public event Action<IEnumerable<TypeFilterTypeEntry>?>? RequestClose;

    public SelectTypeFilterViewModel(List<TypeFilterTypeEntry> filterTypes)
    {
        FilterTypes.AddRange(filterTypes);
    }

    public void SelectAll()
    {
        foreach (var filterType in FilterTypes)
        {
            filterType.IsSelected = true;
        }
    }

    public void DeselectAll()
    {
        foreach (var filterType in FilterTypes)
        {
            filterType.IsSelected = false;
        }
    }

    public void Accept()
    {
        bool includeAllMonoBehaviours = false;

        var monoBehaviourType = FilterTypes.FirstOrDefault(ft => ft.TypeId == 0x72 && ft.ScriptRef is null);
        if (monoBehaviourType is not null)
            includeAllMonoBehaviours = monoBehaviourType.IsSelected;

        IEnumerable<TypeFilterTypeEntry> filteredTypes;
        if (includeAllMonoBehaviours)
            filteredTypes = FilterTypes.Where(ft => ft.IsSelected || ft.ScriptRef is not null);
        else
            filteredTypes = FilterTypes.Where(ft => ft.IsSelected);

        RequestClose?.Invoke(filteredTypes);
    }

    public void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    public static List<TypeFilterTypeEntry> MakeTypeFilterTypes(Workspace workspace, IList<AssetInst> assets)
    {
        var uniqueTypeIds = new HashSet<int>();
        var uniqueMonoIdPairs = new Dictionary<AssetsFileInstance, HashSet<ushort>>();
        var uniqueScriptPtrs = new HashSet<AssetPPtr>();
        foreach (var asset in assets)
        {
            var typeId = asset.TypeId;
            // if a non-script or a script with no script binding, treat as regular type
            if (typeId != 0x72)
            {
                uniqueTypeIds.Add(typeId);
            }
            else
            {
                var assetFileInst = asset.FileInstance;
                var scriptIndex = asset.GetScriptIndex(assetFileInst.file);
                if (scriptIndex == ushort.MaxValue)
                {
                    uniqueTypeIds.Add(typeId);
                }
                else
                {
                    // a bit slow but I don't know what else to do
                    if (!uniqueMonoIdPairs.TryGetValue(assetFileInst, out HashSet<ushort>? value))
                        uniqueMonoIdPairs[assetFileInst] = value = [];

                    uniqueTypeIds.Add(0x72);
                    value.Add(scriptIndex);
                }
            }
        }

        var filterTypes = new List<TypeFilterTypeEntry>();
        foreach (var uniqueTypeId in uniqueTypeIds)
        {
            filterTypes.Add(TypeFilterTypeEntry.FromTypeId(uniqueTypeId));
        }

        // deduplicate mono ids by converting them to global pptrs
        foreach (var uniqueMonoIdPair in uniqueMonoIdPairs)
        {
            var uniqueMonoIdFile = uniqueMonoIdPair.Key;
            var uniqueMonoIds = uniqueMonoIdPair.Value;
            foreach (var uniqueMonoId in uniqueMonoIds)
            {
                var scriptPtr = uniqueMonoIdFile.file.Metadata.ScriptTypes[uniqueMonoId];
                scriptPtr.SetFilePathFromFile(workspace.Manager, uniqueMonoIdFile);

                // SetFilePathFromFile changes hash method to file name
                // rather than file id, so this is fine.
                uniqueScriptPtrs.Add(scriptPtr);
            }
        }

        foreach (var uniqueScriptPtr in uniqueScriptPtrs)
        {
            var scriptTypeRef = AssetNameUtils.GetAssetsFileScriptInfo(workspace.Manager, uniqueScriptPtr);
            if (scriptTypeRef is not null)
            {
                filterTypes.Add(TypeFilterTypeEntry.FromTypeReference(scriptTypeRef));
            }
        }

        var filterTypesSorted = filterTypes
            .OrderBy(a => a.ScriptRef is not null)
            .ThenBy(a => a.DisplayText)
            .ToList();

        return filterTypesSorted;
    }
}

public partial class TypeFilterTypeEntry : ObservableObject
{
    public required string DisplayText { get; set; }
    public required int TypeId { get; set; }
    public required AssetTypeReference? ScriptRef { get; set; }

    [ObservableProperty]
    public bool _isSelected = false;

    public static TypeFilterTypeEntry FromTypeId(int typeId)
    {
        return new TypeFilterTypeEntry
        {
            TypeId = typeId,
            DisplayText = Enum.GetName(typeof(AssetClassID), typeId)
                ?? $"Unknown #{typeId}",
            ScriptRef = null
        };
    }

    public static TypeFilterTypeEntry FromTypeReference(AssetTypeReference reference)
    {
        return new TypeFilterTypeEntry
        {
            TypeId = 0x72,
            DisplayText = reference.Namespace != ""
                ? $"MB {reference.Namespace}.{reference.ClassName}"
                : $"MB {reference.ClassName}",
            ScriptRef = reference
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is not TypeFilterTypeEntry otherObj)
            return false;

        if (ScriptRef is null)
        {
            if (otherObj.ScriptRef is null)
                return TypeId == otherObj.TypeId;
        }
        else if (ScriptRef is not null)
        {
            if (otherObj.ScriptRef is not null)
                return ScriptRef.Equals(otherObj.ScriptRef) && TypeId == otherObj.TypeId;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TypeId, ScriptRef);
    }

    public override string ToString()
    {
        return DisplayText;
    }
}
