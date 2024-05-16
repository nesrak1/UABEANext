using AssetsTools.NET.Extra;

namespace UABEANext4.AssetInfo;

public class AssetInfoService
{
   public GeneralInfo GetGeneralInfo(AssetsFileInstance file) => new GeneralInfo(file);
}
