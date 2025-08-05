using System.Buffers.Binary;

namespace NBT_Parser.Utils;

public static class Tools
{
    public static T ReadNumber<T>(Span<byte> bytes, bool isBigEndian) where T : struct
    {
        if (typeof(T) == typeof(short))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(bytes)
                : BinaryPrimitives.ReadInt16LittleEndian(bytes));
        if (typeof(T) == typeof(ushort))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
                : BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        if (typeof(T) == typeof(int))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(bytes)
                : BinaryPrimitives.ReadInt32LittleEndian(bytes));
        if (typeof(T) == typeof(uint))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(bytes)
                : BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        if (typeof(T) == typeof(long))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(bytes)
                : BinaryPrimitives.ReadInt64LittleEndian(bytes));
        if (typeof(T) == typeof(ulong))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(bytes)
                : BinaryPrimitives.ReadUInt64LittleEndian(bytes));
        if (typeof(T) == typeof(float))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(bytes)
                : BinaryPrimitives.ReadSingleLittleEndian(bytes));
        if (typeof(T) == typeof(double))
            return (T)(object)(isBigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(bytes)
                : BinaryPrimitives.ReadDoubleLittleEndian(bytes));

        throw new NotSupportedException($"不支持类型为 [{typeof(T)}] 的数字读取!");
    }
}