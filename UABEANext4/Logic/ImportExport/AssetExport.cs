using AssetsTools.NET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using static System.FormattableString;

namespace UABEANext4.Logic.ImportExport;
public class AssetExport
{
    private readonly Stream _stream;
    private readonly StreamWriter _streamWriter;

    public AssetExport(Stream writeStream)
    {
        _stream = writeStream;
        _streamWriter = new StreamWriter(_stream);
    }

    public void DumpRawAsset(AssetsFileReader reader, long position, uint size)
    {
        var assetFs = reader.BaseStream;
        assetFs.Position = position;

        var buf = new byte[4096];
        var bytesLeft = (int)size;
        while (bytesLeft > 0)
        {
            var readSize = assetFs.Read(buf, 0, Math.Min(bytesLeft, buf.Length));
            _stream.Write(buf, 0, readSize);
            bytesLeft -= readSize;
        }
    }

    public void DumpTextAsset(AssetTypeValueField baseField)
    {
        RecurseTextDump(baseField, 0);
        _streamWriter.Flush();
    }

    private void RecurseTextDump(AssetTypeValueField field, int depth)
    {
        var template = field.TemplateField;
        var align = template.IsAligned ? "1" : "0";
        var typeName = template.Type;
        var fieldName = template.Name;
        var isArray = template.IsArray;

        // string's field isn't aligned but its array is
        if (template.ValueType == AssetValueType.String)
            align = "1";

        if (isArray)
        {
            var sizeTemplate = template.Children[0];
            var sizeAlign = sizeTemplate.IsAligned ? "1" : "0";
            var sizeTypeName = sizeTemplate.Type;
            var sizeFieldName = sizeTemplate.Name;

            if (template.ValueType != AssetValueType.ByteArray)
            {
                var size = field.AsArray.size;
                _streamWriter.WriteLine(Invariant($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)"));
                _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}"));
                for (int i = 0; i < field.Children.Count; i++)
                {
                    _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}[{i}]"));
                    RecurseTextDump(field.Children[i], depth + 2);
                }
            }
            else
            {
                var data = field.AsByteArray;
                var size = data.Length;

                _streamWriter.WriteLine(Invariant($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)"));
                _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}"));
                for (int i = 0; i < size; i++)
                {
                    _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}[{i}]"));
                    _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 2)}0 UInt8 data = {data[i]}"));
                }
            }
        }
        else
        {
            var value = "";
            if (field.Value != null)
            {
                AssetValueType evt = field.Value.ValueType;
                if (evt == AssetValueType.String)
                {
                    var fixedStr = TextDumpEscapeString(field.AsString);
                    value = $" = \"{fixedStr}\"";
                }
                else if (1 <= (int)evt && (int)evt <= 12)
                {
                    value = Invariant($" = {field.AsString}");
                }
            }
            _streamWriter.WriteLine(Invariant($"{new string(' ', depth)}{align} {typeName} {fieldName}{value}"));

            if (field.Value != null && field.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
            {
                TextDumpManagedReferencesRegistry(field, depth);
            }
            else
            {
                foreach (var child in field)
                {
                    RecurseTextDump(child, depth + 1);
                }
            }
        }
    }

    private void TextDumpManagedReferencesRegistry(AssetTypeValueField field, int depth)
    {
        var registry = field.Value.AsManagedReferencesRegistry;

        if (registry.version == 1)
        {
            // we need to include this since text dumps are
            // essentially pretty raw dumps and need to include
            // that info so we know when to stop the list
            var referencesWithTerm = new List<AssetTypeReferencedObject>(registry.references)
            {
                new AssetTypeReferencedObject()
                {
                    rid = 0,
                    type = AssetTypeReference.TERMINUS,
                    data = AssetTypeValueField.DUMMY_FIELD
                }
            };

            _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}0 int version = {registry.version}"));
            for (int i = 0; i < referencesWithTerm.Count; i++)
            {
                var refObj = referencesWithTerm[i];
                var typeRef = refObj.type;
                _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}0 ReferencedObject {i:d8}"));
                _streamWriter.WriteLine($"{new string(' ', depth + 2)}0 ReferencedManagedType type");
                _streamWriter.WriteLine($"{new string(' ', depth + 3)}1 string class = \"{TextDumpEscapeString(typeRef.ClassName)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 3)}1 string ns = \"{TextDumpEscapeString(typeRef.Namespace)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 3)}1 string asm = \"{TextDumpEscapeString(typeRef.AsmName)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 2)}0 ReferencedObjectData data");

                foreach (var child in refObj.data.Children)
                {
                    RecurseTextDump(child, depth + 3);
                }
            }
        }
        else if (registry.version == 2)
        {
            _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 1)}0 int version = {registry.version}"));
            _streamWriter.WriteLine($"{new string(' ', depth + 1)}0 vector RefIds");
            _streamWriter.WriteLine($"{new string(' ', depth + 2)}1 Array Array");
            _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 3)}0 int size = {registry.references.Count}"));
            for (int i = 0; i < registry.references.Count; i++)
            {
                AssetTypeReferencedObject refObj = registry.references[i];
                AssetTypeReference typeRef = refObj.type;
                _streamWriter.WriteLine($"{new string(' ', depth + 3)}0 ReferencedObject data");
                _streamWriter.WriteLine(Invariant($"{new string(' ', depth + 4)}0 SInt64 rid = {refObj.rid}"));
                _streamWriter.WriteLine($"{new string(' ', depth + 4)}0 ReferencedManagedType type");
                _streamWriter.WriteLine($"{new string(' ', depth + 5)}1 string class = \"{TextDumpEscapeString(typeRef.ClassName)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 5)}1 string ns = \"{TextDumpEscapeString(typeRef.Namespace)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 5)}1 string asm = \"{TextDumpEscapeString(typeRef.AsmName)}\"");
                _streamWriter.WriteLine($"{new string(' ', depth + 4)}0 ReferencedObjectData data");

                foreach (AssetTypeValueField child in refObj.data.Children)
                {
                    RecurseTextDump(child, depth + 5);
                }
            }
        }
        else
        {
            throw new NotSupportedException($"Registry version {registry.version} not supported.");
        }
    }

    public void DumpJsonAsset(AssetTypeValueField baseField)
    {
        var jBaseField = RecurseJsonDump(baseField, false);
        _streamWriter.Write(jBaseField.ToString());
        _streamWriter.Flush();
    }

    private JToken RecurseJsonDump(AssetTypeValueField field, bool uabeFlavor)
    {
        var template = field.TemplateField;
        var isArray = template.IsArray;

        if (isArray)
        {
            var jArray = new JArray();
            if (template.ValueType != AssetValueType.ByteArray)
            {
                for (int i = 0; i < field.Children.Count; i++)
                {
                    jArray.Add(RecurseJsonDump(field.Children[i], uabeFlavor));
                }
            }
            else
            {
                var byteArrayData = field.AsByteArray;
                for (int i = 0; i < byteArrayData.Length; i++)
                {
                    jArray.Add(byteArrayData[i]);
                }
            }

            return jArray;
        }
        else
        {
            if (field.Value != null)
            {
                var valueType = field.Value.ValueType;

                if (field.Value.ValueType != AssetValueType.ManagedReferencesRegistry)
                {
                    object value = valueType switch
                    {
                        AssetValueType.Bool => field.AsBool,
                        AssetValueType.Int8 or
                        AssetValueType.Int16 or
                        AssetValueType.Int32 => field.AsInt,
                        AssetValueType.Int64 => field.AsLong,
                        AssetValueType.UInt8 or
                        AssetValueType.UInt16 or
                        AssetValueType.UInt32 => field.AsUInt,
                        AssetValueType.UInt64 => field.AsULong,
                        AssetValueType.String => field.AsString,
                        AssetValueType.Float => field.AsFloat,
                        AssetValueType.Double => field.AsDouble,
                        _ => "invalid value"
                    };

                    return (JValue)JToken.FromObject(value);
                }
                else
                {
                    return JsonDumpManagedReferencesRegistry(field, uabeFlavor);
                }
            }
            else
            {
                var jObject = new JObject();
                foreach (AssetTypeValueField child in field)
                {
                    jObject.Add(child.FieldName, RecurseJsonDump(child, uabeFlavor));
                }

                return jObject;
            }
        }
    }

    private JObject JsonDumpManagedReferencesRegistry(AssetTypeValueField field, bool uabeFlavor = false)
    {
        var registry = field.Value.AsManagedReferencesRegistry;

        if (registry.version >= 1 || registry.version <= 2)
        {
            var jArrayRefs = new JArray();
            foreach (var refObj in registry.references)
            {
                var typeRef = refObj.type;

                var jObjManagedType = new JObject
                {
                    { "class", typeRef.ClassName },
                    { "ns", typeRef.Namespace },
                    { "asm", typeRef.AsmName }
                };

                var jObjData = new JObject();
                foreach (var child in refObj.data)
                {
                    jObjData.Add(child.FieldName, RecurseJsonDump(child, uabeFlavor));
                }

                JObject jObjRefObject;
                if (registry.version == 1)
                {
                    jObjRefObject = new JObject
                    {
                        { "type", jObjManagedType },
                        { "data", jObjData }
                    };
                }
                else
                {
                    jObjRefObject = new JObject
                    {
                        { "rid", refObj.rid },
                        { "type", jObjManagedType },
                        { "data", jObjData }
                    };
                }

                jArrayRefs.Add(jObjRefObject);
            }

            var jObjReferences = new JObject
            {
                { "version", registry.version },
                { "RefIds", jArrayRefs }
            };

            return jObjReferences;
        }
        else
        {
            throw new NotSupportedException($"Registry version {registry.version} not supported.");
        }
    }

    // only replace \ with \\ but not " with \"
    // you just have to find the last "
    private static string TextDumpEscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
