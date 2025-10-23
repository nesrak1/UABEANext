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
        // allow negative monobehaviours (old style) but not positive non-monobehaviours
        if (asset.Type != AssetClassID.MonoBehaviour && asset.TypeId >= 0)
            return string.Empty;

        try
        {
            // get script index but skip any monobehaviours with index 0xffff (not sure why these happen)
            var scriptIdx = asset.GetScriptIndex(asset.FileInstance.file);
            if (scriptIdx == ushort.MaxValue)
                return "MonoBehaviour";

            var scriptPtr = asset.FileInstance.file.Metadata.ScriptTypes[scriptIdx];
            var fileInst = asset.FileInstance;

            if (scriptPtr.FileId != 0)
                fileInst = fileInst.GetDependency(workspace.Manager, scriptPtr.FileId - 1);
            if (fileInst == null)
                return "MonoBehaviour";

            AssetFileInfo? info = fileInst.file.GetAssetInfo(scriptPtr.PathId);
            if (info == null)
                return "MonoBehaviour";

            lock (fileInst.LockReader)
            {
                var reader = fileInst.file.Reader;
                reader.Position = info.GetAbsoluteByteOffset(fileInst.file);
                return reader.ReadCountStringInt32();
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    // get script info from global assetpptr rather than script index
    public static AssetTypeReference? GetAssetsFileScriptInfo(AssetsManager am, AssetPPtr assetPtr)
    {
        if (string.IsNullOrEmpty(assetPtr.FilePath))
            return null;

        AssetTypeValueField msBaseField;
        try
        {
            var fileInst = am.FileLookup[AssetsManager.GetFileLookupKey(assetPtr.FilePath)];
            msBaseField = am.GetExtAsset(fileInst, 0, assetPtr.PathId).baseField;
            if (msBaseField == null)
                return null;
        }
        catch
        {
            return null;
        }

        AssetTypeValueField assemblyNameField = msBaseField["m_AssemblyName"];
        AssetTypeValueField nameSpaceField = msBaseField["m_Namespace"];
        AssetTypeValueField classNameField = msBaseField["m_ClassName"];
        if (assemblyNameField.IsDummy || nameSpaceField.IsDummy || classNameField.IsDummy)
            return null;

        string assemblyName = assemblyNameField.AsString;
        string nameSpace = nameSpaceField.AsString;
        string className = classNameField.AsString;

        AssetTypeReference info = new AssetTypeReference(className, nameSpace, assemblyName);
        return info;
    }
}
