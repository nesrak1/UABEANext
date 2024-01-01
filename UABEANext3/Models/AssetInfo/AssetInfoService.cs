using AssetsTools.NET.Extra;

namespace UABEANext3.Models.AssetInfo;

public class AssetInfoService
{
   public GeneralInfo GetGeneralInfo(AssetsFileInstance file) => new GeneralInfo(file);
}
