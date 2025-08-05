# Introduction

A tool that helps loading, creating & editing Minecraft NBT files in C# projects.

# Process

- ✅ Serialize
- ✅ Construct Tree-structured
- ✅ Deserialize
- ✅ Create
- ⬜ Delete
- ✅ Change

# Using

### Parse NBT File

1. Load NBT file as a `byte[]`

```Cs
public static byte[] ReadBytes(string path)
    {
        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var binaryReader = new BinaryReader(fileStream);
        if (fileStream.Length > int.MaxValue) throw new Exception("Not Supported for length above Int32!");
        return binaryReader.ReadBytes((int)fileStream.Length);
    }
```

2. New a `NbtParser` then use `Parse()`

```Cs
using NBT_Parser.Class
// Tip: In bedrock Edition, "isBigEndian" should be false, and begins at 8
var treeTag = new NbtParser().Parse(bytes, true, 0);

// In bedrock Edition, NBT file start with 2 intgers, the second one is length of NBT file.
// So we can check if the it equals length of NBT file
// See more at Minecraft Wiki 
private static (int begin, bool isBigEndian) GetNbtBytesInfo(byte[] bytes)
    {
        var sizeField = bytes.AsSpan(4, 4);
        var sizeLittleEndian = BinaryPrimitives.ReadInt32LittleEndian(sizeField);
        if (sizeLittleEndian + 8 == bytes.Length) return (8, false);
        return (0, true);
    }
```

3. Why not try it

```Cs
treeTag.PrintTree();
```

### Get NBT-Tag data

Here are public attributes of `NbtTag`

```Cs
public readonly NbtTagEnum Tag;
public readonly List<NbtTag> Children;
public readonly NbtTagEnum ChildrenTag;
public string? Name; // Elements in a List-Tag have no name
public object? Value; // Dictionary & List have no value, only children
```

### Create NBT-Tag

You can easily create NBT-Tag by `NbtTagBuilder`

1. New a `NbtTagBuilder`

```csharp
using NBT_Parser.Class;

// The 1st param: bool isBigEndian
// The 2nd param: bool isClone(clone children or not when create a list or dictionary)
var builder = new NbtTagBuilder(true, true); 
```

2. Create NBT-Tags

```csharp
var pos = builder.IntArray("position", [-64, 128, 255]);
var id = builder.Int("id", 42);
var entity = builder.Dictionary(null, [pos, id]);
var entities = builder.List("entities", [entity, entity, entity]);
var dim = builder.String("dimension", "minecraft:overworld");
var dict = builder.Dictionary("root", [entities, dim]);

dict.PrintTree(); 
``` 

# Gallery

![map.nbt](Images/map.nbt.png)