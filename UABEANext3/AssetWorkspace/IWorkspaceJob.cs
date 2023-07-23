namespace UABEANext3.AssetWorkspace
{
    public interface IWorkspaceJob
    {
        public string GetTaskName();
        public bool Execute();
    }
}
