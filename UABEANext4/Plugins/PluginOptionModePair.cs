namespace UABEANext4.Plugins;
public class PluginOptionModePair
{
    public IUavPluginOption Option { get; }
    public UavPluginMode Mode { get; }

    public PluginOptionModePair(IUavPluginOption option, UavPluginMode mode)
    {
        Option = option;
        Mode = mode;
    }

    public override string ToString()
    {
        return Option.Name;
    }
}
