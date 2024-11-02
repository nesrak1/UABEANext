using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Util;

public static class AssetNameUtils
{
    // codeflow needs work but should be fine for now
    public static void GetDisplayNameFast(Workspace workspace, AssetInst asset, bool usePrefix, out string? assetName, out string typeName)
    {
        assetName = "Error reading name";
        typeName = "Error reading type";

        try
        {
            ClassDatabaseFile? cldb = workspace.Manager.ClassDatabase;
            AssetsFile file = asset.FileInstance.file;
            AssetsFileReader reader = asset.FileReader;
            long filePosition = asset.AbsoluteByteStart;
            int classId = asset.TypeId;
            ushort monoId = asset.ScriptTypeIndex;
            long pathId = asset.PathId;

            if (file.Metadata.TypeTreeEnabled)
            {
                TypeTreeType ttType;
                if (classId == 0x72 || classId < 0)
                    ttType = file.Metadata.FindTypeTreeTypeByScriptIndex(monoId);
                else
                    ttType = file.Metadata.FindTypeTreeTypeByID(classId);

                if (ttType != null && ttType.Nodes.Count > 0)
                {
                    typeName = ttType.Nodes[0].GetTypeString(ttType.StringBufferBytes);
                    if (ttType.Nodes.Count > 1 && ttType.Nodes[1].GetNameString(ttType.StringBufferBytes) == "m_Name")
                    {
                        reader.Position = filePosition;
                        assetName = reader.ReadCountStringInt32();
                        if (assetName == "")
                            assetName = null;

                        return;
                    }
                    else if (typeName == "GameObject")
                    {
                        reader.Position = filePosition;
                        int size = reader.ReadInt32();
                        int componentSize = file.Header.Version > 0x10 ? 0x0c : 0x10;
                        reader.Position += size * componentSize;
                        reader.Position += 0x04;
                        assetName = reader.ReadCountStringInt32();
                        if (usePrefix)
                            assetName = $"GameObject {assetName}";

                        return;
                    }
                    else if (typeName == "MonoBehaviour")
                    {
                        reader.Position = filePosition;
                        reader.Position += 0x1c;
                        assetName = reader.ReadCountStringInt32();
                        if (assetName == "")
                        {
                            assetName = GetMonoBehaviourNameFast(workspace, asset);
                            if (assetName == "")
                                assetName = null;
                        }
                        return;
                    }
                    assetName = null;
                    return;
                }
            }

            ClassDatabaseType? type = cldb?.FindAssetClassByID(classId);

            if (type == null || cldb == null)
            {
                typeName = $"0x{classId:X8}";
                assetName = null;
                return;
            }

            typeName = cldb.GetString(type.Name);
            List<ClassDatabaseTypeNode> cldbNodes = type.GetPreferredNode(false).Children;

            if (cldbNodes.Count == 0)
            {
                assetName = null;
                return;
            }

            if (cldbNodes.Count > 1 && cldb.GetString(cldbNodes[0].FieldName) == "m_Name")
            {
                reader.Position = filePosition;
                assetName = reader.ReadCountStringInt32();
                if (assetName == "")
                    assetName = null;

                return;
            }
            else if (typeName == "GameObject")
            {
                reader.Position = filePosition;
                int size = reader.ReadInt32();
                int componentSize = file.Header.Version > 0x10 ? 0x0c : 0x10;
                reader.Position += size * componentSize;
                reader.Position += 0x04;
                assetName = reader.ReadCountStringInt32();
                if (usePrefix)
                    assetName = $"GameObject {assetName}";

                return;
            }
            else if (typeName == "MonoBehaviour")
            {
                reader.Position = filePosition;
                reader.Position += 0x1c;
                assetName = reader.ReadCountStringInt32();
                if (assetName == "")
                {
                    assetName = GetMonoBehaviourNameFast(workspace, asset);
                    if (assetName == "")
                        assetName = null;
                }
                return;
            }
            assetName = null;
        }
        catch
        {
        }
    }

    public static string GetFallbackName(AssetInst asset, string? name)
    {
        return name ?? $"{asset.Type} #{asset.PathId}";
    }

    public static string GetAssetFileName(Workspace workspace, AssetInst asset, string ext)
    {
        GetDisplayNameFast(workspace, asset, false, out string? assetName, out string _);
        assetName = GetFallbackName(asset, assetName);
        assetName = PathUtils.ReplaceInvalidPathChars(assetName);
        return $"{assetName}-{Path.GetFileName(asset.FileInstance.path)}-{asset.PathId}{ext}";
    }

    public static string GetAssetFileName(AssetInst asset, string assetNameOverride, string ext)
    {
        string assetName = PathUtils.ReplaceInvalidPathChars(assetNameOverride);
        return $"{assetName}-{Path.GetFileName(asset.FileInstance.path)}-{asset.PathId}{ext}";
    }

    // not very fast but w/e at least it's stable
    public static string GetMonoBehaviourNameFast(Workspace workspace, AssetInst asset)
    {
        try
        {
            if (asset.Type != AssetClassID.MonoBehaviour && asset.TypeId >= 0)
                return string.Empty;

            // hasTypeTree is set to false to ignore type tree (to prevent
            // reading the entire MonoBehaviour if type trees are provided)
            // however, this relies on knowing the unity version of the file
            // and having a class database loaded. when this isn't the case,
            // our only option is to extract the entire type tree and trim off
            // everything after the m_Script field so it doesn't load a lot.

            AssetTypeTemplateField monoTemp = workspace.GetTemplateField(asset, true).Clone();
            // trim off extra (needs a speedup. findindex isn't going to be the fastest.)
            int nameIndex = monoTemp.Children.FindIndex(monoTemp => monoTemp.Name == "m_Script");
            if (nameIndex != -1)
            {
                monoTemp.Children.RemoveRange(nameIndex + 1, monoTemp.Children.Count - (nameIndex + 1));
            }

            AssetTypeValueField monoBf = monoTemp.MakeValue(asset.FileReader, asset.AbsoluteByteStart);
            AssetTypeValueField? scriptBaseField = workspace.GetBaseField(asset.FileInstance, monoBf["m_Script"]);
            if (scriptBaseField == null)
            {
                return string.Empty;
            }

            return scriptBaseField["m_ClassName"].AsString;
        }
        catch
        {
            return string.Empty;
        }
    }
}
