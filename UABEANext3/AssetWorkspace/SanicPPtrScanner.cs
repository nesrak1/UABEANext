using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UABEANext3.AssetWorkspace;

namespace UABEAvalonia
{
    public class SanicPPtrScanner
    {
        private Workspace workspace;

        public List<string> fileList; // abuse AssetPPtr file id here
        public Dictionary<AssetPPtr, List<AssetPPtr>> refLookup;

        public SanicPPtrScanner(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public void ScanFile(AssetsFileInstance fileInst)
        {
            fileList = new List<string>();
            refLookup = new Dictionary<AssetPPtr, List<AssetPPtr>>();

            List<int> fileIdToFileListIndex = new List<int>();
            List<AssetsFileExternal> externals = fileInst.file.Metadata.Externals;
            fileIdToFileListIndex.Add(AddFileListItem(fileInst.name));
            for (int i = 0; i < externals.Count; i++)
            {
                fileIdToFileListIndex.Add(AddFileListItem(externals[i].PathName));
            }

            int len = fileInst.file.AssetInfos.Count;
            for (int i = 0; i < len; i++)
            {
                var assetInfo = fileInst.file.AssetInfos[i];
                ScanAsset(fileInst, assetInfo, fileIdToFileListIndex);
            }
        }

        public void ScanAsset(AssetsFileInstance fileInst, AssetFileInfo assetInfo, List<int> fileIdToFileListIndex)
        {
            AssetInst? cont = workspace.GetAssetInst(fileInst, 0, assetInfo.PathId);
            if (cont == null || cont.FileReader == null)
                return;

            AssetTypeTemplateField? tempBase = workspace.GetTemplateField(cont);
            if (tempBase == null)
                return;

            cont.FileReader.Position = cont.AbsoluteByteStart;

            var thisAssetPtr = new AssetPPtr(fileIdToFileListIndex[0], assetInfo.PathId);

            try
            {
                int childCount = tempBase.Children.Count;
                for (int i = 0; i < childCount; i++)
                {
                    var progress = (float)i / childCount;
                    ScanAssetRecursive(cont.FileReader, thisAssetPtr, fileIdToFileListIndex, tempBase.Children[i]);
                }
            }
            catch { }
        }

        // no error checking, just fail
        private void ScanAssetRecursive(AssetsFileReader reader, AssetPPtr thisAssetPtr, List<int> fileIdToFileListIndex, AssetTypeTemplateField tempField)
        {
            if (tempField.IsArray)
            {
                int itemCount = reader.ReadInt32();

                AssetValueType sizeType = tempField.Children[1].ValueType;
                int byteSize = ValueTypeToSize(sizeType);
                if (byteSize != -1)
                {
                    reader.Position += itemCount * byteSize;
                }
                else if (sizeType == AssetValueType.String)
                {
                    for (int i = 0; i < itemCount; i++)
                    {
                        int stringLength = reader.ReadInt32();
                        reader.Position += stringLength;

                        reader.Align();
                    }
                }
                else
                {
                    for (int i = 0; i < itemCount; i++)
                    {
                        ScanAssetRecursive(reader, thisAssetPtr, fileIdToFileListIndex, tempField.Children[1]);
                    }
                }

                if (tempField.IsAligned)
                {
                    reader.Align();
                }
            }
            else if (tempField.HasValue)
            {
                var valueType = tempField.ValueType;
                if (valueType != AssetValueType.String)
                {
                    int byteSize = ValueTypeToSize(valueType);
                    if (byteSize != -1)
                    {
                        reader.Position += byteSize;
                    }
                    else
                    {
                        throw new Exception("Found non-value type value field");
                    }

                    if (tempField.IsAligned)
                    {
                        reader.Align();
                    }
                }
                else
                {
                    int stringLength = reader.ReadInt32();
                    reader.Position += stringLength;
                    reader.Align();
                }
            }
            else
            {
                if (tempField.Type.StartsWith("PPtr<") && tempField.Children.Count == 2)
                {
                    int fileId = reader.ReadInt32();
                    long pathId = reader.ReadInt64();
                    int fileListIndex = fileIdToFileListIndex[fileId];
                    AssetPPtr thatAssetPtr = new AssetPPtr(fileListIndex, pathId);
                    AddRefLookupItem(thatAssetPtr, thisAssetPtr);
                }
                else
                {
                    int childCount = tempField.Children.Count;
                    for (int i = 0; i < childCount; i++)
                    {
                        ScanAssetRecursive(reader, thisAssetPtr, fileIdToFileListIndex, tempField.Children[i]);
                    }
                }
            }
        }

        public void Save(AssetsFileWriter writer)
        {
            writer.WriteRawString("PPtrScan");
            writer.Write(1); // version

            writer.Write(fileList.Count);
            for (int i = 0; i < fileList.Count; i++)
            {
                writer.Write(fileList[i]);
            }

            List<AssetPPtr> keys = refLookup.Keys.ToList();
            writer.Write(keys.Count);
            int keyCount = keys.Count;
            long offset = 0;
            for (int i = 0; i < keyCount; i++)
            {
                AssetPPtr key = keys[i];
                writer.Write(key.FileId);
                writer.Write(key.PathId);
                writer.Write(offset);
                offset += 4 + refLookup[key].Count * 12;
            }

            for (int i = 0; i < keyCount; i++)
            {
                List<AssetPPtr> refs = refLookup[keys[i]];
                int refCount = refs.Count;
                writer.Write(refCount);
                for (int j = 0; j < refCount; j++)
                {
                    AssetPPtr value = refs[j];
                    writer.Write(value.FileId);
                    writer.Write(value.PathId);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddFileListItem(string name)
        {
            name = Path.GetFileName(name).ToLower();
            if (fileList.Contains(name))
            {
                return fileList.IndexOf(name);
            }
            else
            {
                fileList.Add(name);
                return fileList.Count - 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddRefLookupItem(AssetPPtr from, AssetPPtr to)
        {
            if (refLookup.TryGetValue(from, out List<AssetPPtr>? tos))
            {
                tos.Add(to);
            }
            else
            {
                refLookup[from] = new List<AssetPPtr>()
                {
                    to
                };
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ValueTypeToSize(AssetValueType type)
        {
            return type switch
            {
                AssetValueType.Bool => 1,
                AssetValueType.Int8 => 1,
                AssetValueType.UInt8 => 1,
                AssetValueType.Int16 => 2,
                AssetValueType.UInt16 => 2,
                AssetValueType.Int32 => 4,
                AssetValueType.UInt32 => 4,
                AssetValueType.Int64 => 8,
                AssetValueType.UInt64 => 8,
                AssetValueType.Float => 4,
                AssetValueType.Double => 8,
                _ => -1
            };
        }
    }
}
