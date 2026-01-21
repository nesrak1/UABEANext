using AssetsTools.NET.Extra;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Logic.Search;

public class SearchResultItem
{
    public AssetInst? Asset { get; init; }

    public string FileName { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public long PathId { get; init; }
    public string Offset { get; init; } = string.Empty;
    public AssetClassID Type { get; init; }

}
