# Introduction
This is a tool that helps loading & construct tree-structred Minecraft NBT files in C# projects.

# Process
- ✅ Loading
- ✅ Construct Tree-structred 
- ⬜ Change
- ⬜ Delete
- ⬜ Add
- ⬜ Deserialize

# Using
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

3. Print tree-structred

```Cs
treeTag.PrintTree();
```

# Examples
1. map.nbt (Java)
  !(level.nbt)[Images/map.nbt]
2. level.dat (Bedrock)
 !(level.nbt)[Images/level.dat]
3. litematica.nbt
 !(level.nbt)[Images/litematica.nbt]
