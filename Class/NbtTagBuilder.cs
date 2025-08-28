using NBT_Parser.Enum;

namespace NBT_Parser.Class
{
    public class NbtTagBuilder(bool isBigEndian)
    {
        public NbtTag Byte(string name, byte value)
        {
            return new NbtTag(NbtTagEnum.Byte, isBigEndian, name, value);
        }

        public NbtTag Short(string name, short value)
        {
            return new NbtTag(NbtTagEnum.Short, isBigEndian, name, value);
        }

        public NbtTag Int(string name, int value)
        {
            return new NbtTag(NbtTagEnum.Int, isBigEndian, name, value);
        }

        public NbtTag Long(string name, long value)
        {
            return new NbtTag(NbtTagEnum.Long, isBigEndian, name, value);
        }

        public NbtTag Float(string name, float value)
        {
            return new NbtTag(NbtTagEnum.Float, isBigEndian, name, value);
        }

        public NbtTag Double(string name, double value)
        {
            return new NbtTag(NbtTagEnum.Double, isBigEndian, name, value);
        }

        public NbtTag String(string name, string value)
        {
            return new NbtTag(NbtTagEnum.String, isBigEndian, name, value);
        }

        public NbtTag List(string name, List<NbtTag> children, NbtTagEnum childrenTag)
        {
            return CreateTag(CloneChildren(children));

            NbtTag CreateTag(List<NbtTag> childList)
            {
                foreach (var child in childList)
                {
                    child.IsListDirectElement = true;
                    child.SetName(null); // 列表子元素没有名称
                    if (child.Tag != childrenTag) throw new Exception($"[{childrenTag}]列表不允许[{child.Tag}]!");
                }

                return new NbtTag(NbtTagEnum.List, isBigEndian, name, null, childList, childrenTag);
            }
        }

        public NbtTag Dictionary(string? name, List<NbtTag> children)
        {
            return CreateTag(CloneChildren(children));

            NbtTag CreateTag(List<NbtTag> childList)
            {
                switch (childList.Count)
                {
                    case 0:
                        childList.Add(new NbtTag(NbtTagEnum.End, isBigEndian));
                        break;
                    default:
                        if (childList.Last().Tag != NbtTagEnum.End) childList.Add(new NbtTag(NbtTagEnum.End, isBigEndian));
                        break;
                }

                return new NbtTag(NbtTagEnum.Dictionary, isBigEndian, name, null, childList);
            }
        }

        public NbtTag ByteArray(string name, IEnumerable<byte> bytes)
        {
            return new NbtTag(NbtTagEnum.ByteArray, isBigEndian, name, bytes.ToArray());
        }

        public NbtTag IntArray(string name, IEnumerable<int> ints)
        {
            return new NbtTag(NbtTagEnum.IntArray, isBigEndian, name, ints.ToArray());
        }

        public NbtTag LongArray(string name, IEnumerable<long> longs)
        {
            return new NbtTag(NbtTagEnum.LongArray, isBigEndian, name, longs.ToArray());
        }

        private static List<NbtTag> CloneChildren(List<NbtTag> children)
        {
            return children.Select(child => (NbtTag)child.Clone()).ToList();
        }
    }
}