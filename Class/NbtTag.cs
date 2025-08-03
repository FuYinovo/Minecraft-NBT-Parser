using System.Buffers.Binary;
using System.Collections;
using System.Text;
using NBT_Parser.Enum;
using NBT_Parser.Record;

namespace NBT_Parser.Class;

public class NbtTag
{
    private readonly bool _isBigEndian;
    private Memory<byte> _bytes; // 不包含子元素 (终止于「首个子元素头部 - 1」)
    private string? _name;
    private object? _value;
    private bool _isChanged;
    public readonly NbtTagEnum ChildrenTag;
    public readonly NbtTagEnum Tag;
    public List<NbtTag> Children;
    public bool IsListDirectElement; // 便于构造树形结构，避免单元素(伪)列表)

    /// <summary>
    /// 由字节集合构造 NBT 标签
    /// </summary>
    /// <remarks>请使用 NBT_Parser.Class.NbtTagBuilder 构造 NBT 标签</remarks>
    public NbtTag(NbtTagEnum tag,
        Memory<byte> bytes,
        bool isBigEndian,
        List<NbtTag>? children = null,
        NbtTagEnum childrenTag = NbtTagEnum.Unknown,
        bool isListDirectElement = false)
    {
        _bytes = bytes;
        Children = children ?? [];
        ChildrenTag = childrenTag;
        _isBigEndian = isBigEndian;
        Tag = tag;
        IsListDirectElement = isListDirectElement;

        _name = Tag switch
        {
            NbtTagEnum.End => null,
            _ => IsListDirectElement ? null : ParseName()
        };
        _value = Tag switch
        {
            NbtTagEnum.End or NbtTagEnum.Dictionary or NbtTagEnum.List => null,
            _ => ParseValue()
        };
    }

    /// <summary>
    /// 由名称、值构造 NBT 标签
    /// </summary>
    /// <remarks>请使用 NBT_Parser.Class.NbtTagBuilder 构造 NBT 标签</remarks>
    public NbtTag(NbtTagEnum tag,
        bool isBigEndian,
        string? name = null,
        object? value = null,
        List<NbtTag>? children = null,
        NbtTagEnum childrenTag = NbtTagEnum.Unknown,
        bool isListDirectElement = false)
    {
        _name = name;
        _value = value;
        Children = children ?? [];
        ChildrenTag = childrenTag;
        _isBigEndian = isBigEndian;
        Tag = tag;
        IsListDirectElement = isListDirectElement;
        _isChanged = true;
    }

    /// <summary>
    ///     获取标签名称
    /// </summary>
    public string? GetName()
    {
        return _name;
    }

    /// <summary>
    ///     获取标签值
    /// </summary>
    /// <code>
    ///  返回值：string, byte, short, int, long, float, double, byte[], int[], long[]
    ///  </code>
    public object? GetValue()
    {
        return Tag switch
        {
            NbtTagEnum.Float => float.Parse(_value?.ToString() ?? string.Empty),
            NbtTagEnum.Double =>
                double.Parse(_value?.ToString() ?? string.Empty),
            _ => _value
        };
    }

    /// <summary>
    /// 设置标签名称
    /// </summary>
    public void SetName(string name)
    {
        _name = name;
        _isChanged = true;
    }

    /// <summary>
    /// 设置标签值
    /// </summary>
    /// <exception cref="InvalidCastException">非法值</exception>
    public void SetValue(object value)
    {
        var validDataType = NbtGlobal.ByteToInfo[(byte)Tag].dataType;
        switch (value)
        {
            case IEnumerable enumerable:
                // 检查合法性
                var array = enumerable.Cast<object>().ToArray();
                if (array.GetType() != validDataType)
                    throw new InvalidCastException($"[{Tag}]的值不能设为{array.GetType()}!");
                // 设置值
                _value = array;
                break;
            default:
                // 检查合法性
                if (value.GetType() != validDataType)
                    throw new InvalidCastException($"[{Tag}]的值不能设为{value.GetType()}!");
                // 设置值
                _value = value;
                break;
        }

        _isChanged = true;
    }

    public byte[] GetBytesTree()
    {
        var bytes = _bytes.ToArray().ToList();
        if (Tag is NbtTagEnum.Dictionary && IsListDirectElement) bytes.Clear();
        foreach (var child in Children)
        {
            bytes.AddRange(child.GetBytesTree());
        }

        return bytes.ToArray();
    }


    /// <summary>
    /// 解析标签名称
    /// </summary>
    private string ParseName()
    {
        try
        {
            var nameLength = GetNameLength();
            if (nameLength == 0) return string.Empty; // 若名称长度为零，返回空白
            var nameField = _bytes.Span.Slice(NbtGlobal.NameLengthFieldSize + 1, nameLength);
            return Encoding.UTF8.GetString(nameField);
        }
        catch (Exception)
        {
            return Tag.ToString();
        }
    }

    private object ParseValue()
    {
        try
        {
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
    /// <exception cref="Exception">当前标签不是动态负载长度标签</exception>
    private object ParseDynamicValue()
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
                return data.ToArray();
            case NbtTagEnum.String:
                return Encoding.UTF8.GetString(data);
            case NbtTagEnum.IntArray:
                return ConstructArray(data, 4);
            case NbtTagEnum.LongArray:
                return ConstructArray(data, 8);
            default:
                throw new Exception($"[{Tag}]不是动态负载长度标签!");
        }

        Array ConstructArray(Span<byte> bytes, object type)
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

            return elements.ToArray();
        }
    }

    /// <summary>
    ///     为静态负载长度的标签解析值
    /// </summary>
    /// <code>
    ///  负责：byte, short, int, long, float, double
    ///  </code>
    /// <exception cref="Exception">当前标签不是静态负载长度标签</exception>
    private object ParseConstValue()
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
            NbtTagEnum.Byte => data[0],
            NbtTagEnum.Short => _isBigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(data)
                : BinaryPrimitives.ReadInt16LittleEndian(data),
            NbtTagEnum.Int => _isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(data)
                : BinaryPrimitives.ReadInt32LittleEndian(data),
            NbtTagEnum.Long => _isBigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(data)
                : BinaryPrimitives.ReadInt64LittleEndian(data),
            NbtTagEnum.Float => _isBigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(data).ToString() // ToString() 避免精度误差
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
    /// <remarks>请忽略参数</remarks>
    public void PrintTree(string indent = "", bool isLast = true)
    {
        if (Tag == NbtTagEnum.End) return;

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
            var valueString = _value switch
            {
                null => "",
                string => _value.ToString(),
                IEnumerable enumerable => "[ " + string.Join(", ", enumerable.Cast<object>()) + " ]",
                _ => _value.ToString()
            };
            valueString ??= "";
            Console.WriteLine(valueString.Length <= 50 ? valueString : (valueString[..50]) + " ...");
        }
    }
}