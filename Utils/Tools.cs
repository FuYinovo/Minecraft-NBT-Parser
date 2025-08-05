using System.Buffers.Binary;

namespace NBT_Parser.Utils;

public static class Tools
{
    private static readonly Dictionary<Type, Func<Span<byte>, bool, object>> ReadMethods = new()
    {
        [typeof(short)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadInt16BigEndian(span) : BinaryPrimitives.ReadInt16LittleEndian(span),
        [typeof(ushort)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt16LittleEndian(span),
        [typeof(int)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadInt32BigEndian(span) : BinaryPrimitives.ReadInt32LittleEndian(span),
        [typeof(uint)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span),
        [typeof(long)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span),
        [typeof(ulong)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(span) : BinaryPrimitives.ReadUInt64LittleEndian(span),
        [typeof(float)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadSingleBigEndian(span) : BinaryPrimitives.ReadSingleLittleEndian(span),
        [typeof(double)] = (span, bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadDoubleBigEndian(span) : BinaryPrimitives.ReadDoubleLittleEndian(span)
    };

    public static T ReadNumber<T>(Span<byte> bytes, bool isBigEndian) where T : struct
    {
        try
        {
            return (T)ReadMethods[typeof(T)].Invoke(bytes, isBigEndian);
        }
        catch (Exception)
        {
            throw new NotSupportedException($"不支持类型为 [{typeof(T)}] 的数字读取!");
        }
    }
}