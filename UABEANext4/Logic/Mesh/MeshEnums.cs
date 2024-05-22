namespace UABEANext4.Logic.Mesh
{
    public enum VertexChannelFormat
    {
        Float,
        Float16,
        Color,
        Byte,
        UInt32
    }

    public enum VertexFormatV2
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public enum VertexFormatV1
    {
        Float,
        Float16,
        Color,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public enum ChannelTypeV3
    {
        Vertex,
        Normal,
        Tangent,
        Color,
        TexCoord0,
        TexCoord1,
        TexCoord2,
        TexCoord3,
        TexCoord4,
        TexCoord5,
        TexCoord6,
        TexCoord7,
        BlendWeight,
        BlendIndices,
    }

    public enum ChannelTypeV2
    {
        Vertex,
        Normal,
        Color,
        TexCoord0,
        TexCoord1,
        TexCoord2,
        TexCoord3,
        Tangent,
    }

    public enum ChannelTypeV1
    {
        Vertex,
        Normal,
        Color,
        TexCoord0,
        TexCoord1,
        Tangent,
    }
}
