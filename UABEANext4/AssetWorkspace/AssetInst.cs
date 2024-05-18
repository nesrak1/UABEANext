using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.ComponentModel;
using UABEANext4.Util;

namespace UABEANext4.AssetWorkspace;

// assetfileinfo wrapper for extra info
public class AssetInst : AssetFileInfo, INotifyPropertyChanged
{
    public string? AssetName { get; set; }
    public string? DisplayContainer { get; set; }
    public AssetsFileInstance FileInstance { get; }

    public AssetClassID Type => (AssetClassID)TypeId;
    public AssetsFileReader FileReader => IsReplacerPreviewable
        ? new AssetsFileReader(Replacer.GetPreviewStream())
        : FileInstance.file.Reader;
    public string FileName => FileInstance.name;
    public long AbsoluteByteStart => IsReplacerPreviewable ? 0 : GetAbsoluteByteOffset(FileInstance.file);
    public string ModifiedString => Replacer != null ? "*" : "";
    public uint ByteSizeModified => Replacer != null && Replacer.HasPreview()
        ? (uint)Replacer.GetPreviewStream().Length
        : ByteSize;
    public string DisplayName => AssetNameUtils.GetFallbackName(this, AssetName);

    public AssetInst(AssetsFileInstance parentFile, AssetFileInfo origInfo)
    {
        PathId = origInfo.PathId;
        ByteOffset = origInfo.ByteOffset;
        ByteSize = origInfo.ByteSize;
        TypeIdOrIndex = origInfo.TypeIdOrIndex;
        OldTypeId = origInfo.OldTypeId;
        ScriptTypeIndex = origInfo.ScriptTypeIndex;
        Stripped = origInfo.Stripped;
        TypeId = origInfo.TypeId;
        Replacer = origInfo.Replacer;

        AssetName = "Unnamed asset";
        FileInstance = parentFile;
    }

    public void UpdateAssetDataAndRow(Workspace workspace, AssetTypeValueField baseField)
    {
        UpdateAssetDataAndRow(workspace, baseField.WriteToByteArray());
    }

    public void UpdateAssetDataAndRow(Workspace workspace, byte[] data)
    {
        SetNewData(data);
        AssetNameUtils.GetDisplayNameFast(workspace, this, true, out string? assetName, out string _);
        AssetName = assetName;
        Update(nameof(DisplayName));
        Update(nameof(ByteSizeModified));
        Update(nameof(ModifiedString));
        workspace.Dirty(workspace.ItemLookup[FileInstance.name]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
