using System.Globalization;
using AssetsTools.NET.Extra;

namespace UABEANext3.Models.AssetInfo;

public class GeneralInfo
{
    public string MetadataSize { get; set; }
    
    public string FileSize { get; set; }
    
    public string Format { get; set; }

    public string FirstFileOffset { get; set; }
    
    public string Endianness { get; set; }
    
    public string EngineVersion { get; set; }
    
    public string Platform { get; set; }
    
    public string TypeTreeEnabled { get; set; }
    
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
}
