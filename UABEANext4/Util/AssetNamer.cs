using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Util;
public class AssetNamer
{
    private readonly Workspace _workspace;

    // cache for optimization
    private ConcurrentDictionary<AssetsFileInstance, NameReadOptimization> _gameObjectNro = [];
    private ConcurrentDictionary<AssetsFileInstance, NameReadOptimization> _monoBehaviourNro = [];

    public AssetNamer(Workspace workspace)
    {
        _workspace = workspace;
    }

    public void GetDisplayName(AssetInst asset, bool usePrefix, out string? assetName, out string typeName)
    {
        assetName = null;

        var fileInst = asset.FileInstance;
        var tempBaseField = _workspace.Manager.GetTemplateBaseField(fileInst, asset);
        if (tempBaseField == null)
        {
            var maybeClassId = (AssetClassID)asset.TypeId;
            typeName = Enum.GetName(maybeClassId)! ?? $"Type ID 0x{asset.TypeId:x}";
        }
        else
        {
            typeName = tempBaseField.Type;
        }

        if (tempBaseField == null || tempBaseField.Children.Count == 0)
        {
            // we'd hope this doesn't happen, but if it does, there's nothing else we can do
            return;
        }

        if (tempBaseField.Children.Count > 0)
        {
            var firstChild = tempBaseField.Children[0];
            if (firstChild.Name == "m_Name" && firstChild.Type == "string" && asset.TypeId != (int)AssetClassID.Shader)
            {
                GetLockObjAndReader(asset, usePrefix, out object lockObj, out AssetsFileReader reader);

                lock (lockObj)
                {
                    assetName = asset.FileInstance.file.Reader.ReadCountStringInt32();
                }
                return;
            }

            NameReadOptimization nro = NameReadOptimization.Unchecked;
            if (asset.TypeId == (int)AssetClassID.GameObject)
            {
                if (!_gameObjectNro.TryGetValue(fileInst, out nro))
                    _gameObjectNro[fileInst] = nro = NameReadOptimization.Unchecked;

                if (nro == NameReadOptimization.Unchecked)
                {
                    _gameObjectNro[fileInst] = nro = GetGameObjectNro(tempBaseField);
                }

                if (nro == NameReadOptimization.UseOptimized)
                {
                    var headerVer = fileInst.file.Header.Version;
                    GetLockObjAndReader(asset, usePrefix, out object lockObj, out AssetsFileReader reader);

                    lock (lockObj)
                    {
                        int size = reader.ReadInt32();
                        int componentSize = headerVer >= 17 ? 0x0c : 0x10;
                        reader.Position += size * componentSize;
                        reader.Position += 4;
                        if (usePrefix)
                            assetName = $"GameObject {reader.ReadCountStringInt32()}";
                        else
                            assetName = reader.ReadCountStringInt32();
                    }
                    return;
                }
            }
            else if (asset.TypeId == (int)AssetClassID.MonoBehaviour)
            {
                if (!_monoBehaviourNro.TryGetValue(fileInst, out nro))
                    _monoBehaviourNro[fileInst] = nro = NameReadOptimization.Unchecked;

                if (nro == NameReadOptimization.Unchecked)
                {
                    _monoBehaviourNro[fileInst] = nro = GetMonoBehaviourNro(tempBaseField);
                }

                if (nro == NameReadOptimization.UseOptimized)
                {
                    var headerVer = fileInst.file.Header.Version;
                    GetLockObjAndReader(asset, usePrefix, out object lockObj, out AssetsFileReader reader);

                    lock (lockObj)
                    {
                        reader.Position += 0x1c;
                        assetName = reader.ReadCountStringInt32();
                        if (assetName == "")
                        {
                            assetName = GetMonoBehaviourNameFast(_workspace, asset);
                            if (assetName == "")
                                assetName = null;
                        }
                    }
                    return;
                }
            }
            else if (asset.TypeId == (int)AssetClassID.Shader)
            {
                GetLockObjAndReader(asset, usePrefix, out object lockObj, out AssetsFileReader reader);

                lock (lockObj)
                {
                    var iterator = new AssetTypeValueIterator(tempBaseField, reader, _workspace.Manager.GetRefTypeManager(fileInst));

                    // skip first name
                    iterator.ReadNext();
                    iterator.ReadNext();

                    while (iterator.ReadNext())
                    {
                        if (iterator.TempField.Name == "m_Name" && iterator.TempField.Type == "string" && iterator.TempFieldStack[1].Name == "m_ParsedForm")
                        {
                            var valueField = iterator.ReadValueField();
                            assetName = valueField.AsString;
                            break;
                        }
                    }
                }
                return;
            }

            if (nro == NameReadOptimization.Unchecked)
            {
                assetName = $"{asset.Type} #{asset.PathId}";
            }
            else if (nro == NameReadOptimization.UseIterator)
            {
                GetLockObjAndReader(asset, usePrefix, out object lockObj, out AssetsFileReader reader);

                lock (lockObj)
                {
                    var iterator = new AssetTypeValueIterator(tempBaseField, reader, _workspace.Manager.GetRefTypeManager(fileInst));
                    while (iterator.ReadNext())
                    {
                        if (iterator.TempField.Name == "m_Name" && iterator.TempField.Type == "string")
                        {
                            var valueField = iterator.ReadValueField();
                            assetName = valueField.AsString;
                            break;
                        }
                    }
                }
            }

            if (assetName == string.Empty)
            {
                assetName = $"{asset.Type} #{asset.PathId}";
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetLockObjAndReader(AssetInst asset, bool usePrefix, out object lockObj, out AssetsFileReader reader)
    {
        var fileInst = asset.FileInstance;
        if (asset.IsReplacerPreviewable)
        {
            var stream = asset.Replacer.GetPreviewStream();
            lockObj = stream;
            reader = new AssetsFileReader(stream);
            reader.Position = 0;
        }
        else
        {
            lockObj = fileInst.LockReader;
            reader = asset.FileInstance.file.Reader;
            reader.Position = asset.AbsoluteByteStart;
        }
    }

    // not very fast but w/e at least it's stable
    private static string GetMonoBehaviourNameFast(Workspace workspace, AssetInst asset)
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

    private static NameReadOptimization GetGameObjectNro(AssetTypeTemplateField tempField)
    {
        var b = tempField.Children;
        if (b.Count < 3)
            return NameReadOptimization.UseIterator;

        var b_m_Component = b[0];
        if (b_m_Component.Type != "vector")
            return NameReadOptimization.UseIterator;

        if (b_m_Component.Children.Count != 1)
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array = b_m_Component[0];
        if (b_m_Component_Array.Type != "Array")
            return NameReadOptimization.UseIterator;

        if (b_m_Component_Array.Children.Count != 2)
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array_size = b_m_Component_Array[0];
        if (b_m_Component_Array_size.Type != "int")
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array_data = b_m_Component_Array[1];
        if (b_m_Component_Array_data.Type != "ComponentPair")
            return NameReadOptimization.UseIterator;

        if (b_m_Component_Array_data.Children.Count != 1)
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array_data_component = b_m_Component_Array_data[0];
        if (b_m_Component_Array_data_component.Type != "PPtr<Component>")
            return NameReadOptimization.UseIterator;

        if (b_m_Component_Array_data_component.Children.Count != 2)
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array_data_component_m_FileID = b_m_Component_Array_data_component[0];
        if (b_m_Component_Array_data_component_m_FileID.Type != "int")
            return NameReadOptimization.UseIterator;

        var b_m_Component_Array_data_component_m_PathID = b_m_Component_Array_data_component[1];
        if (b_m_Component_Array_data_component_m_PathID.Type != "SInt64")
            return NameReadOptimization.UseIterator;

        var b_m_Layer = b[1];
        if (b_m_Layer.Type != "unsigned int")
            return NameReadOptimization.UseIterator;

        var b_m_Name = b[2];
        if (b_m_Name.Type != "string")
            return NameReadOptimization.UseIterator;

        return NameReadOptimization.UseOptimized;
    }

    private static NameReadOptimization GetMonoBehaviourNro(AssetTypeTemplateField tempField)
    {
        var b = tempField.Children;
        if (b.Count < 4)
            return NameReadOptimization.UseIterator;

        var b_m_GameObject = b[0];
        if (b_m_GameObject.Type != "PPtr<GameObject>")
            return NameReadOptimization.UseIterator;

        if (b_m_GameObject.Children.Count != 2)
            return NameReadOptimization.UseIterator;

        var b_m_GameObject_m_FileID = b_m_GameObject[0];
        if (b_m_GameObject_m_FileID.Type != "int")
            return NameReadOptimization.UseIterator;

        var b_m_GameObject_m_PathID = b_m_GameObject[1];
        if (b_m_GameObject_m_PathID.Type != "SInt64")
            return NameReadOptimization.UseIterator;

        var b_m_Enabled = b[1];
        if (b_m_Enabled.Type != "UInt8")
            return NameReadOptimization.UseIterator;

        var b_m_Script = b[2];
        if (b_m_Script.Type != "PPtr<MonoScript>")
            return NameReadOptimization.UseIterator;

        if (b_m_Script.Children.Count != 2)
            return NameReadOptimization.UseIterator;

        var b_m_Script_m_FileID = b_m_Script[0];
        if (b_m_Script_m_FileID.Type != "int")
            return NameReadOptimization.UseIterator;

        var b_m_Script_m_PathID = b_m_Script[1];
        if (b_m_Script_m_PathID.Type != "SInt64")
            return NameReadOptimization.UseIterator;

        var b_m_Name = b[3];
        if (b_m_Name.Type != "string")
            return NameReadOptimization.UseIterator;

        return NameReadOptimization.UseOptimized;
    }

    public enum NameReadOptimization
    {
        Unchecked,
        UseOptimized,
        UseIterator
    }
}
