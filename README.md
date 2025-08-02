# Introduction

This is a tool that helps loading & construct tree-structured Minecraft NBT files in C# projects.

# Process

- ✅ Loading
- ✅ Construct Tree-structured
- ⬜ Change
- ⬜ Delete
- ⬜ Add
- ⬜ Deserialize

# Using

## Print tree-structured

1. Load NBT file as a **byte Array**

```Cs
public static byte[] ReadBytes(string path)
    {
        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var binaryReader = new BinaryReader(fileStream);
        if (fileStream.Length > int.MaxValue) throw new Exception("不支持超过 Int32 长度文件!");
        return binaryReader.ReadBytes((int)fileStream.Length);
    }
```

2. New a **NbtParser** then use **Parse()**

```Cs
using NBT_Parser
// Tip: In bedrock Edition, "isBigEndian" should be false, and begins at 8
var treeTag = new NbtParser().Parse(bytes, true, 0);
```

3. Print tree-structured

```Cs
treeTag.PrintTree();
```

# Get NBT tag data

The **NbtTag** object have 3 public Attribute:
```Cs
public readonly NbtTagEnum Tag;
public readonly List<NbtTag> Children;
public readonly NbtTagEnum ChildrenTag;
```
and 2 public functions:
```Cs
public string GetValue()
public string GetName()
```

You can easily get data like this:
```csharp
nbtTag.Children[0].GetName();
nbtTag.Children[0].GetValue();
```

# Examples

1. map.nbt (Java)
   ![](Images/map.nbt.png)
2. level.dat (Bedrock)
   ![](Images/level.dat.png)
3. litematica
   ![](Images/litematica.png)
