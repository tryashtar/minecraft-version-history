## 0.6.4 (fNbt)
- Fixed a case where NbtBinaryReader.ReadString read too many bytes (#26).
- Fixed NbtList.Contains(null) throwing exception instead of returning false.
- Reduced NbtBinaryReader's maximum chunk size to 4 MB. This reduces peak
    memory use when reading huge files without affecting performance.

## 0.6.3 (fNbt)
- Empty NbtLists now allow "TAG_End" as its ListType (#12).

## 0.6.2 (fNbt)
- NbtTags now implement ICloneable and provide copy constructors (#10).
- fNbt is now compatible with /checked compiler option.
- Fixed an OverflowException in .NET 4.0+ when writing arrays of size 1 GiB
	(or larger) to a BufferedStream.
- Fixed a few edge cases in NbtReader when reading corrupt files.
- Minor optimizations and documentation fixes.

## 0.6.1 (fNbt)
- NbtReader now supports non-seekable streams.
- Fixed issues loading from/saving to non-seekable steams in NbtFile.
- NbtFile.LoadFromStream/SaveToStream now accurately report bytes read/written
    for NBT data over 2 GiB in size.
- API change:
    All NbtFile loading/saving methods now return long instead of int.

## 0.6.0 (fNbt)
- Raised .NET framework requirements from 2.0+ to 3.5+
- Added NbtWriter, for linearly writing NBT streams, similarly to XmlWriter.
    It enables high-performance writing, without creating temp NbtTag objects.
- Fixed handling of lists-of-lists and lists-of-compound-tags in NbtReader.
- Fixed being able to add an NbtList to itself.
- API changes:
    Removed NbtCompound.ToArray(), use NbtCompound.Tags.ToArray() instead.
    Removed NbtCompound.ToNameArray(), use NbtCompound.Names.ToArray() instead.
- Improved tag reading and writing performance.
- Expanded unit test coverage.

## 0.5.1 (fNbt)
- Fixed ToString() methods of NbtReader and some NbtTags not respecting the
    NbtTag.DefaultIndentString setting.
- Fixed being able to add a Compound tag to itself.
- Fixed NbtString value defaulting to null, instead of an empty string.
- Fixed a number of bugs in NbtReader.ReadListAsArray<T>().
- API additions:
    New NbtReader property:     bool IsAtStreamEnd
    New NbtReader overload:     string ToString(bool,string)
- Expanded unit test coverage.

## 0.5.0 (fNbt)
- Added NbtReader, for linearly reading NBT streams, similarly to XmlReader.
- API additions:
    New NbtCompound method:     bool TryGet(string,out NbtTag)
    New NbtCompound overload:   NbtTag Get(string)
    New NbtTag property:        bool HasValue
- License changed from LGPL to to 3-Clause BSD, since none of the original
    libnbt source code remains.

## 0.4.1 (LibNbt2012)
- Added a way to set up default indent for NbtTag.ToString() methods, using
    NbtTag.DefaultIndentString static property.
- Added a way to control/disable buffering when reading tags, using properties
    NbtFile.DefaultBufferSize (static) and "nbtFile.BufferSize" (instance).
- Simplified renaming tags. Instead of using NbtFile.RenameRootTag or
    NbtCompound.RenameTag, you can now set tag's Name property directly. It
    will check parent tag automatically, and throw ArgumentException or
    ArgumentNullException is renaming is not possible.
- NbtFile() constructor now initializes RootTag to an empty NbtCompound("").
- Added LoadFro* overloads that do not require a TagSelector parameter.

## 0.4.0 (LibNbt2012)
- Changed the way NbtFiles are constructed. Data is not loaded in the
    constructor itself any more, use LoadFrom* method.
- Added a way to load NBT data directly from byte arrays, and to save them to
    byte arrays.
- All LoadFrom-/SaveTo- methods now return an int, indicating the number of
    bytes read/written.
- Updated NbtFile to override ToString.
- Added a way to control endianness when reading/writing NBT files.

## 0.3.4 (LibNbt2012)
- Added a way to rename tags inside NbtCompound and NbtFile.

## 0.3.3 (LibNbt2012)
- Added a way to skip certain tags at load-time, using a TagSelector callback.

## 0.3.2 (LibNbt2012)
- Added a way to easily identify files, using static NbtFile.ReadRootTagName.
- Added NbtTag.Parent (automatically set/reset by NbtList and NbtCompound).
- Added NbtTag.Path (which includes parents' names, and list indices).
- Added NbtCompound.Names and NbtCompound.Values enumerators.

## 0.3.1 (LibNbt2012)
- Added indexers to NbtTag base class, to make nested compound/list tags easier
    to work with.
- Added shortcut properties for getting tag values.
- Added a ToArray<T>() overload to NbtList, to automate casting to a specific
    tag type.
- Improved .ToString() pretty-printing, now with consistent and configurable
    indentation.

## 0.3.0 (LibNbt2012)
- Auto-detection of NBT file compression.
- Loading and saving of ZLib (RFC-1950) compresessed NBT files.
- Reduced loading/saving CPU use by 15%, and memory use by 40%
- Full support for TAG_Int_Array
- NbtCompound now implements ICollection and ICollection<NbtTag>
- NbtList now implements IList and IList<NbtTag>
- More constraint checks to tag loading, modification, and saving.
- Replaced getter/setter methods with properties, wherever possible.
- Expanded unit test coverage.
- Fully documented everything.
- Made tag names immutable.
- Removed tag queries.

## 0.2.0 (libnbt)
- Implemented tag queries.
- Created unit tests for the larger portions of the code.
- Marked tag constructors that take only tag values as obsolete, use the
    constructor that has name and value instead.

## 0.1.2 (libnbt)
- Added a GetTagType() function to the tag classes.
- Fixed saving NbtList tags.

## 0.1.1 (libnbt)
- Initial release.
- Modified the tag constructors to be consistant with each other.
- Changed NbtFile to allow some functions to be overridden.
