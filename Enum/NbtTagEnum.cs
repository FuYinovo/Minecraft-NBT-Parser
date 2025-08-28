namespace NBT_Parser.Enum
{
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
        public static readonly NbtTagEnum[] Numbers =
        [
            NbtTagEnum.Byte,
            NbtTagEnum.Short,
            NbtTagEnum.Int,
            NbtTagEnum.Long,
            NbtTagEnum.Float,
            NbtTagEnum.Double
        ];

        public static readonly NbtTagEnum[] Arrays =
        [
            NbtTagEnum.ByteArray,
            NbtTagEnum.IntArray,
            NbtTagEnum.LongArray
        ];

        public static readonly NbtTagEnum[] Collections =
        [
            NbtTagEnum.List,
            NbtTagEnum.Dictionary
        ];

        public static readonly NbtTagEnum[] Others =
        [
            NbtTagEnum.String
        ];

        public static bool IsNumber(NbtTagEnum tagEnum)
        {
            return Numbers.Contains(tagEnum);
        }

        public static bool IsCollection(NbtTagEnum tagEnum)
        {
            return Collections.Contains(tagEnum);
        }

        public static bool IsArray(NbtTagEnum tagEnum)
        {
            return Arrays.Contains(tagEnum);
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

        public static NbtTagEnum GetArrayElementType(NbtTagEnum tagEnum)
        {
            return tagEnum switch
            {
                NbtTagEnum.ByteArray => NbtTagEnum.Byte,
                NbtTagEnum.IntArray => NbtTagEnum.Int,
                NbtTagEnum.LongArray => NbtTagEnum.Long,
                _ => NbtTagEnum.Unknown
            };
        }

        public static object GetDefaultValue(NbtTagEnum tagEnum)
        {
            return tagEnum switch
            {
                NbtTagEnum.Unknown => throw new InvalidOperationException(),
                NbtTagEnum.End => throw new InvalidOperationException(),
                NbtTagEnum.Byte => (byte)0,
                NbtTagEnum.Short => (short)0,
                NbtTagEnum.Int => 0,
                NbtTagEnum.Long => (long)0,
                NbtTagEnum.Float => (float)0,
                NbtTagEnum.Double => (double)0,
                NbtTagEnum.ByteArray => Array.Empty<byte>(),
                NbtTagEnum.String => string.Empty,
                NbtTagEnum.List => throw new InvalidOperationException(),
                NbtTagEnum.Dictionary => throw new InvalidOperationException(),
                NbtTagEnum.IntArray => Array.Empty<int>(),
                NbtTagEnum.LongArray => Array.Empty<long>(),
                _ => throw new ArgumentOutOfRangeException(nameof(tagEnum), tagEnum, null)
            };
        }
    }
}