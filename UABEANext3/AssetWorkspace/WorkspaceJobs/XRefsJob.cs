using AssetsTools.NET.Extra;
using System.Collections.Generic;
using UABEAvalonia;

namespace UABEANext3.AssetWorkspace.WorkspaceJobs
{
    public class XRefsJob : IWorkspaceJob
    {
        private SanicPPtrScanner _scanner;
        private List<AssetsFileInstance> _files;

        public XRefsJob(SanicPPtrScanner scanner, List<AssetsFileInstance> files)
        {
            _scanner = scanner;
            _files = files;
        }

        public string GetTaskName()
        {
            return "XRefs";
        }

        public bool Execute()
        {
            foreach (AssetsFileInstance file in _files)
            {
                _scanner.ScanFile(file);
            }
            return true;
        }
    }
}
