using System.Buffers.Binary;
using System.Text;
using NBT_Parser.Enum;
using NBT_Parser.Record;

namespace NBT_Parser.Class;

public class NbtTag
{
    public List<NbtTag> Children;
    public readonly NbtTagEnum ChildrenTag;
    public readonly NbtTagEnum Tag;
    public bool IsListDirectElement;
    private readonly Memory<byte> _bytes;
    private readonly bool _isBigEndian;
    private string? _name;
    private string? _value;

    public NbtTag(NbtTagEnum tag,
        Memory<byte> bytes,
        bool isBigEndian,
        List<NbtTag>? children = null,
        NbtTagEnum childrenTag = NbtTagEnum.Unknown,
        bool isListDirectElement = false) // 便于构造树形结构，避免单元素(伪)列表)
    {
        _bytes = bytes;
        Children = children ?? [];
        ChildrenTag = childrenTag;
        _isBigEndian = isBigEndian;
        Tag = tag;
        IsListDirectElement = isListDirectElement;
        _name = GetName();
        _value = GetValue();
    }

    /// <summary>
    ///     获取标签名称
    /// </summary>
    /// <returns>名称的字符串</returns>
    public string GetName()
    {
        try
        {
            if (_name is not null) return _name;
            _name = string.Empty;

            if (IsListDirectElement) return _name; // 列表元素没有名称

            var nameLength = GetNameLength();
            if (nameLength == 0) return _name; // 若名称长度为零，直接返回

            var nameField = _bytes.Span.Slice(NbtGlobal.NameLengthFieldSize + 1, nameLength);
            _name = Encoding.UTF8.GetString(nameField);

            return _name;
        }
        catch (Exception e)
        {
            return $"{e.GetType()}";
        }
    }

    /// <summary>
    ///     获取标签值
    /// </summary>
    /// <returns>值的字符串</returns>
    public string GetValue()
    {
        try
        {
            if (_value is not null) return _value;
            _value = string.Empty;

            if (Tag is NbtTagEnum.Dictionary or NbtTagEnum.List) return _value; // 列表或字典只有子元素，没有值
            return NbtGlobal.ByteToInfo[(byte)Tag].isDynamic switch
            {
                true => ParseDynamicValue(),
                false => ParseConstValue()
            };
        }
        catch (Exception e)
        {
            return e.GetType().ToString();
        }
    }

    /// <summary>
    ///     为动态负载长度的标签解析值
    /// </summary>
    /// <code>
    ///  负责：ByteArray, String, IntArray, LongArray
    ///  </code>
    /// <returns>值的字符串</returns>
    /// <exception cref="Exception">当前标签不是动态负载长度标签</exception>
    private string ParseDynamicValue()
    {
        // 确定数据范围
        var info = NbtGlobal.ByteToInfo[(byte)Tag];
        var nameLength = GetNameLength(); // 若标签是列表元素，该方法返回 0

        var dataLengthField =
            IsListDirectElement switch
            {
                true => _bytes.Span[..info.fieldSize],
                false => _bytes.Span.Slice(NbtGlobal.NameLengthFieldSize + nameLength + 1, info.fieldSize)
            };

        var dataLength = info.fieldSize switch
        {
            2 => _isBigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(dataLengthField)
                : BinaryPrimitives.ReadInt16LittleEndian(dataLengthField),
            4 => _isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(dataLengthField)
                : BinaryPrimitives.ReadInt32LittleEndian(dataLengthField),
            _ => throw new Exception("未知标签!") // 正常不可能报错
        };

        if (dataLength == 0) return ""; // 长度为零，则直接返回空字符串

        var dataBegin = IsListDirectElement switch
        {
            true => info.fieldSize, // 列表<动态负载长度>中，元素没有名称，但存储了长度 (差别: 见 ParseConstValue 方法)
            false => NbtGlobal.NameLengthFieldSize + info.fieldSize + nameLength + 1
        };
        var data = _bytes.Span.Slice(dataBegin,
            dataLength * info.dataLengthMulti ?? throw new Exception($"动态负载长度标签[{Tag}]没有在[NbtGlobal]中标记负载长度倍率!"));

        // 数据处理
        switch (Tag)
        {
            case NbtTagEnum.ByteArray:
                return string.Join(", ", data.ToArray());
            case NbtTagEnum.String:
                return Encoding.UTF8.GetString(data);
            case NbtTagEnum.IntArray:
                return ConstructArrayString(data, 4);
            case NbtTagEnum.LongArray:
                return ConstructArrayString(data, 8);
            default:
                throw new Exception($"[{Tag}]不是动态负载长度标签!");
        }

        string ConstructArrayString(Span<byte> bytes, object type)
        {
            var length = type switch
            {
                int => 4,
                long => 8,
                _ => throw new Exception($"不支持[{type.GetType()}]数组!")
            };
            var elements = new long[bytes.Length / length];
            for (var i = 0; i < bytes.Length; i += length)
            {
                var singleLong = bytes.Slice(i, length);
                elements[i / length] = _isBigEndian switch
                {
                    true => length == 4
                        ? BinaryPrimitives.ReadInt32BigEndian(singleLong)
                        : BinaryPrimitives.ReadInt64BigEndian(singleLong),
                    false => length == 4
                        ? BinaryPrimitives.ReadInt32LittleEndian(singleLong)
                        : BinaryPrimitives.ReadInt64LittleEndian(singleLong)
                };
            }

            return string.Join(", ", elements.ToArray());
        }
    }

    /// <summary>
    ///     为静态负载长度的标签解析值
    /// </summary>
    /// <code>
    ///  负责：Byte, Short, Int, Long, Float, Double
    ///  </code>
    /// <returns>值的字符串</returns>
    /// <exception cref="Exception">当前标签不是静态负载长度标签</exception>
    private string ParseConstValue()
    {
        var info = NbtGlobal.ByteToInfo[(byte)Tag];
        var nameLength = GetNameLength(); // 若标签是列表元素，该方法返回 0
        var dataBegin = IsListDirectElement switch
        {
            true => 0, // 列表<静态负载长度>中，元素名称、长度均不存储 (差别: 见 ParseDynamicValue 方法)
            false => NbtGlobal.NameLengthFieldSize + nameLength + 1
        };
        var data = _bytes.Span.Slice(dataBegin, info.fieldSize);
        return Tag switch
        {
            NbtTagEnum.Byte => data[0].ToString(),
            NbtTagEnum.Short => _isBigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(data).ToString()
                : BinaryPrimitives.ReadInt16LittleEndian(data).ToString(),
            NbtTagEnum.Int => _isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(data).ToString()
                : BinaryPrimitives.ReadInt32LittleEndian(data).ToString(),
            NbtTagEnum.Long => _isBigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(data).ToString()
                : BinaryPrimitives.ReadInt64LittleEndian(data).ToString(),
            NbtTagEnum.Float => _isBigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(data).ToString()
                : BinaryPrimitives.ReadSingleLittleEndian(data).ToString(),
            NbtTagEnum.Double => _isBigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(data).ToString()
                : BinaryPrimitives.ReadDoubleLittleEndian(data).ToString(),
            _ => throw new Exception("非静态负载长度")
        };
    }

    /// <summary>
    ///     获取标签名称长度
    /// </summary>
    /// <returns>长度</returns>
    private int GetNameLength()
    {
        if (IsListDirectElement) return 0; // 列表元素没有名称
        var nameLengthField = _bytes.Span.Slice(1, NbtGlobal.NameLengthFieldSize);
        return _isBigEndian switch
        {
            true => BinaryPrimitives.ReadInt16BigEndian(nameLengthField),
            false => BinaryPrimitives.ReadInt16LittleEndian(nameLengthField)
        };
    }

    /// <summary>
    ///     打印自身及子项组成的树状结构
    /// </summary>
    /// <param name="indent">[请忽略]</param>
    /// <param name="isLast">[请忽略]</param>
    public void PrintTree(string indent = "", bool isLast = true)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(indent);
        if (isLast)
        {
            Console.Write("└── ");
            PrintTag();
            indent += "".PadRight(4);
        }
        else
        {
            Console.Write("├── ");
            PrintTag();
            indent += "|".PadRight(4);
        }

        for (var i = 0; i < Children.Count; i++) Children[i].PrintTree(indent, i == Children.Count - 1);
        Console.ResetColor();

        return;

        void PrintTag()
        {
            Console.ForegroundColor = NbtGlobal.EnumToColor[Tag];
            // 名称
            Console.Write(string.IsNullOrEmpty(_name) ? Tag : _name);

            // 子元素数量(列表或字典)
            if (Tag is NbtTagEnum.List or NbtTagEnum.Dictionary)
            {
                Console.WriteLine($"<{Children.Count}>");
                return;
            }

            // 值
            var tagColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(" = ");
            Console.ForegroundColor = tagColor;
            // 构造方法执行 GetValue() 第一步设置 string.Empty, _value 必定不是 null
            Console.WriteLine(_value!.Length <= 50 ? _value : _value[..50] + " ...");
        }
    }
}