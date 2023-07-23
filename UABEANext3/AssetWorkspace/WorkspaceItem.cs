using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEANext3.AssetWorkspace
{
    public class WorkspaceItem
    {
        public string Name { get; set; }
        public WorkspaceItem? Parent { get; set; }
        public List<WorkspaceItem> Children { get; set; }
        public object? Object { get; set; }
        public WorkspaceItemType ObjectType { get; }

        public bool Loaded => Object != null;

        private static readonly SolidColorBrush BundleBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#f0ca93"));
        private static readonly SolidColorBrush AssetsBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#b17fd7"));
        private static readonly SolidColorBrush ResourceBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#569cd6"));
        private static readonly SolidColorBrush OtherBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#777777"));
        private static readonly SolidColorBrush EtcBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#123123"));
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

        public WorkspaceItem(AssetsFileInstance fileInst)
        {
            Name = fileInst.name;
            Parent = null;
            Children = new List<WorkspaceItem>(0);
            Object = fileInst;
            ObjectType = WorkspaceItemType.AssetsFile;
        }

        public WorkspaceItem(Workspace workspace, BundleFileInstance bunInst)
        {
            Name = bunInst.name;
            int fileCount = bunInst.file.BlockAndDirInfo.DirectoryInfos.Count;
            Parent = null;
            Children = new List<WorkspaceItem>(fileCount);
            Object = bunInst;
            ObjectType = WorkspaceItemType.BundleFile;

            for (int i = 0; i < fileCount; i++)
            {
                AssetBundleDirectoryInfo dirInf = BundleHelper.GetDirInfo(bunInst.file, i);
                WorkspaceItemType type = ((dirInf.Flags & 0x04) != 0)
                    ? WorkspaceItemType.AssetsFile
                    : WorkspaceItemType.ResourceFile;

                if (type == WorkspaceItemType.AssetsFile)
                {
                    AssetsFileInstance fileInst = workspace.Manager.LoadAssetsFileFromBundle(bunInst, i);
                    WorkspaceItem child = new WorkspaceItem(dirInf.Name, fileInst, type);
                    child.Parent = this;
                    Children.Add(child);
                    workspace.ItemLookup[dirInf.Name] = child;

                    if (workspace.Manager.ClassDatabase == null)
                    {
                        // pretty sure you cannot have both stripped versions and
                        // stripped type tree, so this should be fine, although
                        // we could always look at the bundle header
                        AssetsFileMetadata metadata = fileInst.file.Metadata;
                        string fileVersion = metadata.UnityVersion;
                        bool strippedTypeTree = !metadata.TypeTreeEnabled;
                        if (fileVersion != "0.0.0")
                        {
                            workspace.Manager.LoadClassDatabaseFromPackage(fileVersion);
                        }
                        else if (strippedTypeTree)
                        {
                            throw new Exception("Bundle has both stripped version and type tree! This shouldn't happen!");
                        }
                    }
                }
                else
                {
                    WorkspaceItem child = new WorkspaceItem(dirInf.Name, dirInf, type);
                    child.Parent = this;
                    Children.Add(child);
                    workspace.ItemLookup[dirInf.Name] = child;
                }
            }
        }

        public WorkspaceItem(string name, object? obj, WorkspaceItemType type = WorkspaceItemType.OtherFile)
        {
            Name = name;
            Parent = null;
            Children = new List<WorkspaceItem>(0);
            Object = obj;
            ObjectType = type;
        }
    }
}
