using System.Reflection;
using System.Runtime.InteropServices;
using size_t = System.UIntPtr;

namespace CN.Lalaki.Zstd
{
    public static class Zstd
    {
        private const string DllName = "libzstd.dll";
        private const string ZstdVersion = "zstd-v1.5.7-release";
        private static IntPtr hModule = IntPtr.Zero;

        static Zstd()
        {
            LoadLibrary();
        }

        public static byte[] Compress(byte[] src, int level)
        {
            if (src != null)
            {
                var len = (size_t)src.Length;
                var dst = new byte[src.Length];
                var res = ZSTD_compress(dst, len, src, len, level);
                if (ZSTD_isError(res) == 0)
                {
                    Array.Resize(ref dst, (int)res);
                    return dst;
                }
            }
            else
            {
                ThrowIfNull();
            }

            return [];
        }

        public static void CompressStream(Stream src, Stream dst, int level)
        {
            if (src != null && dst != null)
            {
                var zcs = ZSTD_createCStream();
                if (ZSTD_isError(ZSTD_initCStream(zcs, level)) == 0)
                {
                    var bufferOutSize = ZSTD_CStreamOutSize();
                    var bufferIn = new byte[(int)ZSTD_CStreamInSize()];
                    var bufferOut = new byte[(int)bufferOutSize];
                    var pBufferIn = Marshal.UnsafeAddrOfPinnedArrayElement(bufferIn, 0);
                    var pBufferOut = Marshal.AllocHGlobal(bufferOut.Length);
                    int bytesRead;
                    while ((bytesRead = src.Read(bufferIn, 0, bufferIn.Length)) > 0)
                    {
                        var inputBuffer = new ZstdBuffer { Buffer = pBufferIn, Size = (size_t)bytesRead };
                        var outputBuffer = new ZstdBuffer { Buffer = pBufferOut, Size = bufferOutSize };
                        if (ZSTD_isError(ZSTD_compressStream(zcs, ref outputBuffer, ref inputBuffer)) != 0)
                        {
                            Marshal.FreeHGlobal(pBufferOut);
                            throw new IOException();
                        }

                        var pos = (int)outputBuffer.Pos;
                        if (pos > 0)
                        {
                            Marshal.Copy(pBufferOut, bufferOut, 0, pos);
                            dst.Write(bufferOut, 0, pos);
                        }
                    }

                    ZstdBuffer bufferEnd = new()
                    {
                        Buffer = pBufferOut,
                        Size = bufferOutSize,
                    };
                    if (ZSTD_isError(ZSTD_endStream(zcs, ref bufferEnd)) == 0)
                    {
                        var pos = (int)bufferEnd.Pos;
                        if (pos > 0)
                        {
                            Marshal.Copy(pBufferOut, bufferOut, 0, pos);
                            dst.Write(bufferOut, 0, pos);
                        }
                    }

                    Marshal.FreeHGlobal(pBufferOut);
                }

                ZSTD_freeCStream(zcs);
            }
            else
            {
                ThrowIfNull();
            }
        }

        public static void CompressStream2SimpleArgs(Stream src, Stream dst, int level)
        {
            if (src != null && dst != null)
            {
                var zcs = ZSTD_createCStream();
                if (ZSTD_isError(ZSTD_initCStream(zcs, level)) == 0)
                {
                    var bufferOutSize = ZSTD_CStreamOutSize();
                    var bufferIn = new byte[(int)ZSTD_CStreamInSize()];
                    var bufferOut = new byte[(int)bufferOutSize];
                    int endOp = 0;
                    int bytesRead;
                    while ((bytesRead = src.Read(bufferIn, 0, bufferIn.Length)) > 0)
                    {
                        var srcPos = (size_t)0;
                        var dstPos = (size_t)0;
                        if (bytesRead < bufferIn.Length)
                        {
                            endOp = 2;
                        }

                        if (ZSTD_isError(ZSTD_compressStream2_simpleArgs(zcs, bufferOut, bufferOutSize, ref dstPos, bufferIn, (size_t)bytesRead, ref srcPos, endOp)) != 0)
                        {
                            throw new IOException();
                        }

                        var pos = (int)dstPos;
                        if (pos > 0)
                        {
                            dst.Write(bufferOut, 0, pos);
                        }
                    }
                }

                ZSTD_freeCStream(zcs);
            }
            else
            {
                ThrowIfNull();
            }
        }

        public static byte[] Decompress(byte[] src, int dstCapacity)
        {
            if (src != null)
            {
                var dst = new byte[dstCapacity];
                var res = ZSTD_decompress(dst, (size_t)dst.Length, src, (size_t)src.Length);
                if (ZSTD_isError(res) == 0)
                {
                    Array.Resize(ref dst, (int)res);
                    return dst;
                }
            }
            else
            {
                ThrowIfNull();
            }

            return [];
        }

        public static void DecompressStream(Stream src, Stream dst)
        {
            if (src != null && dst != null)
            {
                var zds = ZSTD_createDStream();
                if (ZSTD_isError(ZSTD_initDStream(zds)) == 0)
                {
                    var bufferOutSize = ZSTD_DStreamOutSize();
                    var bufferIn = new byte[(int)ZSTD_DStreamInSize()];
                    var bufferOut = new byte[(int)bufferOutSize];
                    var pBufferIn = Marshal.UnsafeAddrOfPinnedArrayElement(bufferIn, 0);
                    var pBufferOut = Marshal.AllocHGlobal(bufferOut.Length);
                    int bytesRead;
                    while ((bytesRead = src.Read(bufferIn, 0, bufferIn.Length)) > 0)
                    {
                        var inputBuffer = new ZstdBuffer
                        {
                            Buffer = pBufferIn,
                            Size = (size_t)bytesRead,
                        };
                        while ((uint)inputBuffer.Pos < (uint)inputBuffer.Size)
                        {
                            var outputBuffer = new ZstdBuffer
                            {
                                Buffer = pBufferOut,
                                Size = bufferOutSize,
                            };
                            if (ZSTD_isError(ZSTD_decompressStream(zds, ref outputBuffer, ref inputBuffer)) != 0)
                            {
                                Marshal.FreeHGlobal(pBufferOut);
                                throw new IOException();
                            }

                            int pos = (int)outputBuffer.Pos;
                            if (pos > 0)
                            {
                                Marshal.Copy(pBufferOut, bufferOut, 0, pos);
                                dst.Write(bufferOut, 0, pos);
                            }
                        }
                    }

                    Marshal.FreeHGlobal(pBufferOut);
                }

                ZSTD_freeDStream(zds);
            }
            else
            {
                ThrowIfNull();
            }
        }

        public static void DecompressStreamSimpleArgs(Stream src, Stream dst)
        {
            if (src != null && dst != null)
            {
                var zds = ZSTD_createDStream();
                if (ZSTD_isError(ZSTD_initDStream(zds)) == 0)
                {
                    var bufferOutSize = ZSTD_DStreamOutSize();
                    var bufferOut = new byte[(int)bufferOutSize];
                    var bufferIn = new byte[(int)ZSTD_DStreamInSize()];
                    int bytesRead;
                    while ((bytesRead = src.Read(bufferIn, 0, bufferIn.Length)) > 0)
                    {
                        var srcPos = (size_t)0;
                        while ((uint)srcPos < bytesRead)
                        {
                            var dstPos = (size_t)0;
                            if (ZSTD_isError(ZSTD_decompressStream_simpleArgs(zds, bufferOut, bufferOutSize, ref dstPos, bufferIn, (size_t)bytesRead, ref srcPos)) != 0)
                            {
                                throw new IOException();
                            }

                            var pos = (int)dstPos;
                            if (pos > 0)
                            {
                                dst.Write(bufferOut, 0, pos);
                            }
                        }
                    }
                }

                ZSTD_freeDStream(zds);
            }
            else
            {
                ThrowIfNull();
            }
        }

        public static void FreeLibrary()
        {
            for (int errCount = 0; FreeLibrary(hModule) && errCount < 20; errCount++)
            {
                Thread.Sleep(1);
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hLibModule);

        private static void LoadLibrary()
        {
            string workingDirectory = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}";
            string dllFullName = Path.Combine(workingDirectory, $"{ZstdVersion}{(Environment.Is64BitProcess ? string.Empty : "\\x86")}\\{DllName}");
            if (File.Exists(dllFullName))
            {
                hModule = LoadLibraryExW(dllFullName, IntPtr.Zero, 0);
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        private static void ThrowIfNull()
        {
            throw new ArgumentNullException(null, new IOException());
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_compress(byte[] dst, size_t dstCapacity, byte[] src, size_t srcSize, int compressionLevel);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_compressStream(IntPtr zcs, ref ZstdBuffer output, ref ZstdBuffer input);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_compressStream2_simpleArgs(IntPtr cctx, byte[] dst, size_t dstCapacity, ref size_t pos, byte[] src, size_t srcSize, ref size_t srcPos, int endOp);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern IntPtr ZSTD_createCStream();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern IntPtr ZSTD_createDStream();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_CStreamInSize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_CStreamOutSize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_decompress(byte[] dst, size_t dstCapacity, byte[] src, size_t compressedSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_decompressStream(IntPtr zds, ref ZstdBuffer output, ref ZstdBuffer input);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_decompressStream_simpleArgs(IntPtr dctx, byte[] dst, size_t dstCapacity, ref size_t dstPos, byte[] src, size_t srcSize, ref size_t srcPos);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_DStreamInSize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_DStreamOutSize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_endStream(IntPtr zcs, ref ZstdBuffer output);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_freeCStream(IntPtr zcs);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_freeDStream(IntPtr zds);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_initCStream(IntPtr zcs, int compressionLevel);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern size_t ZSTD_initDStream(IntPtr zds);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(DllName)]
        private static extern uint ZSTD_isError(size_t code);

        [StructLayout(LayoutKind.Sequential)]
        private struct ZstdBuffer
        {
            public IntPtr Buffer;
            public size_t Size;
            public size_t Pos;
        }
    }
}