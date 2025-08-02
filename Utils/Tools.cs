using System.Buffers.Binary;

namespace NBT_Parser.Utils;

public static class Tools
{
    /// <summary>
    ///     读取一个长度
    /// </summary>
    /// <param name="begin">长度数据头部位置</param>
    /// <param name="length">长度数据字节数</param>
    /// <param name="bytes">字节集合</param>
    /// <param name="isBigEndian">是否大端序</param>
    /// <returns>一个长度整数</returns>
    public static int ReadLength(int begin, int length, byte[] bytes, bool isBigEndian)
    {
        var span = bytes.AsSpan(begin, length);
        return length switch
        {
            2 => isBigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span),
            4 => isBigEndian ? BinaryPrimitives.ReadInt32BigEndian(span) : BinaryPrimitives.ReadInt32LittleEndian(span),
            _ => throw new Exception("长度必须为 Int32 或 Int64!")
        };
    }
}