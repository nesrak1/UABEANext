using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace UABEANext4.Logic.AssetInfo;

public class TypeTreeTypeInfo
{
    public TypeTreeType TtType { get; }

    public string Name { get; set; }
    public string ScriptName { get; set; }
    public int TypeId => TtType.TypeId;
    public uint ScriptId { get; set; }
    public bool IsRef => TtType.IsRefType;
    public string TypeHash => !TtType.TypeHash.IsZero() ? TtType.TypeHash.ToString() : string.Empty;
    public string MonoHash => !TtType.ScriptIdHash.IsZero() ? TtType.ScriptIdHash.ToString() : string.Empty;

    public TypeTreeTypeInfo(AssetsManager manager, AssetsFileInstance fileInst, TypeTreeType ttType)
    {
        TtType = ttType;

        Name = GetTypeName(manager, fileInst, ttType) ?? "UNKNOWN";
        if (GetScriptIndexAndName(manager, fileInst, ttType, out ushort index, out string? name))
        {
            ScriptName = name;
            ScriptId = index;
        }
        else
        {
            ScriptName = "UNKNOWN";
            ScriptId = ushort.MaxValue;
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Name);
        if (ScriptName != string.Empty)
        {
            sb.Append(' ');
            sb.Append(ScriptName);
        }

        if (ScriptId != ushort.MaxValue)
        {
            sb.Append(" (0x");
            sb.Append(TypeId.ToString("x"));
            sb.Append('/');
            sb.Append(ScriptId.ToString("d4"));
            sb.Append(')');
        }
        else
        {
            sb.Append(" (0x");
            sb.Append(TypeId.ToString("x"));
            sb.Append(')');
        }

        if (IsRef)
        {
            sb.Append(" REF");
        }

        return sb.ToString();
    }

    private static string? GetTypeName(AssetsManager manager, AssetsFileInstance fileInst, TypeTreeType ttType)
    {
        var cldb = manager.ClassDatabase;
        var metadata = fileInst.file.Metadata;
        if (!metadata.TypeTreeEnabled && cldb != null)
        {
            // use class database
            var cldbType = cldb.FindAssetClassByID(ttType.TypeId);
            if (cldbType == null)
            {
                return null;
            }

            return cldb.GetString(cldbType.Name);
        }
        else
        {
            // use type tree and read the first field, if it exists
            var ttNodes = ttType.Nodes;
            if (ttNodes.Count == 0)
            {
                return null;
            }

            var baseField = ttType.Nodes[0];
            return baseField.GetTypeString(ttType.StringBufferBytes);
        }
    }

    private static bool GetScriptIndexAndName(
        AssetsManager manager, AssetsFileInstance fileInst, TypeTreeType ttType,
        [MaybeNullWhen(false)] out ushort index,
        [MaybeNullWhen(false)] out string name)
    {
        index = ushort.MaxValue;
        name = null;

        if (ttType.ScriptTypeIndex != 0xffff)
        {
            var scriptInfo = AssetHelper.GetAssetsFileScriptInfo(manager, fileInst, ttType.ScriptTypeIndex);
            var scriptName = scriptInfo?.ClassName;
            if (scriptName == null)
            {
                // null because there is a name but we don't know what it is
                return false;
            }

            index = ttType.ScriptTypeIndex;
            name = scriptName;
            return true;
        }
        else
        {
            // empty because there isn't one and it looks better blank
            name = string.Empty;
            return true;
        }
    }
}
