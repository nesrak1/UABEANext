using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace UABEANext3.AssetWorkspace
{
    // assetfileinfo wrapper for extra info
    public class AssetInst : AssetFileInfo, INotifyPropertyChanged
    {
        public string DisplayName { get; set; }
        public string? DisplayContainer { get; set; }
        public AssetsFileInstance FileInstance { get; }
        public AssetTypeValueField? BaseValueField { get; set; }

        [MemberNotNullWhen(true, nameof(BaseValueField))]
        public bool HasValueField => BaseValueField != null;
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

            DisplayName = "Unnamed asset";
            FileInstance = parentFile;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
