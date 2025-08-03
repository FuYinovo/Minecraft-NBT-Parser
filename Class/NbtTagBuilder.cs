using NBT_Parser.Enum;

namespace NBT_Parser.Class;

public static class NbtTagBuilder
{
    public static NbtTag Byte(string name, byte value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Byte, isBigEndian, name, value);
    }

    public static NbtTag Short(string name, short value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Short, isBigEndian, name, value);
    }

    public static NbtTag Int(string name, int value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Int, isBigEndian, name, value);
    }

    public static NbtTag Long(string name, long value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Long, isBigEndian, name, value);
    }

    public static NbtTag Float(string name, float value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Float, isBigEndian, name, value);
    }

    public static NbtTag Double(string name, double value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Double, isBigEndian, name, value);
    }

    public static NbtTag String(string name, string value, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.String, isBigEndian, name, value);
    }

    public static NbtTag List(string name, List<NbtTag> children, NbtTagEnum childrenTag, bool isBigEndian)
    {
        var firstChildTag = children.First().Tag;
        foreach (var child in children)
        {
            child.IsListDirectElement = true;
            if (child.Tag != firstChildTag) throw new Exception($"[{firstChildTag}]列表不允许[{child.Tag}]!");
        }

        return new NbtTag(NbtTagEnum.List, isBigEndian, name, null, children, childrenTag);
    }

    public static NbtTag Dictionary(string name, List<NbtTag> children, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.Dictionary, isBigEndian, name, null, children);
    }

    public static NbtTag ByteArray(string name, IEnumerable<byte> bytes, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.ByteArray, isBigEndian, name, bytes);
    }

    public static NbtTag IntArray(string name, IEnumerable<int> ints, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.IntArray, isBigEndian, name, ints);
    }

    public static NbtTag LongArray(string name, IEnumerable<long> longs, bool isBigEndian)
    {
        return new NbtTag(NbtTagEnum.LongArray, isBigEndian, name, longs);
    }
}