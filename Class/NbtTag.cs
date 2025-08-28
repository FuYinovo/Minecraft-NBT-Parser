using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using NBT_Parser.Enum;
using NBT_Parser.Record;
using NBT_Parser.Utils;

namespace NBT_Parser.Class
{
    /// <summary> 属性、构造方法、克隆方法、 </summary>
    public partial class NbtTag : ICloneable
    {
        public readonly NbtTagEnum ChildrenTag;
        public readonly NbtTagEnum Tag;
        public string? Name;
        public object? Value;
        public bool IsBigEndian;
        public bool IsListDirectElement; // 便于构造树形结构，避免单元素(伪)列表)
        private static readonly int[] ZeroBeginArray = [0];
        private Memory<byte> _bytes; // 不包含子元素 (终止于「首个子元素头部 - 1」)
        internal List<NbtTag> Children;
        public bool IsRemoved { get; private set; }

        /// <summary>
        ///     由字节集合构造 NBT 标签
        /// </summary>
        /// <remarks>请使用 NBT_Parser.Class.NbtTagBuilder 构造 NBT 标签</remarks>
        internal NbtTag(NbtTagEnum tag,
            Memory<byte> bytes,
            bool isBigEndian,
            List<NbtTag>? children = null,
            NbtTagEnum childrenTag = NbtTagEnum.Unknown,
            bool isListDirectElement = false)
        {
            _bytes = bytes;
            Children = children ?? [];
            ChildrenTag = childrenTag;
            IsBigEndian = isBigEndian;
            Tag = tag;
            IsListDirectElement = isListDirectElement;

            Name = Tag == NbtTagEnum.End || IsListDirectElement
                ? null
                : DeserializeName();

            Value = Tag == NbtTagEnum.End || NbtTagEnumExtensions.IsCollection(Tag)
                ? null
                : NbtGlobal.ByteToInfo[(byte)Tag].isDynamic
                    ? DeserializeDynamicValue()
                    : DeserializeConstValue();
        }

        /// <summary>
        ///     由名称、值构造 NBT 标签
        /// </summary>
        /// <remarks>请使用 NBT_Parser.Class.NbtTagBuilder 构造 NBT 标签</remarks>
        internal NbtTag(NbtTagEnum tag,
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
            IsBigEndian = isBigEndian;
            Tag = tag;
            IsListDirectElement = isListDirectElement;
        }


        /// <summary>
        ///     拷贝自身
        /// </summary>
        public object Clone()
        {
            var childrenCopy = Children.Select(child => (NbtTag)child.Clone()).ToList();
            return new NbtTag(Tag, IsBigEndian, Name, Value, childrenCopy, ChildrenTag, IsListDirectElement);
        }
    }

    /// <summary> 序列化方法、 </summary>
    public partial class NbtTag
    {
        /// <summary>
        ///     序列化
        /// </summary>
        /// <returns>标签的字节集合</returns>
        private Memory<byte> Serialize()
        {
            var bytes = new List<byte>(48);
            // 1. 标签ID段
            if (!IsListDirectElement || Tag == NbtTagEnum.End) bytes.Add((byte)Tag);
            // 2. 名称长度段及名称段
            bytes.AddRange(SerializeName());
            // 3. 负载长度段及负载段
            bytes.AddRange(NbtGlobal.ByteToInfo[(byte)Tag].isDynamic ? SerializeDynamicValue() : SerializeConstValue());

            return bytes.ToArray();
        }

        /// <summary>
        ///     序列化名称
        /// </summary>
        /// <returns>名称长度段和名称段的字节数组</returns>
        private byte[] SerializeName()
        {
            if (Name is null || IsListDirectElement) return [];

            // 名称长度字段
            var nameLengthField = BitConverter.GetBytes((short)Name.Length);
            // 名称字段
            var nameField = Encoding.UTF8.GetBytes(Name);

            return IsBigEndian
                ? nameLengthField.Reverse().Concat(nameField).ToArray()
                : nameLengthField.Concat(nameField).ToArray();
        }

        /// <summary>
        ///     序列化值(静态)
        /// </summary>
        /// <returns>内容的字节数组</returns>
        /// <exception cref="Exception">不是静态负载长度标签</exception>
        private byte[] SerializeConstValue()
        {
            if (NbtGlobal.ByteToInfo[(byte)Tag].isDynamic)
                throw new Exception($"序列化失败: [{Tag}]不是静态负载长度标签!");
            if (Value is null) return [];

            // 值字段（不使用 switch 避免均匹配到到 Double 类型）
            byte[] valueField = [];
            if (Tag == NbtTagEnum.Byte) valueField = [(byte)Value];
            if (Tag == NbtTagEnum.Int) valueField = BitConverter.GetBytes((int)Value);
            if (Tag == NbtTagEnum.Short) valueField = BitConverter.GetBytes((short)Value);
            if (Tag == NbtTagEnum.Float) valueField = BitConverter.GetBytes((float)Value);
            if (Tag == NbtTagEnum.Long) valueField = BitConverter.GetBytes((long)Value);
            if (Tag == NbtTagEnum.Double) valueField = BitConverter.GetBytes((double)Value);

            return IsBigEndian
                ? valueField.Reverse().ToArray()
                : valueField;
        }

        /// <summary>
        ///     序列化值(动态)
        /// </summary>
        /// <returns>内容的字节数组</returns>
        /// <exception cref="Exception">不是静态动态长度标签</exception>
        private byte[] SerializeDynamicValue()
        {
            if (!NbtGlobal.ByteToInfo[(byte)Tag].isDynamic)
                throw new Exception($"序列化失败: [{Tag}]不是动态负载长度标签!");

            #region 列表标签

            if (Tag == NbtTagEnum.List)
            {
                // 子项类型字段
                var childrenTagField = (byte)ChildrenTag;
                // 子项数量字段
                var childrenCountField = BitConverter.GetBytes(Children.Count(child => !child.IsRemoved));

                return IsBigEndian
                    ? new[] { childrenTagField }.Concat(childrenCountField.Reverse()).ToArray()
                    : new[] { childrenTagField }.Concat(childrenCountField).ToArray();
            }

            #endregion 列表标签

            #region 非列表标签

            if (Value is null) return [];
            // 负载长度
            var length = Tag switch
            {
                NbtTagEnum.String => (short)Encoding.UTF8.GetBytes((string)Value).Length,
                NbtTagEnum.ByteArray => ((byte[])Value).Length,
                NbtTagEnum.IntArray => ((int[])Value).Length,
                NbtTagEnum.LongArray => ((long[])Value).Length,
                _ => throw new ArgumentException()
            };
            // 负载长度字段
            var lengthField = Tag switch
            {
                NbtTagEnum.String => BitConverter.GetBytes((short)length), // 防止 int 类型导致长度为 4 字节
                _ => BitConverter.GetBytes(length)
            };
            // 负载字段
            var valueField = Tag switch // 负载段
            {
                NbtTagEnum.String => Encoding.UTF8.GetBytes((string)Value),
                NbtTagEnum.ByteArray => (byte[])Value,
                NbtTagEnum.IntArray => ((int[])Value).SelectMany(BitConverter.GetBytes),
                NbtTagEnum.LongArray => ((long[])Value).SelectMany(BitConverter.GetBytes),
                _ => throw new ArgumentException()
            };

            return IsBigEndian
                ? lengthField.Reverse().Concat(valueField).ToArray()
                : lengthField.Concat(valueField).ToArray();

            #endregion 非列表标签
        }
    }

    /// <summary> 反序列化方法、 </summary>
    public partial class NbtTag
    {
        /// <summary>
        ///     反序列化名称
        /// </summary>
        private string DeserializeName()
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
        ///      反序列化值(动态)
        /// </summary>
        /// <code>
        ///  负责：ByteArray, String, IntArray, LongArray
        ///  </code>
        /// <exception cref="Exception">当前标签不是动态负载长度标签</exception>
        private object DeserializeDynamicValue()
        {
            // 元数据
            var info = NbtGlobal.ByteToInfo[(byte)Tag];
            if (!info.isDynamic) throw new Exception($"[{Tag}]不是动态负载长度标签!");
            var fieldSize = info.fieldSize;
            var dataLengthMulti = info.dataLengthMulti ??
                                  throw new Exception($"动态负载长度标签[{Tag}]没有在[NbtGlobal]中标记负载长度倍率!");

            // 名称长度
            var nameLength = GetNameLength();

            // 负载长度字段
            var dataLengthField = IsListDirectElement
                ? _bytes.Span[..fieldSize]
                : _bytes.Span.Slice(NbtGlobal.NameLengthFieldSize + nameLength + 1, fieldSize);

            // 负载长度
            var dataLength = fieldSize switch
            {
                2 => Tools.ReadBinaryNumber<short>(dataLengthField.ToArray(), IsBigEndian),
                4 => Tools.ReadBinaryNumber<int>(dataLengthField.ToArray(), IsBigEndian),
                _ => throw new ArgumentException() // 正常不可能报错
            };

            // 若长度为零，返回默认值（空白）
            if (dataLength == 0) return NbtTagEnumExtensions.GetDefaultValue(Tag);

            // 负载开头索引
            var dataBegin = IsListDirectElement switch
            {
                true => fieldSize, // 列表<动态负载长度>中，元素没有名称，但存储了长度 (差别: 见 ParseConstValue 方法)
                false => NbtGlobal.NameLengthFieldSize + fieldSize + nameLength + 1
            };

            // 负载字段
            var data = _bytes.Span.Slice(dataBegin, dataLength * dataLengthMulti).ToArray();

            // 负载
            return Tag switch
            {
                NbtTagEnum.ByteArray => data.ToArray(),
                NbtTagEnum.String => Encoding.UTF8.GetString(data),
                NbtTagEnum.IntArray => ParseArrayValue<int>(IsBigEndian ? data.Reverse().ToArray() : data),
                NbtTagEnum.LongArray => ParseArrayValue<long>(IsBigEndian ? data.Reverse().ToArray() : data),
                _ => throw new ArgumentOutOfRangeException()
            };

            T[] ParseArrayValue<T>(byte[] bytes) where T : struct
            {
                var array = new T[bytes.Length / Marshal.SizeOf<T>()];
                var source = bytes.ToArray();
                Buffer.BlockCopy(source, 0, array, 0, bytes.Length);
                return array;
            }
        }

        /// <summary>
        ///     反序列化值(静态)
        /// </summary>
        /// <code>
        ///  负责：byte, short, int, long, float, double
        ///  </code>
        /// <exception cref="Exception">当前标签不是静态负载长度标签</exception>
        private object DeserializeConstValue()
        {
            // 元数据
            var info = NbtGlobal.ByteToInfo[(byte)Tag];
            if (info.isDynamic) throw new Exception("非静态负载长度");
            var fieldSize = info.fieldSize;

            // 名称长度
            var nameLength = GetNameLength();

            // 负载字段开头索引
            var dataBegin = IsListDirectElement
                ? 0 // 列表<静态>的元素不存储名称、长度字段 (差别: 见 ParseDynamicValue 方法)
                : NbtGlobal.NameLengthFieldSize + nameLength + 1;

            // 负载字段
            var data = _bytes.Span.Slice(dataBegin, fieldSize).ToArray();

            // 负载（不使用 switch 避免均匹配到 Double 类型）
            if (Tag == NbtTagEnum.Byte) return data[0];
            if (Tag == NbtTagEnum.Short) return Tools.ReadBinaryNumber<short>(data, IsBigEndian);
            if (Tag == NbtTagEnum.Int) return Tools.ReadBinaryNumber<int>(data, IsBigEndian);
            if (Tag == NbtTagEnum.Float) return Tools.ReadBinaryNumber<float>(data, IsBigEndian);
            if (Tag == NbtTagEnum.Long) return Tools.ReadBinaryNumber<long>(data, IsBigEndian);
            if (Tag == NbtTagEnum.Double) return Tools.ReadBinaryNumber<double>(data, IsBigEndian);
            throw new ArgumentException();
        }


        /// <summary>
        ///     获取标签名称长度
        /// </summary>
        /// <remarks> 若标签是列表元素，该方法返回 0 </remarks>
        private int GetNameLength()
        {
            if (IsListDirectElement) return 0; // 列表元素没有名称
            var nameLengthField = _bytes.Span.Slice(1, NbtGlobal.NameLengthFieldSize);
            return Tools.ReadBinaryNumber<short>(nameLengthField.ToArray(), IsBigEndian);
        }
    }

    /// <summary> 自身、子项操作方法、 </summary>
    public partial class NbtTag
    {
        /// <summary>
        ///     添加子项
        /// </summary>
        /// <remarks>indexes 可以为空</remarks>
        /// <example>
        ///     假设有结构
        ///     <code>
        /// DictA{
        ///     DictB{ List[Int, Int] }
        ///     DictC{ IntArray }
        /// }
        ///
        /// </code>
        ///     若要向 DictC 添加 LongArray 标签
        ///     <code>
        /// DictA.AppendChild([1])
        /// </code>
        /// </example>
        /// <returns>自身</returns>
        /// <param name="child">子项</param>
        /// <param name="indexes">索引集合</param>
        /// <param name="begin">[忽略]</param>
        /// <param name="addedZero">[忽略]</param>
        public NbtTag AppendChild(NbtTag child, int[] indexes, int begin = 0, bool addedZero = false)
        {
            if (!addedZero) indexes = ZeroBeginArray.Concat(indexes).ToArray();
            var index = indexes[begin];
            try
            {
                if (indexes.Length == 0 || begin == indexes.Length - 1) return Append(child);
                Children[index].AppendChild(child, indexes, begin + 1, true);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new Exception($"[{Tag}][{Name}]没有下标为[{index}]的子项!");
            }

            return this;

            NbtTag Append(NbtTag childElement)
            {
                if (Tag == NbtTagEnum.List) childElement.IsListDirectElement = true;
                switch (Tag)
                {
                    case NbtTagEnum.List:
                        if (childElement.Tag != ChildrenTag)
                            throw new Exception($"列表<{ChildrenTag}>不能插入{childElement.Tag}元素!");
                        Children.Add(childElement);
                        break;
                    case NbtTagEnum.Dictionary:
                        Children.Insert(Children.Count - 1, childElement); // 插入到结束标签前
                        break;
                    default:
                        throw new Exception($"[{Tag}]不允许添加子项!");
                }

                return this;
            }
        }

        /// <summary>
        ///     获取子项的引用
        /// </summary>
        /// <remarks>indexes 不可为空</remarks>
        /// <example>
        ///     假设有结构
        ///     <code>
        /// DictA{
        ///     DictB{ List[Int, Int] }
        ///     DictC{ IntArray }
        /// }
        ///
        /// </code>
        ///     若要获取 IntArray 标签
        ///     <code>
        /// DictA.GetChild([1, 0])
        /// </code>
        /// </example>
        /// <param name="indexes">索引集合</param>
        /// <param name="begin">[忽略]</param>
        public NbtTag GetChild(int[] indexes, int begin = 0)
        {
            if (indexes.Length == 0) throw new Exception("获取子项引用时，索引不能为空!");
            var index = indexes[begin];
            try
            {
                return begin == indexes.Length - 1 ? Children[index] : Children[index].GetChild(indexes, begin + 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new Exception($"[{Tag}][{Name}]没有下标为[{index}]的子项!");
            }
        }

        /// <summary>
        ///     移除子项
        /// </summary>
        /// <remarks>indexes 不可为空，此方法仅将 IsRemoved 设为 true</remarks>
        /// <example>
        ///     假设有结构
        ///     <code>
        /// DictA{
        ///     DictB{ List[Int, Int] }
        ///     DictC{ IntArray }
        /// }
        ///
        /// </code>
        ///     若要移除 IntArray 标签
        ///     <code>
        /// DictA.RemoveChild([1, 0])
        /// </code>
        /// </example>
        /// <returns>自身</returns>
        /// <param name="indexes">索引集合</param>
        /// <param name="begin">[忽略]</param>
        public void RemoveChild(int[] indexes, int begin = 0)
        {
            if (indexes.Length == 0) throw new Exception("删除子项时，索引不能为空!");
            var index = indexes[begin];
            try
            {
                if (begin == indexes.Length - 1)
                {
                    if (Tag == NbtTagEnum.End) throw new Exception("不能移除结束标签!");
                    Children[index].IsRemoved = true;
                }
                else
                {
                    Children[index].RemoveChild(indexes, begin + 1);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new Exception($"[{Tag}][{Name}]没有下标为[{index}]的子项!");
            }
        }

        /// <summary>
        ///     移除自身
        /// </summary>
        /// <remarks>仅将 IsRemoved 设为 true </remarks>
        public void RemoveSelf()
        {
            IsRemoved = true;
        }
    }

    /// <summary> 数据操作方法、 </summary>
    public partial class NbtTag
    {
        /// <summary>
        ///     设置字节序
        /// </summary>
        /// <param name="isBigEndian">是否大端序</param>
        /// <returns>自身的引用</returns>
        public NbtTag SetEndianness(bool isBigEndian)
        {
            IsBigEndian = isBigEndian;
            return this;
        }


        /// <summary>
        ///     设置标签名称
        /// </summary>
        public NbtTag SetName(string? name)
        {
            if (IsListDirectElement && name is not null) throw new Exception("列表子元素不允许设置名称!");
            Name = name;
            return this;
        }

        /// <summary>
        ///     设置标签值
        /// </summary>
        /// <exception cref="InvalidCastException">非法值</exception>
        public NbtTag SetValue(object value)
        {
            var validType = NbtGlobal.ByteToInfo[(byte)Tag].dataType;
            if (validType is null) throw new Exception($"[{Tag}]应设置子项, 而非值!");
            var valueType = value.GetType();

            // 场景一：值类型与目标一致
            if (valueType == validType)
            {
                Value = value;
                return this;
            }

            // 场景二：值类型与目标类型不一致
            var exceptionMsg = $"[{Tag}]的值不能设为[{valueType}]!";
            try
            {
                Value = Tag switch
                {
                    // 情况一：值可以转换为数组
                    // Tip: 元素 => <T>, IEnumerable => Array
                    NbtTagEnum.ByteArray => (value as IEnumerable)?.Cast<byte>().ToArray(),
                    NbtTagEnum.IntArray => (value as IEnumerable)?.Cast<int>().ToArray(),
                    NbtTagEnum.LongArray => (value as IEnumerable)?.Cast<long>().ToArray(),
                    // 情况二：值不合法
                    _ => throw new InvalidCastException(exceptionMsg)
                };
            }
            catch (Exception)
            {
                throw new InvalidCastException(exceptionMsg);
            }

            return this;
        }


        /// <summary>
        ///     以自身为根节点，获取自身及所有子元素的字节集合
        /// </summary>
        /// <remarks>IsRemoved 为 true 的 NBT 标签除外</remarks>
        public byte[] GetBytes()
        {
            if (IsRemoved) return [];
            _bytes = Serialize();
            var bytes = _bytes.ToArray().ToList();
            if (Tag is NbtTagEnum.Dictionary && IsListDirectElement) bytes.Clear();
            foreach (var child in Children) bytes.AddRange(child.GetBytes());

            return bytes.ToArray();
        }
    }

    /// <summary> 娱乐方法 </summary>
    public partial class NbtTag
    {
        /// <summary>
        ///     打印彩色树状图
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
                if (Tag == NbtTagEnum.End)
                {
                    Console.WriteLine();
                    return;
                }

                var tagColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(" = ");
                Console.ForegroundColor = tagColor;
                var valueString = Value switch
                {
                    null => "",
                    string => Value.ToString(),
                    IEnumerable enumerable => "[ " + string.Join(", ", enumerable.Cast<object>()) + " ]",
                    _ => Value.ToString()
                };
                valueString ??= "";
                Console.WriteLine(valueString.Length <= 50 ? valueString : valueString[..50] + " ...");
            }
        }
    }
}