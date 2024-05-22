using AssetsTools.NET;

namespace UABEANext4.Logic.Mesh
{
    public class Channel
    {
        public byte stream;
        public byte offset;
        public byte format;
        public byte dimension;
        public Channel(AssetTypeValueField field)
        {
            stream = field["stream"].AsByte;
            offset = field["offset"].AsByte;
            format = field["format"].AsByte;
            dimension = field["dimension"].AsByte;
        }
    }
}
