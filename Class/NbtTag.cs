using System.Collections;
using System.Text;
using NBT_Parser.Enum;
using NBT_Parser.Record;
using NBT_Parser.Utils;

namespace NBT_Parser.Class;

public class NbtTag
{
    private readonly bool _isBigEndian;
    public readonly NbtTagEnum ChildrenTag;
    public readonly NbtTagEnum Tag;
    private Memory<byte> _bytes; // 不包含子元素 (终止于「首个子元素头部 - 1」)
    private string _floatValueTemp = string.Empty;
    private bool _isChanged;
    public List<NbtTag> Children;
    public bool IsListDirectElement; // 便于构造树形结构，避免单元素(伪)列表)
    public string? Name;
    public object? Value;

    /// <summary>
    ///     由字节集合构造 NBT 标签
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

        Name = Tag switch
        {
            NbtTagEnum.End => null,
            _ => IsListDirectElement ? null : ParseName()
        };
        Value = Tag switch
        {
            NbtTagEnum.End or NbtTagEnum.Dictionary or NbtTagEnum.List => null,
            _ => ParseValue()
        };
    }

    /// <summary>
    ///     由名称、值构造 NBT 标签
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
        Name = name;
        Value = value;
        Children = children ?? [];
        ChildrenTag = childrenTag;
        _isBigEndian = isBigEndian;
        Tag = tag;
        IsListDirectElement = isListDirectElement;
        _isChanged = true;
    }

    /// <summary>
    ///     设置标签名称
    /// </summary>
    public void SetName(string? name)
    {
        Name = name;
        _isChanged = true;
    }

    /// <summary>
    ///     设置标签值
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
                Value = array;
                break;
            default:
                // 检查合法性
                if (value.GetType() != validDataType)
                    throw new InvalidCastException($"[{Tag}]的值不能设为{value.GetType()}!");
                // 设置值
                Value = value;
                break;
        }

        _isChanged = true;
    }

    /// <summary>
    ///     以自身为根节点，获取自身及所有子元素的字节集合
    /// </summary>
    /// <remarks>可直接保存为 NBT 文件</remarks>
    public byte[] GetBytesTree()
    {
        if (_isChanged) _bytes = Deserialize();
        var bytes = _bytes.ToArray().ToList();
        if (Tag is NbtTagEnum.Dictionary && IsListDirectElement) bytes.Clear();
        foreach (var child in Children) bytes.AddRange(child.GetBytesTree());

        return bytes.ToArray();
    }

    /// <summary>
    ///     反序列化
    /// </summary>
    /// <returns>标签的字节集合</returns>
    private Memory<byte> Deserialize()
    {
        var bytes = new List<byte>();
        // 1. 标签ID段
        switch (IsListDirectElement)
        {
            case false:
            case true when Tag == NbtTagEnum.End:
                bytes.Add((byte)Tag);
                break;
        }

        // 2. 名称长度段及名称段
        bytes.AddRange(DeserializeName());
        // 3. 负载长度段及负载段
        bytes.AddRange(NbtGlobal.ByteToInfo[(byte)Tag].isDynamic ? DeserializeDynamicValue() : DeserializeConstValue());
        return bytes.ToArray();
    }

    /// <summary>
    ///     反序列化名称
    /// </summary>
    /// <returns>名称长度段和名称段的字节数组</returns>
    private byte[] DeserializeName()
    {
        if (Name is null) return [];

        var bytes = new List<byte>();
        // 名称长度段
        var nameLength = (short)Name.Length;
        var nameLengthField = BitConverter.GetBytes(nameLength);
        bytes.AddRange(_isBigEndian ? nameLengthField.Reverse().ToArray() : nameLengthField);
        // 名称段
        var nameField = Encoding.UTF8.GetBytes(Name);
        bytes.AddRange(nameField);

        return bytes.ToArray();
    }

    /// <summary>
    ///     为静态负载长度的标签反序列化值
    /// </summary>
    /// <returns>内容的字节数组</returns>
    /// <exception cref="Exception">不是静态负载长度标签</exception>
    private byte[] DeserializeConstValue()
    {
        if (Value is null) return [];
        byte[] valueField = [];
        // 防止 switch 隐式将 2字节、4字节的类型匹配到 8字节的 Double
        if (Tag == NbtTagEnum.Byte) valueField = [(byte)Value];
        if (Tag == NbtTagEnum.Int) valueField = BitConverter.GetBytes((int)Value);
        if (Tag == NbtTagEnum.Short) valueField = BitConverter.GetBytes((short)Value);
        if (Tag == NbtTagEnum.Float) valueField = BitConverter.GetBytes((float)Value);
        if (Tag == NbtTagEnum.Long) valueField = BitConverter.GetBytes((long)Value);
        if (Tag == NbtTagEnum.Double) valueField = BitConverter.GetBytes((double)Value);

        if (valueField.Length > 0) return _isBigEndian ? valueField.Reverse().ToArray() : valueField;
        throw new Exception($"反序列化失败: [{Tag}]不是静态负载长度标签!");
    }

    /// <summary>
    ///     为动态负载长度的标签反序列化值
    /// </summary>
    /// <returns>内容的字节数组</returns>
    /// <exception cref="Exception">不是静态动态长度标签</exception>
    private byte[] DeserializeDynamicValue()
    {
        var bytes = new List<byte>();

        // 列表标签
        if (Tag == NbtTagEnum.List)
        {
            var childrenTagField = (byte)ChildrenTag;
            var childrenCountField = BitConverter.GetBytes(Children.Count);
            bytes.Add(childrenTagField);
            bytes.AddRange(_isBigEndian ? childrenCountField.Reverse() : childrenCountField);
            return bytes.ToArray();
        }

        // 非列表标签
        if (Value is null) return [];
        var length = Tag switch
        {
            NbtTagEnum.String => (short)((string)Value).Length,
            NbtTagEnum.ByteArray => ((byte[])Value).Length,
            NbtTagEnum.IntArray => ((int[])Value).Length,
            NbtTagEnum.LongArray => ((long[])Value).Length,
            _ => throw new Exception($"反序列化失败: [{Tag}]不是动态负载长度标签!")
        };
        var lengthField = BitConverter.GetBytes(length); // 负载长度段
        var valueField = Tag switch // 负载段
        {
            NbtTagEnum.String => Encoding.UTF8.GetBytes((string)Value),
            NbtTagEnum.ByteArray => (byte[])Value,
            NbtTagEnum.IntArray => ((int[])Value).SelectMany(BitConverter.GetBytes),
            NbtTagEnum.LongArray => ((long[])Value).SelectMany(BitConverter.GetBytes),
            _ => throw new Exception($"反序列化失败: [{Tag}]不是动态负载长度标签!")
        };
        bytes.AddRange(_isBigEndian ? lengthField.Reverse() : lengthField); // 大小端序反转
        bytes.AddRange(valueField);

        return bytes.ToArray();
    }

    /// <summary>
    ///     解析标签名称
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

    /// <summary>
    ///     解析标签值
    /// </summary>
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
            2 => Tools.ReadNumber<short>(dataLengthField.ToArray(), _isBigEndian),
            4 => Tools.ReadNumber<int>(dataLengthField.ToArray(), _isBigEndian),
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
                var intArray = new int[data.Length / 4];
                var intSource = data.ToArray();
                intSource = _isBigEndian ? intSource.Reverse().ToArray() : intSource;
                Buffer.BlockCopy(intSource, 0, intArray, 0, data.Length);
                return intArray;
            case NbtTagEnum.LongArray:
                var longArray = new long[data.Length / 8];
                var longSource = data.ToArray();
                longSource = _isBigEndian ? longSource.Reverse().ToArray() : longSource;
                Buffer.BlockCopy(longSource, 0, longArray, 0, data.Length);
                return longArray;
            default:
                throw new Exception($"[{Tag}]不是动态负载长度标签!");
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

        _floatValueTemp = Tag switch
        {
            NbtTagEnum.Float => Tools.ReadNumber<float>(data.ToArray(), _isBigEndian).ToString(),
            NbtTagEnum.Double => Tools.ReadNumber<double>(data.ToArray(), _isBigEndian).ToString(),
            _ => string.Empty
        };
        if (Tag == NbtTagEnum.Byte) return data[0];
        if (Tag == NbtTagEnum.Short) return Tools.ReadNumber<short>(data.ToArray(), _isBigEndian);
        if (Tag == NbtTagEnum.Int) return Tools.ReadNumber<int>(data.ToArray(), _isBigEndian);
        if (Tag == NbtTagEnum.Float) return Tools.ReadNumber<float>(data.ToArray(), _isBigEndian);
        if (Tag == NbtTagEnum.Long) return Tools.ReadNumber<long>(data.ToArray(), _isBigEndian);
        if (Tag == NbtTagEnum.Double) return Tools.ReadNumber<double>(data.ToArray(), _isBigEndian);
        throw new Exception("非静态负载长度");
    }

    /// <summary>
    ///     获取标签名称长度
    /// </summary>
    private int GetNameLength()
    {
        if (IsListDirectElement) return 0; // 列表元素没有名称
        var nameLengthField = _bytes.Span.Slice(1, NbtGlobal.NameLengthFieldSize);
        return Tools.ReadNumber<short>(nameLengthField.ToArray(), _isBigEndian);
    }

    /// <summary>
    ///     打印自身及子项组成的树状结构
    /// </summary>
    /// <param name="hideEnd">是否隐藏结束标签</param>
    /// <param name="indent">[忽略]</param>
    /// <param name="isLast">[忽略]</param>
    public void PrintTree(bool hideEnd = true, string indent = "", bool isLast = true)
    {
        if (Tag == NbtTagEnum.End && hideEnd) return;

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

        for (var i = 0; i < Children.Count; i++) Children[i].PrintTree(hideEnd, indent, i == Children.Count - 1);
        Console.ResetColor();

        return;

        void PrintTag()
        {
            Console.ForegroundColor = NbtGlobal.EnumToColor[Tag];
            // 名称
            Console.Write(string.IsNullOrEmpty(Name) ? Tag : Name);

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
            var valueString = Value switch
            {
                null => "",
                string => Value.ToString(),
                IEnumerable enumerable => "[ " + string.Join(", ", enumerable.Cast<object>()) + " ]",
                _ => Tag is NbtTagEnum.Float or NbtTagEnum.Double ? _floatValueTemp : Value.ToString()
            };
            valueString ??= "";
            Console.WriteLine(valueString.Length <= 50 ? valueString : valueString[..50] + " ...");
        }
    }
}