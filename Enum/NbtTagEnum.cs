namespace NBT_Parser.Enum;

public enum NbtTagEnum
{
    Unknown = -1,
    End = 0,
    Byte = 1,
    Short = 2,
    Int = 3,
    Long = 4,
    Float = 5,
    Double = 6,
    ByteArray = 7,
    String = 8,
    List = 9,
    Dictionary = 10,
    IntArray = 11,
    LongArray = 12
}

public static class NbtTagEnumExtensions
{
    public static bool IsNumber(NbtTagEnum tagEnum)
    {
        return tagEnum switch
        {
            NbtTagEnum.Byte or
                NbtTagEnum.Short or
                NbtTagEnum.Int or
                NbtTagEnum.Long or
                NbtTagEnum.Float or
                NbtTagEnum.Double => true,
            _ => false,
        };
    }

    public static bool IsCollection(NbtTagEnum tagEnum)
    {
        return tagEnum switch
        {
            NbtTagEnum.Dictionary or
                NbtTagEnum.List => true,
            _ => false
        };
    }

    public static bool IsArray(NbtTagEnum tagEnum)
    {
        return tagEnum switch
        {
            NbtTagEnum.ByteArray or
                NbtTagEnum.IntArray or
                NbtTagEnum.LongArray => true,
            _ => false
        };
    }


    public static bool AllowDecimal(NbtTagEnum tagEnum)
    {
        if (!IsNumber(tagEnum)) return false;
        return tagEnum switch
        {
            NbtTagEnum.Float or
                NbtTagEnum.Double => true,
            _ => false
        };
    }

    public static bool AllowNegative(NbtTagEnum tagEnum)
    {
        if (!IsNumber(tagEnum)) return false;
        return tagEnum switch
        {
            NbtTagEnum.Byte => false,
            _ => true
        };
    }
}