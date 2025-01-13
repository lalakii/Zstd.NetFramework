# Zstd.NetFramework
[![Available on NuGet https://www.nuget.org/packages?q=Zstd.NetFramework](https://img.shields.io/nuget/v/Zstd.NetFramework.svg?style=flat-square)](https://www.nuget.org/packages?q=Zstd.NetFramework)

This is a Wrappers based on P/Invoke implementations.

Functions implemented by calling [facebook/zstd](https://github.com/facebook/zstd)(v1.5.6).

## API
```cs
Zstd.Compress(byte[] src, int level)

Zstd.Decompress(byte[] src, int dstCapacity)

Zstd.CompressStream(Stream src, Stream dst, int level)

Zstd.DecompressStream(Stream src, Stream dst)

Zstd.CompressStream2SimpleArgs(Stream src, Stream dst, int level)

Zstd.DecompressStreamSimpleArgs(Stream src, Stream dst)
```

## Demo
```cs
using CN.Lalaki.Zstd;
using System.IO;
using System;
// ...sample code
using (var cstream = File.OpenRead("path\\of\\example.zst"))
{
    MemoryStream destream = new MemoryStream();
    // Decompress Stream
    Zstd.DecompressStream(cstream, destream);
    var c2stream = new MemoryStream();
    destream.Seek(0, SeekOrigin.Begin);
    // Compress Stream
    Zstd.CompressStream(destream, c2stream, 3);
    // Decompress Byte Array
    const int bufferSize = 1024 * 1024 * 20; // if 20MB, must be larger than original file size
    var ddata = Zstd.Decompress(c2stream.ToArray(), bufferSize);
    Console.WriteLine("Decompress Size: {0}", ddata.Length);
    // Compress Byte Array
    var cdata = Zstd.Compress(ddata, 3);
    Console.WriteLine("Compress Size {0}", cdata.Length);
}
```
## License
[MIT](LICENSE)
