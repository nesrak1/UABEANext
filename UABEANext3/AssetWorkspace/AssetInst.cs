using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;

namespace UABEANext3.AssetWorkspace
{
    // improvement over AssetExternal to handle dynamic changes
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

        //public string DisplayName { get; set; }
        //public long PathId { get; }
        //public int ClassId { get; }
        //public ushort MonoId { get; }
        //public uint Size { get; }
        //public string Container { get; set; } // should be a list later
        //public AssetsFileInstance FileInstance { get; }
        //public AssetTypeValueField? BaseValueField { get; set; }
        //
        //public long FilePosition { get; }
        //public AssetsFileReader FileReader { get; }
        //public AssetID AssetId
        //{
        //    get => new AssetID(FileInstance.path, PathId);
        //}
        //public string TypeName
        //{
        //    get => ((AssetClassID)ClassId).ToString();
        //}
        //public AssetClassID Type
        //{
        //    get => (AssetClassID)ClassId;
        //}
        //
        //// existing assets
        //public AssetInst(AssetFileInfo info, AssetsFileInstance fileInst, AssetTypeValueField? baseField = null)
        //{
        //    FilePosition = info.AbsoluteByteStart;
        //    FileReader = fileInst.file.Reader;
        //
        //    PathId = info.PathId;
        //    ClassId = info.TypeId;
        //    MonoId = fileInst.file.GetScriptIndex(info);
        //    Size = info.ByteSize;
        //    Container = string.Empty;
        //    FileInstance = fileInst;
        //    BaseValueField = baseField;
        //    DisplayName = TypeName;
        //}
        //
        //// newly created assets
        //public AssetInst(AssetsFileReader fileReader, long assetPosition, long pathId, int classId, ushort monoId, uint size,
        //                      AssetsFileInstance fileInst, AssetTypeValueField? baseField = null)
        //{
        //    FilePosition = assetPosition;
        //    FileReader = fileReader;
        //
        //    PathId = pathId;
        //    ClassId = classId;
        //    MonoId = monoId;
        //    Size = size;
        //    Container = string.Empty;
        //    FileInstance = fileInst;
        //    BaseValueField = baseField;
        //    DisplayName = TypeName;
        //}
        //
        //// modified assets
        //public AssetInst(AssetInst container, AssetsFileReader fileReader, long assetPosition, uint size)
        //{
        //    FilePosition = assetPosition;
        //    FileReader = fileReader;
        //
        //    PathId = container.PathId;
        //    ClassId = container.ClassId;
        //    MonoId = container.MonoId;
        //    Size = size;
        //    Container = string.Empty;
        //    FileInstance = container.FileInstance;
        //    BaseValueField = container.BaseValueField;
        //    DisplayName = TypeName;
        //}
        //
        //public AssetInst(AssetInst container, AssetTypeValueField baseField)
        //{
        //    FilePosition = container.FilePosition;
        //    FileReader = container.FileReader;
        //
        //    PathId = container.PathId;
        //    ClassId = container.ClassId;
        //    MonoId = container.MonoId;
        //    Size = container.Size;
        //    Container = string.Empty;
        //    FileInstance = container.FileInstance;
        //    BaseValueField = baseField;
        //    DisplayName = TypeName;
        //}
    }
}
