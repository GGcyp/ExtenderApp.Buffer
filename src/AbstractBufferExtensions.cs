using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 提供针对 <see cref="AbstractBuffer{byte}"/>、 <see cref="Span{byte}"/> 和相关类型的读写扩展方法， 包括对非托管类型的二进制写入、字符串写入以及尝试写入的辅助方法。
    /// </summary>
    public static class AbstractBufferExtensions
    {
        #region Write

        /// <summary>
        /// 将指定非托管值写入到目标 <see cref="AbstractBuffer{byte}"/> 的可写区，并以指定字节序写入。 写入前会通过 <see cref="AbstractBuffer{T}.GetSpan(int)"/> 获取可写区域并在写入后由调用方推进写指针。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="buffer">目标缓冲区实例（不能为 <c>null</c>）。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffer"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="IndexOutOfRangeException">当缓冲区可写区长度小于要写入的数据长度时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this AbstractBuffer<byte> buffer, T value, bool isBigEndian = true)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            int size = Marshal.SizeOf<T>();
            var span = buffer.GetSpan(size);
            if (span.Length < size)
                throw new IndexOutOfRangeException($"当前需要转换类型为 {typeof(T).Name}，所需大小为 {size}，当前缓存范围为 {span.Length}，缓存不足。");

            MemoryMarshal.Write(span, in value);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Slice(0, size).Reverse();
            }
        }

        /// <summary>
        /// 将指定非托管值写入到目标 <see cref="Memory{byte}"/>（以平台本机字节序或指定字节序写入）。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">目标内存。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this Memory<byte> memory, T value, bool isBigEndian = true)
            where T : unmanaged
        {
            memory.Write(value, out _, isBigEndian);
        }

        /// <summary>
        /// 将指定非托管值写入到目标 <see cref="Memory{byte}"/>，并返回写入所需的字节数。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">目标内存。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="size">输出的写入字节数（等于 <typeparamref name="T"/> 的字节大小）。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this Memory<byte> memory, T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            memory.Span.Write(value, out size, isBigEndian);
        }

        /// <summary>
        /// 将指定非托管值写入到目标 <see cref="Span{byte}"/>（不返回大小）。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">目标缓冲区。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="span"/> 为空时抛出。</exception>
        /// <exception cref="IndexOutOfRangeException">当 <paramref name="span"/> 长度小于目标类型所需字节数时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this Span<byte> span, T value, bool isBigEndian = true)
            where T : unmanaged
        {
            span.Write(value, out _, isBigEndian);
        }

        /// <summary>
        /// 将指定非托管值写入到目标 <see cref="Span{byte}"/>，并返回写入所需的字节数。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">目标缓冲区（应至少包含 <paramref name="size"/> 字节）。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="size">输出的写入字节数（等于 <typeparamref name="T"/> 的字节大小）。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="span"/> 为空时抛出。</exception>
        /// <exception cref="IndexOutOfRangeException">当 <paramref name="span"/> 长度小于所需字节数时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this Span<byte> span, T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            if (span.IsEmpty)
                throw new ArgumentNullException(nameof(span));
            if (span.Length < size)
                throw new IndexOutOfRangeException($"当前需要转换类型为 {typeof(T).Name}，所需大小为 {size}，当前缓存范围为 {span.Length}，缓存不足。");

            MemoryMarshal.Write(span, in value);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Slice(0, size).Reverse();
            }
        }

        /// <summary>
        /// 将指定字符串按指定编码写入到目标 <see cref="AbstractBuffer{byte}"/>（不包含长度或终止符），并推进写指针。 若 <paramref name="value"/> 为 <c>null</c> 或空字符串则不执行任何写入。
        /// </summary>
        /// <param name="buffer">目标缓冲区（不能为 <c>null</c>）。</param>
        /// <param name="value">要写入的字符串。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this AbstractBuffer<byte> buffer, string value)
            => Write(buffer, value, Encoding.GetEncoding(0));

        /// <summary>
        /// 将指定字符串按给定编码写入到目标 <see cref="AbstractBuffer{byte}"/>（不包含长度或终止符），并推进写指针。
        /// </summary>
        /// <param name="buffer">目标缓冲区（不能为 <c>null</c>）。</param>
        /// <param name="value">要写入的字符串。</param>
        /// <param name="encoding">用于将字符串编码为字节的编码器（不能为 <c>null</c>）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this AbstractBuffer<byte> buffer, string value, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));

            int size = encoding.GetMaxByteCount(value.Length);
            var span = buffer.GetSpan(size);
            size = encoding.GetBytes(value, span);
            buffer.Advance(size);
        }

        /// <summary>
        /// 将指定字符串按默认编码写入到目标 <see cref="Memory{byte}"/>，并返回写入的字节数。
        /// </summary>
        /// <param name="memory">目标内存。</param>
        /// <param name="value">要写入的字符串。</param>
        /// <param name="size">输出写入的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Memory<byte> memory, string value, out int size)
            => Write(memory, value, Encoding.GetEncoding(0), out size);

        /// <summary>
        /// 将指定字符串按给定编码写入到目标 <see cref="Memory{byte}"/>，并返回写入的字节数。
        /// </summary>
        /// <param name="memory">目标内存。</param>
        /// <param name="value">要写入的字符串。</param>
        /// <param name="encoding">用于将字符串编码为字节的编码器（不能为 <c>null</c>）。</param>
        /// <param name="size">输出写入的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Memory<byte> memory, string value, Encoding encoding, out int size)
            => Write(memory.Span, value, encoding, out size);

        /// <summary>
        /// 将指定字符串按默认编码写入到目标 <see cref="Span{byte}"/>，并返回写入的字节数。
        /// </summary>
        /// <param name="span">目标跨度。</param>
        /// <param name="value">要写入的字符串。</param>
        /// <param name="size">输出写入的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Span<byte> span, string value, out int size)
            => Write(span, value, Encoding.GetEncoding(0), out size);

        /// <summary>
        /// 将指定字符串按给定编码写入到目标 <see cref="Span{byte}"/>，并返回写入的字节数。
        /// </summary>
        /// <param name="span">目标跨度。</param>
        /// <param name="value">要写入的字符串。</param>
        /// <param name="encoding">用于将字符串编码为字节的编码器（不能为 <c>null</c>）。</param>
        /// <param name="size">输出写入的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Span<byte> span, string value, Encoding encoding, out int size)
        {
            size = 0;
            if (span.IsEmpty)
                throw new ArgumentNullException(nameof(span));
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
            if (string.IsNullOrEmpty(value))
                return;

            size = encoding.GetByteCount(value);
            if (span.Length < size)
                throw new IndexOutOfRangeException($"当前需要写入字符串的字节大小为 {size}，当前缓存范围为 {span.Length}，缓存不足。");
            size = encoding.GetBytes(value, span);
        }

        #endregion Write

        #region TryWrite

        /// <summary>
        /// 尝试将指定非托管值写入到目标 <see cref="Span{byte}"/>，若写入空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        /// <typeparam name="T">要写入的值类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">目标缓冲区（应至少包含 <paramref name="size"/> 字节）。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="size">输出的写入字节数（等于 <typeparamref name="T"/> 的字节大小），当返回 <c>false</c> 时为 0。</param>
        /// <param name="isBigEndian">指示写入时是否采用大端字节序；为 <c>true</c> 则按 big-endian 写入，否则按平台 native 序写入。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWrite<T>(this Span<byte> span, T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            if (span.IsEmpty || span.Length < size)
            {
                size = 0;
                return false;
            }

            MemoryMarshal.Write(span, in value);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Slice(0, size).Reverse();
            }
            return true;
        }

        /// <summary>
        /// 尝试将指定非托管值写入到目标 <see cref="Memory{byte}"/>，若写入空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWrite<T>(this Memory<byte> memory, T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return memory.Span.TryWrite(value, out size, isBigEndian);
        }

        /// <summary>
        /// 尝试将指定非托管值写入到目标 <see cref="AbstractBuffer{byte}"/>，若写入空间不足或发生错误则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWrite<T>(this AbstractBuffer<byte> buffer, T value, bool isBigEndian = true)
            where T : unmanaged
        {
            if (buffer is null)
            {
                return false;
            }

            int size = Marshal.SizeOf<T>();
            try
            {
                var span = buffer.GetSpan(size);
                if (span.Length < size)
                {
                    size = 0;
                    return false;
                }
                MemoryMarshal.Write(span, in value);
                if (BitConverter.IsLittleEndian == isBigEndian)
                {
                    span.Slice(0, size).Reverse();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion TryWrite

        #region Read

        /// <summary>
        /// 从 <see cref="Memory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">包含目标数据的内存。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示内存中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this AbstractBuffer<byte> buffer, bool isBigEndian = true)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            var result = Read<T>(buffer.CommittedSequence, out var size, isBigEndian);
            return result;
        }

        /// <summary>
        /// 从 <see cref="Memory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值（默认按 big-endian 解释字节序）。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">包含目标数据的内存。</param>
        /// <param name="isBigEndian">指示内存中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this Memory<byte> memory, bool isBigEndian = true)
            where T : unmanaged
        {
            return memory.Read<T>(out _, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="Memory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">包含目标数据的内存。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示内存中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this Memory<byte> memory, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return Read<T>(memory.Span, out size, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlyMemory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值（不返回大小）。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">包含目标数据的只读内存。</param>
        /// <param name="isBigEndian">指示内存中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this ReadOnlyMemory<byte> memory, bool isBigEndian = true)
            where T : unmanaged
        {
            return memory.Read<T>(out _, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlyMemory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="memory">包含目标数据的只读内存。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示内存中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this ReadOnlyMemory<byte> memory, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return Read<T>(memory.Span, out size, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="Span{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值（不返回大小）。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">包含目标数据的缓冲区。</param>
        /// <param name="isBigEndian">指示缓冲区中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this scoped Span<byte> span, bool isBigEndian = true)
            where T : unmanaged
        {
            return span.Read<T>(out _, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="Span{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">包含目标数据的缓冲区。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示缓冲区中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this scoped Span<byte> span, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return Read<T>((ReadOnlySpan<byte>)span, out size, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlySpan{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值（不返回大小）。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">包含目标数据的缓冲区。</param>
        /// <param name="isBigEndian">指示缓冲区中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this scoped ReadOnlySpan<byte> span, bool isBigEndian = true)
            where T : unmanaged
        {
            return span.Read<T>(out _, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlySpan{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="span">包含目标数据的只读缓冲区。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示缓冲区中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会使用临时栈缓冲反转字节以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this scoped ReadOnlySpan<byte> span, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            if (span.IsEmpty)
                throw new ArgumentNullException(nameof(span));
            if (span.Length < size)
                throw new IndexOutOfRangeException($"当前需要转换类型为 {typeof(T).Name}，所需大小为 {size}，当前缓存范围为 {span.Length}，缓存不足。");

            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                Span<byte> tempSpan = stackalloc byte[size];
                span.Slice(0, size).CopyTo(tempSpan);
                tempSpan.Reverse();
                span = tempSpan;
            }
            return MemoryMarshal.Read<T>(span);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlySequence{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值（不返回大小）。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="sequence">包含目标数据的只读序列。</param>
        /// <param name="isBigEndian">指示序列中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this ReadOnlySequence<byte> sequence, bool isBigEndian = true)
            where T : unmanaged
        {
            return sequence.Read<T>(out _, isBigEndian);
        }

        /// <summary>
        /// 从 <see cref="ReadOnlySequence{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，并返回读取所用的字节数。
        /// </summary>
        /// <typeparam name="T">要读取的目标类型，必须为 <c>unmanaged</c>。</typeparam>
        /// <param name="sequence">包含目标数据的只读序列。</param>
        /// <param name="size">输出的字节大小（等于 <typeparamref name="T"/> 的字节长度）。</param>
        /// <param name="isBigEndian">指示序列中的字节序是否为大端：若平台为 little-endian 且本参数为 <c>true</c>，方法会对字节进行反转以恢复正确值。</param>
        /// <returns>解析得到的 <typeparamref name="T"/> 值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this ReadOnlySequence<byte> sequence, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            if (sequence.IsEmpty)
                throw new ArgumentNullException(nameof(sequence));
            if (sequence.Length < size)
                throw new IndexOutOfRangeException($"当前需要转换类型为 {typeof(T).Name}，所需大小为 {size}，当前缓存范围为 {sequence.Length}，缓存不足。");

            Span<byte> span = stackalloc byte[size];
            sequence.Slice(0, size).CopyTo(span);

            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Reverse();
            }
            return MemoryMarshal.Read<T>(span);
        }

        #endregion Read

        #region TryRead

        /// <summary>
        /// 尝试从 <see cref="ReadOnlySpan{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，若空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead<T>(this scoped ReadOnlySpan<byte> span, out T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            value = default;
            if (span.IsEmpty || span.Length < size)
            {
                size = 0;
                return false;
            }

            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                Span<byte> tempSpan = stackalloc byte[size];
                span.Slice(0, size).CopyTo(tempSpan);
                tempSpan.Reverse();
                value = MemoryMarshal.Read<T>(tempSpan);
            }
            else
            {
                value = MemoryMarshal.Read<T>(span);
            }
            return true;
        }

        /// <summary>
        /// 尝试从 <see cref="ReadOnlyMemory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，若空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead<T>(this ReadOnlyMemory<byte> memory, out T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return TryRead<T>(memory.Span, out value, out size, isBigEndian);
        }

        /// <summary>
        /// 尝试从 <see cref="Memory{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，若空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead<T>(this Memory<byte> memory, out T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            return TryRead<T>(memory.Span, out value, out size, isBigEndian);
        }

        /// <summary>
        /// 尝试从 <see cref="ReadOnlySequence{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，若空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead<T>(this ReadOnlySequence<byte> sequence, out T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            size = Marshal.SizeOf<T>();
            value = default;
            if (sequence.IsEmpty || sequence.Length < size)
            {
                size = 0;
                return false;
            }

            Span<byte> span = stackalloc byte[size];
            sequence.Slice(0, size).CopyTo(span);
            if (BitConverter.IsLittleEndian == isBigEndian)
            {
                span.Reverse();
            }
            value = MemoryMarshal.Read<T>(span);
            return true;
        }

        /// <summary>
        /// 尝试从 <see cref="AbstractBuffer{byte}"/> 中读取类型为 <typeparamref name="T"/> 的值，若空间不足则返回 <c>false</c>（不抛出异常）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead<T>(this AbstractBuffer<byte> buffer, out T value, out int size, bool isBigEndian = true)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return TryRead(buffer.CommittedSequence, out value, out size, isBigEndian);
        }

        #endregion TryRead

        #region Slice

        /// <summary>
        /// 获取当前 <see cref="AbstractBuffer{T}"/> 中尚未提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从已提交末尾开始、长度为可用空间的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbstractBuffer<T> AvailableSlice<T>(this AbstractBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(buffer.Committed, buffer.Available);
        }

        /// <summary>
        /// 获取当前 <see cref="AbstractBuffer{T}"/> 中已提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从起始位置开始、长度为已提交数据的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbstractBuffer<T> CommittedSlice<T>(this AbstractBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(0, buffer.Committed);
        }

        #endregion Slice
    }
}