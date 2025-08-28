using System.Buffers.Binary;

namespace NBT_Parser.Utils
{
    public static class Tools
    {
       private static readonly Dictionary<Type, Func<Memory<byte>, bool, object>> ReadMethods = new()
    {
        [typeof(short)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(span.Span)
                : BinaryPrimitives.ReadInt16LittleEndian(span.Span),
        [typeof(ushort)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span.Span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span.Span),
        [typeof(int)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span.Span)
                : BinaryPrimitives.ReadInt32LittleEndian(span.Span),
        [typeof(uint)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(span.Span)
                : BinaryPrimitives.ReadUInt32LittleEndian(span.Span),
        [typeof(long)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(span.Span)
                : BinaryPrimitives.ReadInt64LittleEndian(span.Span),
        [typeof(ulong)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(span.Span)
                : BinaryPrimitives.ReadUInt64LittleEndian(span.Span),
        [typeof(float)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(span.Span)
                : BinaryPrimitives.ReadSingleLittleEndian(span.Span),
        [typeof(double)] = (span, bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(span.Span)
                : BinaryPrimitives.ReadDoubleLittleEndian(span.Span)
    };

        public static T ReadBinaryNumber<T>(Span<byte> bytes, bool isBigEndian) where T : struct
        {
            try
            {
                return (T)ReadMethods[typeof(T)].Invoke(bytes.ToArray(), isBigEndian);
            }
            catch (Exception)
            {
                throw new NotSupportedException($"不支持类型为 [{typeof(T)}] 的数字读取!");
            }
        }
    }
}