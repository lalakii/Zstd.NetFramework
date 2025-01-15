using System;
using System.IO;
using System.Runtime.InteropServices;
using size_t = System.UIntPtr;

namespace CN.Lalaki.Zstd
{
    public static class Zstd
    {
        private const string DllName = "libzstd.dll";
        private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
        private const string ZstdVersion = "zstd-v1.5.7-dev";
        private static readonly string WorkingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        static Zstd()
        {
            SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs);
            var dllPath = Path.Combine(WorkingDirectory, ZstdVersion);
            if (!Environment.Is64BitProcess)
            {
                dllPath = Path.Combine(dllPath, "x86\\");
            }

            var pDllPath = Marshal.StringToHGlobalAuto(dllPath);
            AddDllDirectory(pDllPath);
            Marshal.FreeHGlobal(pDllPath);
        }

        public static byte[] Compress(byte[] src, int level)
        {
            var len = (size_t)src.Length;
            var dst = new byte[src.Length];
            var res = ZSTD_compress(dst, len, src, len, level);
            if (ZSTD_isError(res) == 0)
            {
                Array.Resize(ref dst, (int)res);
                return dst;
            }

            return [];
        }

        public static void CompressStream(Stream src, Stream dst, int level)
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

        public static void CompressStream2SimpleArgs(Stream src, Stream dst, int level)
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

        public static byte[] Decompress(byte[] src, int dstCapacity)
        {
            var dst = new byte[dstCapacity];
            var res = ZSTD_decompress(dst, (size_t)dst.Length, src, (size_t)src.Length);
            if (ZSTD_isError(res) == 0)
            {
                Array.Resize(ref dst, (int)res);
                return dst;
            }

            return [];
        }

        public static void DecompressStream(Stream src, Stream dst)
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

        public static void DecompressStreamSimpleArgs(Stream src, Stream dst)
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

        [DllImport("kernel32.dll")]
        private static extern int AddDllDirectory(IntPtr newDirectory);

        [DllImport("kernel32.dll")]
        private static extern bool SetDefaultDllDirectories(uint directoryFlags);

        [DllImport(DllName)]
        private static extern size_t ZSTD_compress(byte[] dst, size_t dstCapacity, byte[] src, size_t srcSize, int compressionLevel);

        [DllImport(DllName)]
        private static extern size_t ZSTD_compressStream(IntPtr zcs, ref ZstdBuffer output, ref ZstdBuffer input);

        [DllImport(DllName)]
        private static extern size_t ZSTD_compressStream2_simpleArgs(IntPtr cctx, byte[] dst, size_t dstCapacity, ref size_t pos, byte[] src, size_t srcSize, ref size_t srcPos, int endOp);

        [DllImport(DllName)]
        private static extern IntPtr ZSTD_createCStream();

        [DllImport(DllName)]
        private static extern IntPtr ZSTD_createDStream();

        [DllImport(DllName)]
        private static extern size_t ZSTD_CStreamInSize();

        [DllImport(DllName)]
        private static extern size_t ZSTD_CStreamOutSize();

        [DllImport(DllName)]
        private static extern size_t ZSTD_decompress(byte[] dst, size_t dstCapacity, byte[] src, size_t compressedSize);

        [DllImport(DllName)]
        private static extern size_t ZSTD_decompressStream(IntPtr zds, ref ZstdBuffer output, ref ZstdBuffer input);

        [DllImport(DllName)]
        private static extern size_t ZSTD_decompressStream_simpleArgs(IntPtr dctx, byte[] dst, size_t dstCapacity, ref size_t dstPos, byte[] src, size_t srcSize, ref size_t srcPos);

        [DllImport(DllName)]
        private static extern size_t ZSTD_DStreamInSize();

        [DllImport(DllName)]
        private static extern size_t ZSTD_DStreamOutSize();

        [DllImport(DllName)]
        private static extern size_t ZSTD_endStream(IntPtr zcs, ref ZstdBuffer output);

        [DllImport(DllName)]
        private static extern size_t ZSTD_freeCStream(IntPtr zcs);

        [DllImport(DllName)]
        private static extern size_t ZSTD_freeDStream(IntPtr zds);

        [DllImport(DllName)]
        private static extern size_t ZSTD_initCStream(IntPtr zcs, int compressionLevel);

        [DllImport(DllName)]
        private static extern size_t ZSTD_initDStream(IntPtr zds);

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