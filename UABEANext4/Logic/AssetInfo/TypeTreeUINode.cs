using AssetsTools.NET;
using Avalonia.Controls.Documents;
using System.Collections.Generic;

namespace UABEANext4.Logic.AssetInfo;

public class TypeTreeUINode
{
    public required TypeTreeNode Node { get; init; }
    public required InlineCollection Display { get; init; }
    public required List<TypeTreeUINode> Children { get; init; }
}
