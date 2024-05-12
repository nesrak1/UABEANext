using AssetsTools.NET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UABEANext4.Logic.ImportExport;
public class AssetImport
{
    private readonly Stream _stream;
    private readonly StreamReader _streamReader;

    public AssetImport(Stream readStream)
    {
        _stream = readStream;
        _streamReader = new StreamReader(_stream);
    }

    public byte[] ImportRawAsset()
    {
        using var ms = new MemoryStream();
        _stream.CopyTo(ms);
        return ms.ToArray();
    }

    public byte[]? ImportTextAsset(out string? exceptionMessage)
    {
        using var ms = new MemoryStream();
        var writer = new AssetsFileWriter(ms)
        {
            BigEndian = false
        };

        try
        {
            ImportTextAssetLoop(writer);
            exceptionMessage = null;
        }
        catch (Exception ex)
        {
            exceptionMessage = ex.Message;
            return null;
        }
        return ms.ToArray();
    }

    private void ImportTextAssetLoop(AssetsFileWriter writer)
    {
        Stack<bool> alignStack = new Stack<bool>();
        while (true)
        {
            string? line = _streamReader.ReadLine();
            if (line == null)
                return;

            int thisDepth = 0;
            while (line[thisDepth] == ' ')
                thisDepth++;

            if (line[thisDepth] == '[') // array index, ignore
                continue;

            if (thisDepth < alignStack.Count)
            {
                while (thisDepth < alignStack.Count)
                {
                    if (alignStack.Pop())
                        writer.Align();
                }
            }

            bool align = line.Substring(thisDepth, 1) == "1";
            int typeName = thisDepth + 2;
            int eqSign = line.IndexOf('=');
            string valueStr = line.Substring(eqSign + 1).Trim();

            if (eqSign != -1)
            {
                string check = line.Substring(typeName);

                // sorted by frequency
                if (StartsWithSpace(check, "int"))
                {
                    writer.Write(int.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "float"))
                {
                    writer.Write(float.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "bool"))
                {
                    writer.Write(bool.Parse(valueStr));
                }
                else if (StartsWithSpace(check, "SInt64"))
                {
                    writer.Write(long.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "string"))
                {
                    int firstQuote = valueStr.IndexOf('"');
                    int lastQuote = valueStr.LastIndexOf('"');
                    string valueStrFix = valueStr.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                    valueStrFix = UnescapeDumpString(valueStrFix);
                    writer.WriteCountStringInt32(valueStrFix);
                }
                else if (StartsWithSpace(check, "UInt8"))
                {
                    writer.Write(byte.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "unsigned int"))
                {
                    writer.Write(uint.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "UInt16"))
                {
                    writer.Write(ushort.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "SInt8"))
                {
                    writer.Write(sbyte.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "SInt16"))
                {
                    writer.Write(short.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "UInt64"))
                {
                    writer.Write(ulong.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "double"))
                {
                    writer.Write(double.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "char"))
                {
                    writer.Write(sbyte.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "FileSize"))
                {
                    writer.Write(ulong.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                // not seen in the wild? but still part of at
                // I'm not sure where this list is from
                else if (StartsWithSpace(check, "short"))
                {
                    writer.Write(short.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "long"))
                {
                    writer.Write(long.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "SInt32"))
                {
                    writer.Write(int.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "UInt32"))
                {
                    writer.Write(uint.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "unsigned char"))
                {
                    writer.Write(byte.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "unsigned short"))
                {
                    writer.Write(ushort.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }
                else if (StartsWithSpace(check, "unsigned long long"))
                {
                    writer.Write(ulong.Parse(valueStr, NumberStyles.None, CultureInfo.InvariantCulture));
                }

                if (align)
                {
                    writer.Align();
                }
            }
            else
            {
                alignStack.Push(align);
            }
        }
    }

    public byte[]? ImportJsonAsset(AssetTypeTemplateField tempField, out string? exceptionMessage)
    {
        using var ms = new MemoryStream();
        var writer = new AssetsFileWriter(ms)
        {
            BigEndian = false
        };

        try
        {
            string jsonText = _streamReader.ReadToEnd();
            JToken token = JToken.Parse(jsonText);

            RecurseJsonImport(writer, tempField, token);
            exceptionMessage = null;
        }
        catch (Exception ex)
        {
            exceptionMessage = ex.Message;
            return null;
        }
        return ms.ToArray();
    }

    private void RecurseJsonImport(AssetsFileWriter writer, AssetTypeTemplateField tempField, JToken token)
    {
        bool align = tempField.IsAligned;

        if (tempField.Children.Count == 1 && tempField.Children[0].IsArray &&
            token.Type == JTokenType.Array)
        {
            RecurseJsonImport(writer, tempField.Children[0], token);
            return;
        }

        if (!tempField.HasValue && !tempField.IsArray)
        {
            foreach (AssetTypeTemplateField childTempField in tempField.Children)
            {
                JToken? childToken = token[childTempField.Name];

                if (childToken == null)
                {
                    if (tempField != null)
                    {
                        throw new Exception($"Missing field {childTempField.Name} in JSON. Parent field is {tempField.Type} {tempField.Name}.");
                    }
                    else
                    {
                        throw new Exception($"Missing field {childTempField.Name} in JSON.");
                    }
                }

                RecurseJsonImport(writer, childTempField, childToken);
            }

            if (align)
            {
                writer.Align();
            }
        }
        else if (tempField.HasValue && tempField.ValueType == AssetValueType.ManagedReferencesRegistry)
        {
            throw new NotImplementedException("SerializeReference not supported in JSON import yet!");
        }
        else
        {
            switch (tempField.ValueType)
            {
                case AssetValueType.Bool:
                {
                    writer.Write((bool)token);
                    break;
                }
                case AssetValueType.UInt8:
                {
                    writer.Write((byte)token);
                    break;
                }
                case AssetValueType.Int8:
                {
                    writer.Write((sbyte)token);
                    break;
                }
                case AssetValueType.UInt16:
                {
                    writer.Write((ushort)token);
                    break;
                }
                case AssetValueType.Int16:
                {
                    writer.Write((short)token);
                    break;
                }
                case AssetValueType.UInt32:
                {
                    writer.Write((uint)token);
                    break;
                }
                case AssetValueType.Int32:
                {
                    writer.Write((int)token);
                    break;
                }
                case AssetValueType.UInt64:
                {
                    writer.Write((ulong)token);
                    break;
                }
                case AssetValueType.Int64:
                {
                    writer.Write((long)token);
                    break;
                }
                case AssetValueType.Float:
                {
                    writer.Write((float)token);
                    break;
                }
                case AssetValueType.Double:
                {
                    writer.Write((double)token);
                    break;
                }
                case AssetValueType.String:
                {
                    align = true;
                    writer.WriteCountStringInt32((string?)token ?? "");
                    break;
                }
                case AssetValueType.ByteArray:
                {
                    JArray byteArrayJArray = ((JArray?)token) ?? new JArray();
                    byte[] byteArrayData = new byte[byteArrayJArray.Count];
                    for (int i = 0; i < byteArrayJArray.Count; i++)
                    {
                        byteArrayData[i] = (byte)byteArrayJArray[i];
                    }
                    writer.Write(byteArrayData.Length);
                    writer.Write(byteArrayData);
                    break;
                }
            }

            // have to do this because of bug in MonoDeserializer
            if (tempField.IsArray && tempField.ValueType != AssetValueType.ByteArray)
            {
                // children[0] is size field, children[1] is the data field
                AssetTypeTemplateField childTempField = tempField.Children[1];

                JArray? tokenArray = (JArray?)token;

                if (tokenArray == null)
                    throw new Exception($"Field {tempField.Name} was not an array in json.");

                writer.Write(tokenArray.Count);
                foreach (JToken childToken in tokenArray.Children())
                {
                    RecurseJsonImport(writer, childTempField, childToken);
                }
            }

            if (align)
            {
                writer.Align();
            }
        }
    }

    private static bool StartsWithSpace(string str, string value)
    {
        return str.StartsWith(value + " ");
    }

    private static string UnescapeDumpString(string str)
    {
        StringBuilder sb = new StringBuilder(str.Length);
        bool escaping = false;
        foreach (char c in str)
        {
            if (!escaping && c == '\\')
            {
                escaping = true;
                continue;
            }

            if (escaping)
            {
                if (c == '\\')
                    sb.Append('\\');
                else if (c == 'r')
                    sb.Append('\r');
                else if (c == 'n')
                    sb.Append('\n');
                else
                    sb.Append(c);

                escaping = false;
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
