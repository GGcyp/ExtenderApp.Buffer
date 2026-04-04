using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 提供针对 <see cref="Span{T}"/> 和 <see cref="ReadOnlySpan{T}"/> 的二进制数据读写扩展方法。
    /// 封装了 <see cref="BinaryPrimitives"/> 的常用操作，支持多种基础数据类型及自定义逻辑（如 decimal）。
    /// </summary>
    public static class BinaryPrimitivesExtensions
    {
        #region Write

        /// <summary>
        /// 向跨度写入一个 16 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16(this Span<byte> span, short value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteInt16BigEndian(span, value);
            else BinaryPrimitives.WriteInt16LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 32 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(this Span<byte> span, int value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteInt32BigEndian(span, value);
            else BinaryPrimitives.WriteInt32LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 64 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64(this Span<byte> span, long value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteInt64BigEndian(span, value);
            else BinaryPrimitives.WriteInt64LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 32 位单精度浮点数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingle(this Span<byte> span, float value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteSingleBigEndian(span, value);
            else BinaryPrimitives.WriteSingleLittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 64 位双精度浮点数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDouble(this Span<byte> span, double value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteDoubleBigEndian(span, value);
            else BinaryPrimitives.WriteDoubleLittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 16 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16(this Span<byte> span, ushort value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteUInt16BigEndian(span, value);
            else BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 32 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(this Span<byte> span, uint value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteUInt32BigEndian(span, value);
            else BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 64 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64(this Span<byte> span, ulong value, bool isBigEndian = true)
        {
            if (isBigEndian) BinaryPrimitives.WriteUInt64BigEndian(span, value);
            else BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        }

        /// <summary>
        /// 向跨度写入一个 <see cref="decimal"/> 值（16 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimal(this Span<byte> span, decimal value)
        {
            const int length = 4;

            Span<int> intSpan = stackalloc int[length];
            decimal.GetBits(value, intSpan);
            for (int i = 0; i < length; i++)
            {
                WriteInt32(span.Slice(i * sizeof(int)), intSpan[i], true);
            }
        }

        /// <summary>
        /// 向跨度写入一个 <see cref="Guid"/>（16 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteGuid(this Span<byte> span, Guid value)
        {
            value.TryWriteBytes(span);
        }

        /// <summary>
        /// 向跨度写入一个布尔值（1 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBoolean(this Span<byte> span, bool value)
        {
            span[0] = value ? (byte)1 : (byte)0;
        }

        #endregion Write

        #region Read

        /// <summary>
        /// 从跨度读取一个 16 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadInt16BigEndian(span) : BinaryPrimitives.ReadInt16LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 32 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadInt32BigEndian(span) : BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 64 位有符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 32 位单精度浮点数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingle(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadSingleBigEndian(span) : BinaryPrimitives.ReadSingleLittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 64 位双精度浮点数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadDoubleBigEndian(span) : BinaryPrimitives.ReadDoubleLittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 16 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 32 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 64 位无符号整数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64(this ReadOnlySpan<byte> span, bool isBigEndian = true)
        {
            return isBigEndian ? BinaryPrimitives.ReadUInt64BigEndian(span) : BinaryPrimitives.ReadUInt64LittleEndian(span);
        }

        /// <summary>
        /// 从跨度读取一个 <see cref="decimal"/> 值（16 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(this ReadOnlySpan<byte> span)
        {
            const int length = 4;
            Span<int> bits = stackalloc int[length];
            for (int i = 0; i < length; i++)
            {
                bits[i] = ReadInt32(span.Slice(i * sizeof(int)), true);
            }
            return new decimal(bits);
        }

        /// <summary>
        /// 从跨度读取一个布尔值（1 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadBoolean(this ReadOnlySpan<byte> span)
        {
            return span[0] != 0;
        }

        /// <summary>
        /// 从跨度读取一个 <see cref="Guid"/>（16 字节）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this ReadOnlySpan<byte> span)
        {
            return new Guid(span);
        }

        #endregion Read
    }
}