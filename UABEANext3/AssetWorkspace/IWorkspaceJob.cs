using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEANext3.AssetWorkspace
{
    public interface IWorkspaceJob
    {
        public string GetTaskName();
        public bool Execute();
    }
}
