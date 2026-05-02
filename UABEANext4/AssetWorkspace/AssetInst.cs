using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.ComponentModel;
using UABEANext4.Logic.Configuration;
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
    public string DisplayName => AssetNamer.GetFallbackName(this, AssetName);
    public string BundleName => FileInstance.parentBundle != null ? FileInstance.parentBundle.name : "";

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
        
        var maxNameLen = ConfigurationManager.Settings.ListingNameLength;
        AssetName = workspace.Namer.GetAssetName(this, true, maxNameLen);
        
        Update(nameof(DisplayName));
        Update(nameof(ByteSizeModified));
        Update(nameof(ModifiedString));
        workspace.Dirty(workspace.ItemLookup[FileInstance.name]);
    }

    public void UpdateAssetDataSilent(Workspace workspace, AssetTypeValueField baseField)
    {
        UpdateAssetDataSilent(workspace, baseField.WriteToByteArray());
    }

    /// <summary>
    /// Updates asset data without firing PropertyChanged events or marking dirty.
    /// Used during batch import to avoid 3× UI notifications per asset (which freezes UI with 26K+ files).
    /// Call RefreshRow() and Workspace.Dirty() separately after all batch updates are complete.
    /// </summary>
    public void UpdateAssetDataSilent(Workspace workspace, byte[] data)
    {
        SetNewData(data);
        
        var maxNameLen = ConfigurationManager.Settings.ListingNameLength;
        AssetName = workspace.Namer.GetAssetName(this, true, maxNameLen);
    }

    /// <summary>
    /// Fires PropertyChanged for display properties. Call once after batch updates.
    /// </summary>
    public void RefreshRow()
    {
        Update(nameof(DisplayName));
        Update(nameof(ByteSizeModified));
        Update(nameof(ModifiedString));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
