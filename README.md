# Introduction

A tool that helps loading, creating & editing Minecraft NBT files in C# projects.

# Process

- ✅ Serialize
- ✅ Construct Tree-structured
- ✅ Deserialize
- ✅ Create
- ✅ Delete
- ✅ Change

# Using

## Parse

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

## Self

### Get

Here are public properties of `NbtTag`

```Cs
public readonly NbtTagEnum ChildrenTag;
public readonly bool IsBigEndian;
public bool IsRemoved;
public string? Name; // Elements in a List-Tag have no name
public object? Value; // Dictionary & List have no value, only children
public readonly NbtTagEnum Tag;
public readonly List<NbtTag> Children;
public readonly NbtTagEnum ChildrenTag;
```

### Create

You can easily create NBT-Tag by `NbtTagBuilder`

1. New a `NbtTagBuilder`

```csharp
using NBT_Parser.Class;

// The 1st param: bool isBigEndian
var builder = new NbtTagBuilder(true); 
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

### Delete

Here's an example to delete an NBT-Tag \
Actually it won't be deleted, but property `IsRemoved` will be `true`

```csharp
tag.RemoveSelf();
```

### SetName

Here's an example to rename an NBT-Tag

```csharp
var tag = builder.String("nameA", "str");
tag.SetName("nameB");
```

### SetValue

Here are two examples to set another value for an NBT-Tag

```csharp
var tagA = builder.Int("number", 24);
tagA.SetValue(64);    // OK
tagA.SetValue("64");  // InvalidCastException

var tagB = builder.IntArray("numbers", [1, 2, 3]);
tagB.SetValue([10, 20, 30])                // OK;
tagB.SetValue(new List<int>(){10, 20, 30}) // OK;
```

## Child

Assume we have a structure like this

 ```json
 {
   "Data": {
     "Item": {
       "Name": "Apple",
       "Count": 64
     }
   }
 }
 ```

### Get

Here is an example to get `Count` of `Item`

```csharp
var appleCount = Data.GetChild([0, 1]);
```

### Append

Here are two examples to append `Type` to `Item`

1. Directly append

```csharp
Data.AppendChild(newChild, [0]) 
```        

2. Get parent tag & append

```csharp
var parent = Data.GetChild([0]);
parent.AppendChild(newChild, [])
```    

### Delete

Here is an example to delete `Count` Tag

```csharp
Data.DeleteChild([0, 1])
```

# Gallery

![map.nbt](Images/map.nbt.png)