using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Media;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace UABEANext4.AssetWorkspace;

public class WorkspaceItem : INotifyPropertyChanged
{
    public string OriginalName { get; set; }
    public string Name { get; set; }
    public WorkspaceItem? Parent { get; set; }
    public List<WorkspaceItem> Children { get; set; }
    public object? Object { get; set; }
    public WorkspaceItemType ObjectType { get; }
    public int LoadIndex { get; }

    public bool Loaded => Object != null;

    private static readonly SolidColorBrush BundleBrush = new(Avalonia.Media.Color.Parse("#f0ca93"));
    private static readonly SolidColorBrush AssetsBrush = new(Avalonia.Media.Color.Parse("#b17fd7"));
    private static readonly SolidColorBrush ResourceBrush = new(Avalonia.Media.Color.Parse("#569cd6"));
    private static readonly SolidColorBrush OtherBrush = new(Avalonia.Media.Color.Parse("#777777"));
    private static readonly SolidColorBrush EtcBrush = new(Avalonia.Media.Color.Parse("#ff4444"));

    public IBrush Color
    {
        get
        {
            return ObjectType switch
            {
                WorkspaceItemType.BundleFile => BundleBrush,
                WorkspaceItemType.AssetsFile => AssetsBrush,
                WorkspaceItemType.ResourceFile => ResourceBrush,
                WorkspaceItemType.OtherFile => OtherBrush,
                _ => EtcBrush,
            };
        }
    }

    public WorkspaceItem(AssetsFileInstance fileInst, int loadOrder)
    {
        Name = fileInst.name;
        OriginalName = Name;
        Parent = null;
        Children = new List<WorkspaceItem>(0);
        Object = fileInst;
        ObjectType = WorkspaceItemType.AssetsFile;
        LoadIndex = loadOrder;
    }

    public WorkspaceItem(Workspace workspace, BundleFileInstance bunInst, int loadOrder)
    {
        Name = bunInst.name;
        OriginalName = Name;
        int fileCount = bunInst.file.BlockAndDirInfo.DirectoryInfos.Count;
        Parent = null;
        Children = new List<WorkspaceItem>(fileCount);
        Object = bunInst;
        ObjectType = WorkspaceItemType.BundleFile;
        LoadIndex = loadOrder;

        for (int i = 0; i < fileCount; i++)
        {
            AssetBundleDirectoryInfo dirInf = BundleHelper.GetDirInfo(bunInst.file, i);
            WorkspaceItemType type = ((dirInf.Flags & 0x04) != 0)
                ? WorkspaceItemType.AssetsFile
                : WorkspaceItemType.ResourceFile;

            WorkspaceItem child;
            if (type == WorkspaceItemType.AssetsFile)
            {
                child = workspace.LoadAssetsFromBundle(bunInst, i);
            }
            else
            {
                child = new WorkspaceItem(dirInf.Name, dirInf, loadOrder, type);
            }

            workspace.AddChildItemThreadSafe(child, this, dirInf.Name);
        }
    }

    public WorkspaceItem(string name, object? obj, int loadOrder, WorkspaceItemType type = WorkspaceItemType.OtherFile)
    {
        Name = name;
        OriginalName = Name;
        Parent = null;
        Children = new List<WorkspaceItem>(0);
        Object = obj;
        ObjectType = type;
        LoadIndex = loadOrder;
    }

    public static IEnumerable<WorkspaceItem> GetAssetsFileWorkspaceItems(IEnumerable<WorkspaceItem> workspaceItems)
    {
        foreach (var item in workspaceItems)
        {
            if (item.ObjectType == WorkspaceItemType.AssetsFile)
            {
                yield return item;
            }

            if (item.ObjectType == WorkspaceItemType.BundleFile)
            {
                foreach (var assetFileChild in item.Children.Where(x => x.ObjectType == WorkspaceItemType.AssetsFile))
                {
                    yield return assetFileChild;
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
