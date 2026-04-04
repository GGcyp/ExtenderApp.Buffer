using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 为 <see cref="ByteBuffer"/> 提供高性能二进制读写扩展方法。
    /// 该类封装了对基础数值类型、布尔、<see cref="Guid"/>, <see cref="decimal"/>, <see cref="DateTime"/> 与 字符/字符串 的顺序读写。
    /// 注意：写操作使用 <see cref="ByteBuffer.GetSpan(int)"/> + <see cref="ByteBuffer.Advance(int)"/>；
    /// 读操作在遇到多段序列边界时会将数据复制到临时 <see cref="ByteBlock"/> 以保证读取连续性。
    /// </summary>
    public static class ByteBufferExtensions
    {
        private static readonly Encoding DefaultEncoding = Encoding.UTF8;

        #region Write

        /// <summary>
        /// 将非托管类型 <typeparamref name="T"/> 的原始字节写入 <paramref name="buffer"/> 的当前写入位置。
        /// </summary>
        /// <typeparam name="T">要写入的非托管类型（<c>unmanaged</c>）。</typeparam>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/>（以 ref 传入）。</param>
        /// <param name="value">要写入的值（按平台本机字节序写入）。</param>
        /// <remarks>
        /// - 本方法以平台本机字节序将值按位写入缓冲区，不会做字节序（endianness）转换。
        /// - 若需要固定线序（例如 big-endian/network order），请使用相应的明确方法（例如 WriteInt32/WriteInt64 等带 <c>isBigEndian</c> 参数的方法）。
        /// - 使用 <c>MemoryMarshal.Write</c> 将值写入目标内存以获得最小开销。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this ref ByteBuffer buffer, in T value, bool isBigEndian = true)
            where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            Span<byte> span = buffer.GetSpan(size);
            MemoryMarshal.Write(span, in value);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Reverse();
            }
            buffer.Advance(size);
        }

        /// <summary>
        /// 写入一个 decimal（16 字节），并将写入指针前进 16 字节。
        /// 内部按四个 32 位大端序整数存储以保持与 <see cref="ByteBlock"/> 兼容。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的 <see cref="decimal"/> 值。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimal(this ref ByteBuffer buffer, decimal value)
        {
            const int size = 16;
            buffer.GetSpan(size).WriteDecimal(value);
            buffer.Advance(size);
        }

        /// <summary>
        /// 写入一个 <see cref="Guid"/>（16 字节），并将写入指针前进 16 字节。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的 <see cref="Guid"/> 值。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteGuid(this ref ByteBuffer buffer, Guid value)
        {
            const int size = 16;
            buffer.GetSpan(size).WriteGuid(value);
            buffer.Advance(size);
        }

        /// <summary>
        /// 写入一个布尔值（1 字节），并将写入指针前进 1 字节。true 写为 0x01，false 写为 0x00。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的布尔值。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBoolean(this ref ByteBuffer buffer, bool value)
        {
            buffer.GetSpan(1).WriteBoolean(value);
            buffer.Advance(1);
        }

        /// <summary>
        /// 写入 <see cref="DateTime"/> 的 <see cref="DateTime.Ticks"/>（Int64），并将写入指针前进 8 字节。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的 <see cref="DateTime"/> 值。</param>
        /// <param name="isBigEndian">Ticks 的字节序（默认为大端）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDateTime(this ref ByteBuffer buffer, DateTime value, bool isBigEndian = true)
        {
            buffer.Write(value.Ticks);
        }

        /// <summary>
        /// 写入单个字符（按指定编码），不带长度前缀，写入后前进相应字节数。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的字符。</param>
        /// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChar(this ref ByteBuffer buffer, char value, Encoding? encoding = null)
        {
            encoding ??= DefaultEncoding;
            Span<char> chars = stackalloc char[1] { value };
            int byteCount = encoding.GetByteCount(chars);
            encoding.GetBytes(chars, buffer.GetSpan(byteCount));
            buffer.Advance(byteCount);
        }

        /// <summary>
        /// 写入字符串（按指定编码），不带长度前缀，写入后前进相应字节数。
        /// </summary>
        /// <param name="buffer">目标 <see cref="ByteBuffer"/> 实例（按引用传递）。</param>
        /// <param name="value">要写入的字符串（可为 null 或空）。</param>
        /// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        public static void Write(this ref ByteBuffer buffer, string? value, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            encoding ??= DefaultEncoding;
            int byteCount = encoding.GetByteCount(value);
            encoding.GetBytes(value, buffer.GetSpan(byteCount));
            buffer.Advance(byteCount);
        }

        #endregion Write

        //#region Read

        ///// <summary>
        ///// 从 <paramref name="buffer"/> 的当前读取位置读取类型 <typeparamref name="T"/> 的原始字节并还原为该类型。
        ///// </summary>
        ///// <typeparam name="T">目标值类型，必须为 <c>unmanaged</c>。</typeparam>
        ///// <param name="buffer">来源 <see cref="ByteBuffer"/>（以 ref 传入）。</param>
        ///// <param name="isBigEndian">
        ///// 指示缓冲区中的数据是否采用大端字节序（true 表示数据为 big-endian）。
        ///// 方法在平台为 little-endian 且 <paramref name="isBigEndian"/> 不同时会对字节执行反转以恢复正确值。
        ///// </param>
        ///// <returns>反序列化得到的 <typeparamref name="T"/> 值。</returns>
        ///// <remarks>
        ///// - 本方法使用 stackalloc 临时缓冲以避免堆分配；临时缓冲大小等于类型字节大小。
        ///// - 当需要跨平台兼容时，确保读写双方对 <c>isBigEndian</c> 的约定一致。
        ///// </remarks>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private static T Read<T>(this ref ByteBuffer buffer, bool isBigEndian = true)
        //    where T : unmanaged
        //{
        //    int size = Unsafe.SizeOf<T>();
        //    Span<byte> span = stackalloc byte[size];
        //    buffer.Read(span);
        //    if (BitConverter.IsLittleEndian == isBigEndian)
        //    {
        //        span.Reverse();
        //    }
        //    return MemoryMarshal.Read<T>(span);
        //}

        ///// <summary>
        ///// 从当前位置读取一个 <see cref="decimal"/>（16 字节）并前进读取指针 16 字节。
        ///// </summary>
        ///// <param name="buffer">源 <see cref="ByteBuffer"/>（按引用）。</param>
        ///// <returns>解码后的 <see cref="decimal"/> 值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static decimal ReadDecimal(this ref ByteBuffer buffer)
        //{
        //    const int size = 16;
        //    Span<byte> span = stackalloc byte[size];
        //    buffer.Read(span);
        //    ReadOnlySpan<byte> readOnlySpan = span;
        //    return readOnlySpan.ReadDecimal();
        //}

        ///// <summary>
        ///// 从当前位置读取一个 <see cref="Guid"/>（16 字节）并前进读取指针 16 字节。
        ///// </summary>
        ///// <param name="buffer">源 <see cref="ByteBuffer"/>（按引用）。</param>
        ///// <returns>解析出的 <see cref="Guid"/>。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static Guid ReadGuid(this ref ByteBuffer buffer)
        //{
        //    const int size = 16;
        //    Span<byte> span = stackalloc byte[size];
        //    buffer.Read(span);
        //    ReadOnlySpan<byte> readOnlySpan = span;
        //    return readOnlySpan.ReadGuid();
        //}

        ///// <summary>
        ///// 从当前位置读取一个布尔值（1 字节）并前进读取指针 1 字节。
        ///// </summary>
        ///// <param name="buffer">源 <see cref="ByteBuffer"/>（按引用）。</param>
        ///// <returns>解析出的布尔值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static bool ReadBoolean(this ref ByteBuffer buffer)
        //{
        //    const int size = 1;
        //    Span<byte> span = stackalloc byte[size];
        //    buffer.Read(span);
        //    ReadOnlySpan<byte> readOnlySpan = span;
        //    return readOnlySpan.ReadBoolean();
        //}

        ///// <summary>
        ///// 从当前位置读取 <see cref="DateTime"/>（通过 Ticks 恢复）并前进读取指针 8 字节。
        ///// </summary>
        ///// <param name="buffer">源 <see cref="ByteBuffer"/>（按引用）。</param>
        ///// <param name="isBigEndian">Ticks 的字节序（默认为大端）。</param>
        ///// <returns>解析出的 <see cref="DateTime"/>。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static DateTime ReadDateTime(this ref ByteBuffer buffer, bool isBigEndian = true)
        //{
        //    return new DateTime(buffer.Read<long>());
        //}

        ///// <summary>
        ///// 从当前位置按指定编码读取一个字符并前进相应字节数。
        ///// </summary>
        ///// <remarks>
        ///// - 方法按单个 UTF-16 单元（<see cref="char"/>）进行读取并解码。
        ///// - 对于需要代理对（surrogate pair）表示的 Unicode 码点，应由调用方以字符串/码点层面处理以保证正确性。
        ///// - 如果编码需要更多字节，本方法会基于 <see cref="encoding"/> 的最大字节数分配临时块进行读取并解码。
        ///// - 解码失败或不完整时，具体行为由 <see cref="Encoding"/> 的回退策略决定（替换或抛异常）。
        ///// </remarks>
        ///// <param name="buffer">源 <see cref="ByteBuffer"/>（按引用）。</param>
        ///// <param name="encoding">编码（若为 null 则使用默认编码）。</param>
        ///// <returns>解码得到的字符；若无法读取则返回默认 <c>'\0'</c>。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static string ReadString(this ref ByteBuffer buffer, int byteCount, Encoding? encoding = null)
        //{
        //    const int MaxStackAllocSize = 1024;

        //    if (byteCount <= 0) return string.Empty;
        //    encoding ??= DefaultEncoding;

        //    if (byteCount <= MaxStackAllocSize)
        //    {
        //        Span<byte> span = stackalloc byte[byteCount];
        //        buffer.Read(span);
        //        return encoding.GetString(span);
        //    }

        //    ByteBlock block = new(byteCount);
        //    buffer.Read(block.GetSpan(byteCount).Slice(0, byteCount));
        //    return block.ReadString(encoding);
        //}

        //#endregion Read
    }
}