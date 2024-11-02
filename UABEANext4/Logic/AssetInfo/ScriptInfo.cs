using AssetsTools.NET;
using AssetsTools.NET.Extra;
using DynamicData;
using DynamicData.Binding;
using System;
using System.Collections.ObjectModel;
using System.IO;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Logic.AssetInfo;
public class ScriptInfo
{
    public ObservableCollection<AssetPPtr> Scripts { get; set; } = [];
    public ReadOnlyObservableCollection<string> ScriptsDisplay { get; init; }

    public ScriptInfo(Workspace workspace, AssetsFileInstance fileInst)
    {
        var scripts = fileInst.file.Metadata.ScriptTypes;
        foreach (var script in scripts)
        {
            Scripts.Add(script);
        }

        Func<AssetPPtr, int, string> scriptsNameTransFac = (pptr, idx) =>
        {
            AssetTypeValueField? scriptBf = workspace.GetBaseField(fileInst, pptr.FileId, pptr.PathId);
            if (scriptBf == null)
            {
                if (pptr.FileId == 0)
                {
                    return $"{idx} - {fileInst.name}/{pptr.PathId}";
                }
                else
                {
                    string fileName = fileInst.file.Metadata.Externals[pptr.FileId - 1].PathName;
                    return $"{idx} - {Path.GetFileName(fileName)}/{pptr.PathId}";
                }
            }

            string nameSpace = scriptBf["m_Namespace"].AsString;
            string className = scriptBf["m_ClassName"].AsString;

            string fullName;
            if (nameSpace != "")
                fullName = $"{nameSpace}.{className}";
            else
                fullName = className;

            return $"{idx} - {fullName}";
        };

        Scripts
            .ToObservableChangeSet()
            .Transform(scriptsNameTransFac)
            .Bind(out var scriptsItems)
            .DisposeMany()
            .Subscribe();

        ScriptsDisplay = scriptsItems!;
    }

    private ScriptInfo()
    {
        ScriptsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>());
    }

    public static ScriptInfo Empty { get; } = new()
    {
        Scripts = [],
        ScriptsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>())
    };
}
