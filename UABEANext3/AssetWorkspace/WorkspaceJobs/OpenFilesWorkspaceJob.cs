using AssetsTools.NET;
using System.IO;
using UABEANext3.Logic;

namespace UABEANext3.AssetWorkspace.WorkspaceJobs
{
    public class OpenFilesWorkspaceJob : IWorkspaceJob
    {
        private readonly Workspace workspace;
        private readonly string path;

        public OpenFilesWorkspaceJob(Workspace workspace, string path)
        {
            this.workspace = workspace;
            this.path = path;
        }

        public string GetTaskName()
        {
            return $"Open file {Path.GetFileName(path)}";
        }

        public bool Execute()
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var detectedType = FileTypeDetector.DetectFileType(new AssetsFileReader(fileStream), 0);
            if (detectedType == DetectedFileType.BundleFile)
            {
                fileStream.Position = 0;
                workspace.LoadBundle(fileStream);
            }
            else if (detectedType == DetectedFileType.AssetsFile)
            {
                fileStream.Position = 0;
                workspace.LoadAssets(fileStream);
            }
            else if (path.EndsWith(".resS") || path.EndsWith(".resource"))
            {
                workspace.LoadResource(fileStream);
            }

            return true;
        }
    }
}
