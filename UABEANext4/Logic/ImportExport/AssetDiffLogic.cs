using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Logic.ImportExport;

public enum DiffStatus
{
    Same,
    Modified,
    LeftOnly,
    RightOnly
}

public class DiffAssetItem
{
    public AssetInst? LeftAsset { get; set; }
    public AssetInst? RightAsset { get; set; }
    public long PathId { get; set; }
    public DiffStatus Status { get; set; }
    
    public string Name => LeftAsset?.DisplayName ?? RightAsset?.DisplayName ?? "Unknown";
    public string Type => LeftAsset?.Type.ToString() ?? RightAsset?.Type.ToString() ?? "Unknown";
    public string SizeDiff
    {
        get
        {
            if (Status == DiffStatus.Modified)
                return $"{LeftAsset?.ByteSize} -> {RightAsset?.ByteSize}";
            return LeftAsset?.ByteSize.ToString() ?? RightAsset?.ByteSize.ToString() ?? "0";
        }
    }
}

public static class AssetDiffLogic
{
    public static async Task<List<DiffAssetItem>> CompareFiles(Workspace workspace, AssetsFileInstance left, AssetsFileInstance right)
    {
        var results = new List<DiffAssetItem>();

        await Task.Run(() =>
        {
            var leftAssets = left.file.AssetInfos.ToDictionary(a => a.PathId);
            var rightAssets = right.file.AssetInfos.ToDictionary(a => a.PathId);

            var allPathIds = leftAssets.Keys.Union(rightAssets.Keys).OrderBy(x => x).ToList();
            int total = allPathIds.Count;
            int current = 0;

            foreach (var pathId in allPathIds)
            {
                current++;
                if (current % 100 == 0)
                    workspace.SetProgressThreadSafe((float)current / total, $"Comparing assets {current}/{total}...");

                bool hasLeft = leftAssets.TryGetValue(pathId, out var leftInfo);
                bool hasRight = rightAssets.TryGetValue(pathId, out var rightInfo);

                var item = new DiffAssetItem { PathId = pathId };

                if (hasLeft) item.LeftAsset = workspace.GetAssetInst(left, 0, pathId);
                if (hasRight) item.RightAsset = workspace.GetAssetInst(right, 0, pathId);

                if (hasLeft && hasRight)
                {
                    if (IsModified(leftInfo!, rightInfo!, left, right))
                    {
                        item.Status = DiffStatus.Modified;
                        results.Add(item);
                    }
                    // Одинаковые файлы пропускаем (раскомментируйте, если нужны)
                    // else { item.Status = DiffStatus.Same; results.Add(item); }
                }
                else if (hasLeft)
                {
                    item.Status = DiffStatus.LeftOnly;
                    results.Add(item);
                }
                else
                {
                    item.Status = DiffStatus.RightOnly;
                    results.Add(item);
                }
            }
            workspace.SetProgressThreadSafe(1.0f, "Comparison complete.");
        });

        return results;
    }

    private static bool IsModified(AssetFileInfo left, AssetFileInfo right, AssetsFileInstance leftFile, AssetsFileInstance rightFile)
    {
        if (left.ByteSize != right.ByteSize) return true;
        if (left.TypeId != right.TypeId) return true;

        var leftReader = leftFile.file.Reader;
        var rightReader = rightFile.file.Reader;

        lock (leftFile.LockReader)
        {
            lock (rightFile.LockReader)
            {
                leftReader.Position = left.GetAbsoluteByteOffset(leftFile.file);
                rightReader.Position = right.GetAbsoluteByteOffset(rightFile.file);

                byte[] bufferL = new byte[4096];
                byte[] bufferR = new byte[4096];
                long remaining = left.ByteSize;

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, 4096);
                    leftReader.Read(bufferL, 0, toRead);
                    rightReader.Read(bufferR, 0, toRead);

                    for (int i = 0; i < toRead; i++)
                    {
                        if (bufferL[i] != bufferR[i]) return true;
                    }
                    remaining -= toRead;
                }
            }
        }
        return false;
    }
}
