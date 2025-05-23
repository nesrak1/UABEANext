namespace UABEANext4.Plugins;
public class PluginPreviewerTypePair(IUavPluginPreviewer previewer, UavPluginPreviewerType previewType)
{
    public IUavPluginPreviewer Previewer { get; } = previewer;
    public UavPluginPreviewerType PreviewType { get; } = previewType;

    public override string ToString()
    {
        return Previewer.Name;
    }
}
