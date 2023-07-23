using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.Util
{
    public static class AssetNameUtils
    {
        // codeflow needs work but should be fine for now
        public static void GetDisplayNameFast(Workspace workspace, AssetInst asset, bool usePrefix, out string assetName, out string typeName)
        {
            assetName = "Unnamed asset";
            typeName = "Unknown type";

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
                        typeName = ttType.Nodes[0].GetTypeString(ttType.StringBuffer);
                        if (ttType.Nodes.Count > 1 && ttType.Nodes[1].GetNameString(ttType.StringBuffer) == "m_Name")
                        {
                            reader.Position = filePosition;
                            assetName = reader.ReadCountStringInt32();
                            if (assetName == "")
                                assetName = $"{typeName} #{pathId}";

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
                                    assetName = $"{typeName} #{pathId}";
                            }
                            return;
                        }
                        assetName = $"{typeName} #{pathId}";
                        return;
                    }
                }

                ClassDatabaseType? type = cldb?.FindAssetClassByID(classId);

                if (type == null || cldb == null)
                {
                    typeName = $"0x{classId:X8}";
                    assetName = $"Unnamed asset #{pathId}";
                    return;
                }

                typeName = cldb.GetString(type.Name);
                List<ClassDatabaseTypeNode> cldbNodes = type.GetPreferredNode(false).Children;

                if (cldbNodes.Count == 0)
                {
                    assetName = $"{typeName} #{pathId}";
                    return;
                }

                if (cldbNodes.Count > 1 && cldb.GetString(cldbNodes[0].FieldName) == "m_Name")
                {
                    reader.Position = filePosition;
                    assetName = reader.ReadCountStringInt32();
                    if (assetName == "")
                        assetName = $"{typeName} #{pathId}";
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
                            assetName = $"{typeName} #{pathId}";
                    }
                    return;
                }
                assetName = $"{typeName} #{pathId}";
            }
            catch
            {
            }
        }

        // not very fast but w/e at least it's stable
        public static string GetMonoBehaviourNameFast(Workspace workspace, AssetInst asset)
        {
            try
            {
                if (asset.Type != AssetClassID.MonoBehaviour && asset.TypeId >= 0)
                    return string.Empty;

                AssetTypeValueField monoBf;
                if (asset.HasValueField)
                {
                    monoBf = asset.BaseValueField;
                }
                else
                {
                    // hasTypeTree is set to false to ignore type tree (to prevent
                    // reading the entire MonoBehaviour if type trees are provided)

                    // it might be a better idea to just temporarily remove the extra
                    // fields from a single MonoBehaviour so we don't have to read
                    // from the cldb (especially so for stripped versions of bundles)

                    bool wasUsingCache = workspace.Manager.UseTemplateFieldCache;
                    workspace.Manager.UseTemplateFieldCache = false;
                    AssetTypeTemplateField monoTemp = workspace.GetTemplateField(asset, true);
                    workspace.Manager.UseTemplateFieldCache = wasUsingCache;

                    monoBf = monoTemp.MakeValue(asset.FileReader, asset.AbsoluteByteStart);
                }

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
}
