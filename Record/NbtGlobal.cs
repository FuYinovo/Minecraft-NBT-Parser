using NBT_Parser.Enum;

namespace NBT_Parser.Record;

public abstract record NbtGlobal
{
    public const int NameLengthFieldSize = 2;
    public const int ListElementCountFieldSize = 4; // 列表的长度段: 前1字节为元素类型，后4字节为元素数量

    public static readonly Dictionary<byte, (NbtTagEnum Enum, int fieldSize, bool isDynamic, int? dataLengthMulti, Type?
            dataType)>
        ByteToInfo =
            new()
            {
                // 对于固定负载长度的标签：fieldSize是负载长度
                // 对于动态负载长度的标签：fieldSize是存储负载长度的字节数
                { 0, (NbtTagEnum.End, 0, false, null, null) },
                { 1, (NbtTagEnum.Byte, 1, false, null, typeof(byte)) },
                { 2, (NbtTagEnum.Short, 2, false, null, typeof(short)) },
                { 3, (NbtTagEnum.Int, 4, false, null, typeof(int)) },
                { 4, (NbtTagEnum.Long, 8, false, null, typeof(long)) },
                { 5, (NbtTagEnum.Float, 4, false, null, typeof(float)) },
                { 6, (NbtTagEnum.Double, 8, false, null, typeof(double)) },
                { 7, (NbtTagEnum.ByteArray, 4, true, 1, typeof(byte[])) },
                { 8, (NbtTagEnum.String, 2, true, 1, typeof(string[])) },
                { 9, (NbtTagEnum.List, 5, true, null, null) },
                { 10, (NbtTagEnum.Dictionary, 0, false, null, null) },
                { 11, (NbtTagEnum.IntArray, 4, true, 4, typeof(int[])) },
                { 12, (NbtTagEnum.LongArray, 4, true, 8, typeof(long[])) }
            };

    public static readonly Dictionary<NbtTagEnum, ConsoleColor>
        EnumToColor =
            new()
            {
                { NbtTagEnum.End, ConsoleColor.Red },
                { NbtTagEnum.Byte, ConsoleColor.Red },
                { NbtTagEnum.Short, ConsoleColor.Magenta },
                { NbtTagEnum.Int, ConsoleColor.Blue },
                { NbtTagEnum.Long, ConsoleColor.Cyan },
                { NbtTagEnum.Float, ConsoleColor.Yellow },
                { NbtTagEnum.Double, ConsoleColor.Green },
                { NbtTagEnum.ByteArray, ConsoleColor.DarkRed },
                { NbtTagEnum.String, ConsoleColor.Green },
                { NbtTagEnum.List, ConsoleColor.DarkYellow },
                { NbtTagEnum.Dictionary, ConsoleColor.DarkMagenta },
                { NbtTagEnum.IntArray, ConsoleColor.DarkBlue },
                { NbtTagEnum.LongArray, ConsoleColor.DarkCyan }
            };
}