using AssetsTools.NET.Extra;
using System.Diagnostics.CodeAnalysis;

namespace UABEANext4.Logic.AssetInfo;

public class GeneralInfo
{
    public required string MetadataSize { get; set; }
    public required string FileSize { get; set; }
    public required string Format { get; set; }
    public required string FirstFileOffset { get; set; }
    public required string Endianness { get; set; }
    public required string EngineVersion { get; set; }
    public required string Platform { get; set; }
    public required string TypeTreeEnabled { get; set; }

    [SetsRequiredMembers]
    public GeneralInfo(AssetsFileInstance file)
    {
        var header = file.file.Header;
        var metadata = file.file.Metadata;

        MetadataSize = header.MetadataSize.ToString();
        FileSize = header.FileSize.ToString();
        Format = header.Version.ToString();
        FirstFileOffset = header.DataOffset.ToString();
        Endianness = header.Endianness ? "Big endian" : "Little endian";

        EngineVersion = metadata.UnityVersion;
        Platform = $"{(BuildTarget)metadata.TargetPlatform} ({metadata.TargetPlatform})";
        TypeTreeEnabled = metadata.TypeTreeEnabled ? "Enabled" : "Disabled";
    }

    private GeneralInfo()
    {
    }

    public static GeneralInfo Empty { get; } = new()
    {
        MetadataSize = string.Empty,
        FileSize = string.Empty,
        Format = string.Empty,
        FirstFileOffset = string.Empty,
        Endianness = string.Empty,

        EngineVersion = string.Empty,
        Platform = string.Empty,
        TypeTreeEnabled = string.Empty,
    };
}
