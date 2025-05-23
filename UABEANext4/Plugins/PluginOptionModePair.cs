namespace UABEANext4.Plugins;
public class PluginOptionModePair(IUavPluginOption option, UavPluginMode mode)
{
    public IUavPluginOption Option { get; } = option;
    public UavPluginMode Mode { get; } = mode;

    public override string ToString()
    {
        return Option.Name;
    }
}
