using NBT_Parser.Enum;
using NBT_Parser.Record;
using NBT_Parser.Utils;

namespace NBT_Parser.Class;

public class NbtParser
{
    private byte[] _bytes = [];
    private bool _isBigEndian;


    /// <summary>
    ///     解析一个 NBT 字节集合
    /// </summary>
    /// <param name="bytes">字节集合</param>
    /// <param name="isBigEndian">字节序是否为大端序(JE:大端序|BE:小端序)</param>
    /// <param name="begin">有效数据头部位置</param>
    /// <returns>一个树状结构的 <see cref="NbtTag" />  </returns>
    public NbtTag Parse(byte[] bytes, bool isBigEndian, int begin = 0)
    {
        _bytes = bytes;
        _isBigEndian = isBigEndian;
        var tags = GetTags(begin);
        return ConstructTagTree(ref tags);
    }

    /// <summary>
    ///     构建 NBT 标签树形结构
    /// </summary>
    /// <remarks>
    ///     字典的结束标签将被保留为最后一个子元素
    /// </remarks>
    /// <param name="tags">一个包含 End 标签的原始 NBT 标签列表</param>
    /// <returns>一个树形结构的 NBT 标签</returns>
    private static NbtTag ConstructTagTree(ref List<NbtTag> tags)
    {
        var stack = new Stack<NbtTag>();
        foreach (var tag in tags)
            switch (tag.Tag)
            {
                case NbtTagEnum.Dictionary:
                    stack.Push(tag);
                    continue;
                case NbtTagEnum.List:
                    // 为列表内元素构建树形结构
                    stack.Peek().Children.Add(ConstructListTagTree(tag));
                    continue;
                case NbtTagEnum.End:
                {
                    stack.Peek().Children.Add(tag);
                    if (stack.Count == 1) return stack.Peek(); // 栈内仅剩的1个元素时，其为最终结果
                    var completedTag = stack.Pop();
                    stack.Peek().Children.Add(completedTag);
                    continue;
                }
                default:
                    stack.Peek().Children.Add(tag);
                    break;
            }

        throw new Exception("建构树形结构失败!");
    }

    /// <summary>
    ///     构建列表 NBT 标签属性结构
    /// </summary>
    /// <remarks>
    ///     字典的结束标签将被保留为最后一个子元素
    /// </remarks>
    /// <param name="listTag">列表 NBT 标签</param>
    /// <returns>一个树形结构的新列表 NBT 标签</returns>
    private static NbtTag ConstructListTagTree(NbtTag listTag)
    {
        var stack = new Stack<NbtTag>();
        if (listTag.ChildrenTag != NbtTagEnum.Dictionary) return listTag; // 非字典列表无需处理
        foreach (var child in listTag.Children)
            switch (child.Tag)
            {
                case NbtTagEnum.List:
                    stack.Peek().Children.Add(ConstructListTagTree(child));
                    continue;
                case NbtTagEnum.End:
                    stack.Peek().Children.Add(child);
                    if (child.IsListDirectElement) continue;
                    var completedTag = stack.Pop();
                    stack.Peek().Children.Add(completedTag);
                    continue;
                case NbtTagEnum.Dictionary:
                    stack.Push(child);
                    continue;
                default:
                    stack.Peek().Children.Add(child);
                    continue;
            }

        listTag.Children = stack.ToList();
        listTag.Children.Reverse();
        return listTag;
    }

    /// <summary>
    ///     获取所有 NBT 标签实例
    /// </summary>
    /// <param name="begin">有效数据头部位置</param>
    /// <returns>一个 <see cref="NbtTag" /> 列表</returns>
    private List<NbtTag> GetTags(int begin)
    {
        var list = new List<NbtTag>();
        var offset = begin;
        while (offset < _bytes.Length) list.Add(GetTag(ref offset));
        return list;
    }

    /// <summary>
    ///     获取单个 NBT 标签实例
    /// </summary>
    /// <param name="offset">标签头部位置</param>
    /// <param name="listElementsTag">List 子元素类型</param>
    /// <param name="isListDirectItem">是否是列表直接子元素</param>
    /// <returns>一个 NBT 标签实例</returns>
    private NbtTag GetTag(ref int offset, NbtTagEnum listElementsTag = NbtTagEnum.Unknown,
        bool isListDirectItem = false)
    {
        var tagEnum = isListDirectItem ? listElementsTag : ConsumeTagEnum(ref offset);
        return QueryIsDynamic(tagEnum)
            ? tagEnum == NbtTagEnum.List
                ? ConsumeListTag(ref offset, isListDirectItem)
                : ConsumeDynamicTag(ref offset, tagEnum, isListDirectItem)
            : ConsumeConstTag(ref offset, tagEnum, isListDirectItem);
    }

    /// <summary>
    ///     解析一个动态负载长度的 NBT 标签(不负责列表标签)
    /// </summary>
    /// <code>
    ///  负责：ByteArray, String, IntArray, LongArray
    ///  </code>
    /// <param name="offset">标签头部位置</param>
    /// <param name="tagEnum">标签枚举</param>
    /// <param name="isListDirectItem">是否是列表直接子元素</param>
    /// <returns>一个 NBT 标签实例</returns>
    private NbtTag ConsumeDynamicTag(ref int offset, NbtTagEnum tagEnum, bool isListDirectItem = false)
    {
        var begin = offset - 1;
        var dataLengthMulti = QueryDataLengthMulti(tagEnum);
        var dataLengthFieldSize = QueryFieldSize(tagEnum);
        var nameLength = ConsumeLengthField<ushort>(ref offset, NbtGlobal.NameLengthFieldSize);
        offset += nameLength;

        // 列表元素
        if (isListDirectItem)
        {
            begin++; // 无标签ID
            // 列表<字符串>的元素的名称即是值
            if (isListDirectItem && tagEnum == NbtTagEnum.String)
                return BuildTag(tagEnum, begin, offset - begin, isListDirectItem);
        }

        // 非列表元素
        var dataLength = dataLengthFieldSize switch
        {
            2 => ConsumeLengthField<ushort>(ref offset, 2),
            4 => ConsumeLengthField<int>(ref offset, 4),
            _ => throw new Exception($"[{tagEnum}]数据长度段占用[{dataLengthFieldSize}]字节!检查[NbtGlobal]是否正确!")
        };
        offset += dataLengthMulti * dataLength;
        return BuildTag(tagEnum, begin, offset - begin, isListDirectItem);
    }

    /// <summary>
    ///     解析一个固定负载长度的 NBT 标签
    /// </summary>
    /// <code>
    ///  负责：Byte, Short, Int, Long, Float, Double, Dictionary, End
    ///  </code>
    /// <param name="offset">标签头部位置</param>
    /// <param name="tagEnum">标签枚举</param>
    /// <param name="isListDirectItem">是否是列表直接子元素</param>
    /// <returns>一个 NBT 标签实例</returns>
    private NbtTag ConsumeConstTag(ref int offset, NbtTagEnum tagEnum, bool isListDirectItem = false)
    {
        var begin = offset - 1;
        var dataLength = QueryFieldSize(tagEnum);

        // 对于 End 标签，长度固定为 1 字节
        if (tagEnum is NbtTagEnum.End) return BuildTag(tagEnum, begin, 1);

        // 列表元素
        if (isListDirectItem)
        {
            // 没有ID和名称
            begin++;
            offset += dataLength;
            return BuildTag(tagEnum, begin, offset - begin, isListDirectItem);
        }

        // 非列表元素
        var nameLength = ConsumeLengthField<ushort>(ref offset, NbtGlobal.NameLengthFieldSize);
        offset += nameLength + dataLength;
        // 数据：[03] (00 01) "D1" (00 00 00 00) [03] ........
        // 移动：[01] [02]    [03] [04]          [05]
        // 执行：[01]Init | [02]GetTag() | [03]ConsumeLengthField() | [04]+nameLength | [05]+dataLength
        return BuildTag(tagEnum, begin, offset - begin, isListDirectItem);
    }

    /// <summary>
    ///     解析一个列表 NBT 标签
    /// </summary>
    /// <code>
    ///  负责：List
    ///  </code>
    /// <param name="offset">标签头部位置</param>
    /// <param name="isListDirectItem">是否是列表直接子元素</param>
    /// <returns>一个 NBT 标签</returns>
    private NbtTag ConsumeListTag(ref int offset, bool isListDirectItem = false)
    {
        var begin = offset - 1;
        var nameLength = ConsumeLengthField<ushort>(ref offset, NbtGlobal.NameLengthFieldSize);
        offset += nameLength;
        var elementTag = ConsumeTagEnum(ref offset);
        var elementCount = ConsumeLengthField<int>(ref offset, NbtGlobal.ListElementCountFieldSize);

        var elements = ConsumeListElements(ref offset, elementTag, elementCount);
        var length = NbtGlobal.NameLengthFieldSize + nameLength + 1 + NbtGlobal.ListElementCountFieldSize + 1;
        return BuildTag(NbtTagEnum.List, begin, length, isListDirectItem, elementTag, elements);
    }

    /// <summary>
    ///     解析一个列表 NBT 标签的所有元素
    /// </summary>
    /// <param name="offset">第一个元素头部位置</param>
    /// <param name="elementsTag">元素类型</param>
    /// <param name="elementsCount">元素数量</param>
    /// <returns>一个 <see cref="NbtTag" /> 列表</returns>
    private List<NbtTag> ConsumeListElements(ref int offset, NbtTagEnum elementsTag, int elementsCount)
    {
        // 对于列表<字典>，需要存储字典+结束标签，故2倍容量
        var elements = new List<NbtTag>(elementsTag == NbtTagEnum.Dictionary ? elementsCount * 2 : elementsCount);
        for (var i = 1; i <= elementsCount; i++)
        {
            // 列表<一般标签>的处理
            if (elementsTag != NbtTagEnum.Dictionary)
            {
                elements.Add(GetTag(ref offset, elementsTag, true));
                continue;
            }

            // 列表<字典>的处理
            // 列表子元素为隐式标签ID，为便于构建树形结构，补一个字典标签
            elements.Add(BuildTag(NbtTagEnum.Dictionary, offset, 1, true));

            var isEnd = false; // 第 i 个子元素是否结束
            var require = 1; // 遇到多个结束标签算作结束
            var count = 0; // 遇到了多少个结束标签
            while (!isEnd)
            {
                var tag = GetTag(ref offset);

                // List<Dict>
                //     Dict[A]
                //         Dict[Aa]
                //         End [Aa]
                //     End[A]
                // 若Dict[A]里嵌套了Dict[Aa]，End[Aa]将被误判为End[A]，故令 limit++
                if (tag.Tag == NbtTagEnum.Dictionary) require++;
                if (tag.Tag == NbtTagEnum.End)
                {
                    count++;
                    if (count == require) isEnd = true;
                }

                elements.Add(tag);
            }

            elements.Last().IsListDirectElement = true; // 给子元素结束标签标记为直接子元素(便于构建树形结构)
        }

        return elements;
    }

    /// <summary>
    ///     构造一个 NBT 标签实例
    /// </summary>
    /// <param name="type">标签类型</param>
    /// <param name="begin">标签头部位置</param>
    /// <param name="length">标签长度</param>
    /// <param name="isListDirectItem">是否为列表直接子元素</param>
    /// <param name="childrenTag">子元素类型</param>
    /// <param name="children">子元素列表</param>
    /// <returns>NBT 标签实例</returns>
    private NbtTag BuildTag(NbtTagEnum type, int begin, int length, bool isListDirectItem = false,
        NbtTagEnum childrenTag = NbtTagEnum.Unknown, List<NbtTag>? children = null)
    {
        var bytes = _bytes.AsMemory().Slice(begin, length);
        return new NbtTag(type, bytes, _isBigEndian, children, childrenTag, isListDirectItem);
    }

    /// <summary>
    ///     获取标签枚举
    /// </summary>
    /// <param name="offset">标签头部位置</param>
    /// <returns>标签枚举</returns>
    private NbtTagEnum ConsumeTagEnum(ref int offset)
    {
        var tagEnum = QueryEnum(_bytes[offset]);
        offset++;
        return tagEnum;
    }

    private T ConsumeLengthField<T>(ref int offset, int fieldSize) where T : struct
    {
        var bytes = _bytes.AsSpan(offset, fieldSize).ToArray();
        var length = Tools.ReadNumber<T>(bytes, _isBigEndian);
        offset += fieldSize;
        return length;
    }

    #region Querier

    /// <returns>
    ///     <para>
    ///         对于固定负载长度的标签：负载长度
    ///     </para>
    ///     <para>对于动态负载长度的标签：存储负载长度的字节数</para>
    /// </returns>
    private static int QueryFieldSize(NbtTagEnum tagEnum)
    {
        return NbtGlobal.ByteToInfo[(byte)tagEnum].fieldSize;
    }

    private static bool QueryIsDynamic(NbtTagEnum tagEnum)
    {
        return NbtGlobal.ByteToInfo[(byte)tagEnum].isDynamic;
    }

    private static int QueryDataLengthMulti(NbtTagEnum tagEnum)
    {
        return NbtGlobal.ByteToInfo[(byte)tagEnum].dataLengthMulti ??
               throw new Exception($"[{tagEnum}]的负载长度非倍数!");
    }

    private static NbtTagEnum QueryEnum(byte tag)
    {
        if (!NbtGlobal.ByteToInfo.TryGetValue(tag, out var info))
            throw new Exception($"不存在编号为[{tag}]的标签!");
        return info.Enum;
    }

    #endregion
}