using AssetsTools.NET;
using AssetsTools.NET.Extra;
using DynamicData;
using DynamicData.Binding;
using System;
using System.Collections.ObjectModel;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Logic.AssetInfo;
public class ExternalInfo
{
    public ObservableCollection<AssetsFileExternal> Externals { get; set; } = [];
    public ReadOnlyObservableCollection<string> ExternalsDisplay { get; init; }

    public ExternalInfo(Workspace workspace, AssetsFileInstance fileInst)
    {
        var externals = fileInst.file.Metadata.Externals;
        foreach (var external in externals)
        {
            Externals.Add(external);
        }

        Func<AssetsFileExternal, int, string> externalsNameTransFac = (dep, idx) =>
        {
            if (dep.PathName != string.Empty)
                return $"{idx} - {dep.PathName}";
            else
                return $"{idx} - {dep.Guid}";
        };

        Externals
            .ToObservableChangeSet()
            .Transform(externalsNameTransFac)
            .Bind(out var externalsItems)
            .DisposeMany()
            .Subscribe();

        ExternalsDisplay = externalsItems!;
    }

    private ExternalInfo()
    {
        ExternalsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>());
    }

    public static ExternalInfo Empty { get; } = new()
    {
        Externals = [],
        ExternalsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>())
    };
}
