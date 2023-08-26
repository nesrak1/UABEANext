namespace UABEANext3.Logic.Plugins
{
    public class UABEAPluginMenuInfo
    {
        public readonly UABEAPluginInfo PluginInf;
        public readonly UABEAPluginOption PluginOpt;
        public readonly string DisplayName;

        public UABEAPluginMenuInfo(UABEAPluginInfo pluginInf, UABEAPluginOption pluginOpt, string displayName)
        {
            PluginInf = pluginInf;
            PluginOpt = pluginOpt;
            DisplayName = displayName;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
