using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 为 <see cref="ByteBlock"/> 提供高性能二进制写入扩展方法。 数值写入在语义上与 <see cref="BinaryPrimitivesExtensions"/>（基于 <see cref="System.Buffers.Binary.BinaryPrimitives"/>）对齐；泛型 <c>Write&lt;T&gt;</c> 使用 <see cref="System.Runtime.InteropServices.MemoryMarshal"/> 按托管布局写入后再按需反转字节序。
    /// </summary>
    public static class ByteBlockExtensions
    {
        private static readonly Encoding DefaultEncoding = ExtenderApp.Common.ProgramDirectory.DefaultEncoding;

        #region Write

        /// <summary>
        /// 将一个非托管类型的数据写入字节块，并将写入指针前进相应字节数。
        /// </summary>
        /// <typeparam name="T">指定 <typeparamref name="T"/> 类型</typeparam>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的 <typeparamref name="T"/> 值。</param>
        /// <param name="isBigEndian">是否采用大端字节序。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this ref ByteBlock block, in T value, bool isBigEndian = true)
            where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            Span<byte> span = block.GetSpan(size);
            MemoryMarshal.Write(span, in value);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Reverse();
            }
            block.Advance(size);
        }

        /// <summary>
        /// 写入一个 128 位精度的高精度浮点数（decimal），并将写入指针前进 16 字节。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的 decimal 值。</param>
        /// <remarks>内部按四个 32 位大端序整数存储。</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimal(this ref ByteBlock block, decimal value)
        {
            const int size = 16;
            block.GetSpan(size).WriteDecimal(value);
            block.Advance(size);
        }

        /// <summary>
        /// 写入一个全局唯一标识符（Guid），并将写入指针前进 16 字节。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的 Guid 值。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteGuid(this ref ByteBlock block, Guid value)
        {
            const int size = 16;
            block.GetSpan(size).WriteGuid(value);
            block.Advance(size);
        }

        /// <summary>
        /// 写入一个布尔值（1 字节），并将写入指针前进 1 字节。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的布尔值（true=1, false=0）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBoolean(this ref ByteBlock block, bool value)
        {
            block.GetSpan(1).WriteBoolean(value);
            block.Advance(1);
        }

        /// <summary>
        /// 写入日期时间（DateTime），仅存储其 Ticks 值，并将写入指针前进 8 字节。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的时间值。</param>
        /// <param name="isBigEndian">是否采用大端字节序写入 Ticks。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDateTime(this ref ByteBlock block, DateTime value, bool isBigEndian = true)
        {
            // 与 BinaryPrimitivesExtensions.WriteInt64 / ReadInt64 的字节序约定一致，避免与 MemoryMarshal.Write+Reverse 顺序组合产生不一致。
            Span<byte> span = block.GetSpan(sizeof(long));
            span.WriteInt64(value.Ticks, isBigEndian);
            block.Advance(sizeof(long));
        }

        /// <summary>
        /// 写入一个 Unicode 字符，根据编码计算字节长度并前进指针。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的字符。</param>
        /// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChar(this ref ByteBlock block, char value, Encoding? encoding = null)
        {
            encoding ??= DefaultEncoding;
            Span<char> chars = stackalloc char[1] { value };
            int byteCount = encoding.GetByteCount(chars);
            encoding.GetBytes(chars, block.GetSpan(byteCount));
            block.Advance(byteCount);
        }

        /// <summary>
        /// 写入一个字符串内容，不包含长度前缀。写入后自动前进相应字节长度。
        /// </summary>
        /// <param name="block">目标字节块实例。</param>
        /// <param name="value">要写入的字符串内容。</param>
        /// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        public static void WriteString(this ref ByteBlock block, string? value, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            encoding ??= DefaultEncoding;
            int byteCount = encoding.GetByteCount(value);
            encoding.GetBytes(value, block.GetSpan(byteCount));
            block.Advance(byteCount);
        }

        #endregion Write

        //#region Read

        ///// <summary>
        ///// 写入一个非托管类型的数据，并将写入指针前进相应字节数。
        ///// </summary>
        ///// <typeparam name="T">非托管类型数据类型</typeparam>
        ///// <param name="block">源字节块实例。</param>
        ///// <param name="isBigEndian">是否按大端序解码。</param>
        ///// <returns>解码后的 <typeparamref name="T"/> 值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static T Read<T>(this ref ByteBlock block, bool isBigEndian = true)
        //    where T : unmanaged
        //{
        //    int size = Marshal.SizeOf<T>();
        //    Span<byte> span = stackalloc byte[size];
        //    block.Read(span);
        //    if (BitConverter.IsLittleEndian == isBigEndian)
        //    {
        //        span.Reverse();
        //    }
        //    return MemoryMarshal.Read<T>(span);
        //}

        ///// <summary>
        ///// 从当前位置读取一个 decimal 值并前进 16 字节。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <returns>解码后的 decimal 值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static decimal ReadDecimal(this ref ByteBlock block)
        //{
        //    const int size = 16;
        //    return block.Read(size).ReadDecimal();
        //}

        ///// <summary>
        ///// 从当前位置读取一个字符并前进相应字节数。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        ///// <returns>解码后的 <see cref="char"/> 值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static char ReadChar(this ref ByteBlock block, Encoding? encoding = null)
        //{
        //    encoding ??= DefaultEncoding;
        //    int maxByteCount = encoding.GetMaxByteCount(1);
        //    ReadOnlySpan<byte> span = block.Read(maxByteCount);
        //    Span<char> chars = stackalloc char[1];
        //    int actualByteCount = encoding.GetChars(span, chars);
        //    return chars[0];
        //}

        ///// <summary>
        ///// 从当前位置读取一个 Guid 并前进 16 字节。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <returns>解析出的 Guid。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static Guid ReadGuid(this ref ByteBlock block)
        //{
        //    const int size = 16;
        //    return block.Read(size).ReadGuid();
        //}

        ///// <summary>
        ///// 读取一个布尔值（1 字节），并前进指针。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <returns>解析出的布尔值。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static bool ReadBoolean(this ref ByteBlock block)
        //{
        //    return block.Read(1).ReadBoolean();
        //}

        ///// <summary>
        ///// 读取日期时间（Ticks），并前进读取指针 8 字节。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <param name="isBigEndian">Ticks 的字节序格式。</param>
        ///// <returns>解析出的 DateTime 结构。</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static DateTime ReadDateTime(this ref ByteBlock block, bool isBigEndian = true)
        //{
        //    return new DateTime(block.Read(isBigEndian));
        //}

        ///// <summary>
        ///// 读取所有剩余可用字节并将其解码为字符串内容。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        ///// <returns>解码后的字符串内容。</returns>
        //public static string ReadString(this ref ByteBlock block, Encoding? encoding = null)
        //{
        //    return block.ReadString(block.Remaining > int.MaxValue ? int.MaxValue : (int)block.Remaining, encoding);
        //}

        ///// <summary>
        ///// 读取指定数量的字节并将其解码为字符串内容，完成后前进相应指针。
        ///// </summary>
        ///// <param name="block">源字节块实例。</param>
        ///// <param name="byteCount">要读取的字节数量。</param>
        ///// <param name="encoding">指定编码，若为 null 则使用默认编码。</param>
        ///// <returns>解码后的字符串内容。</returns>
        //public static string ReadString(this ref ByteBlock block, int byteCount, Encoding? encoding = null)
        //{
        //    if (byteCount <= 0) return string.Empty;
        //    encoding ??= DefaultEncoding;
        //    return encoding.GetString(block.Read(byteCount));
        //}

        //#endregion Read
    }
}