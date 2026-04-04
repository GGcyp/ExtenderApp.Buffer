using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 表示一个原生的字节内存，用于管理非托管内存。
    /// </summary>
    public unsafe struct NativeByteMemory : IDisposable, IEquatable<NativeByteMemory>
    {
        /// <summary>
        /// 获取一个空的 <see cref="NativeByteMemory"/> 实例。
        /// </summary>
        public static NativeByteMemory Empty => new();

        /// <summary>
        /// 原生字节指针。
        /// </summary>
        private NativeIntPtr<byte> _nativePtr;

        /// <summary>
        /// 获取内存块的长度（以字节为单位）。
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// 获取指向内存块起始位置的字节指针。
        /// </summary>
        public byte* Ptr => _nativePtr.Value;

        /// <summary>
        /// 获取表示内存块的 <see cref="Span{T}"/>。
        /// </summary>
        public Span<byte> Span => new(Ptr, Length);

        /// <summary>
        /// 获取一个值，该值指示内存块是否为空。
        /// </summary>
        public bool IsEmpty => Length == 0 || _nativePtr.IsEmpty;

        /// <summary>
        /// 初始化 <see cref="NativeByteMemory"/> 结构的新实例，表示一个空内存块。
        /// </summary>
        public NativeByteMemory()
        {
            _nativePtr = NativeIntPtr<byte>.Empty;
            Length = 0;
        }

        /// <summary>
        /// 初始化 <see cref="NativeByteMemory"/> 结构的新实例，并分配指定长度的非托管内存。
        /// </summary>
        /// <param name="length">要分配的内存长度（以字节为单位）。</param>
        public NativeByteMemory(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            Length = length;
            _nativePtr = new(Marshal.AllocHGlobal(length));
        }

        /// <summary>
        /// 初始化 <see cref="NativeByteMemory"/> 结构的新实例，该实例包装现有的指针和长度。
        /// </summary>
        /// <param name="ptr">指向内存块的指针。</param>
        /// <param name="length">内存块的长度。</param>
        public NativeByteMemory(byte* ptr, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (ptr == null && length > 0)
                throw new ArgumentNullException(nameof(ptr));
            Length = length;
            _nativePtr = new(ptr);
        }

        /// <summary>
        /// 初始化 <see cref="NativeByteMemory"/> 结构的新实例，并从 <see cref="Span{T}"/> 复制数据。
        /// </summary>
        /// <param name="span">要复制到新分配的非托管内存的 <see cref="Span{T}"/>。</param>
        public NativeByteMemory(Span<byte> span)
        {
            Length = span.Length;
            _nativePtr = new(Marshal.AllocHGlobal(Length));
            span.CopyTo(Span);
        }

        /// <summary>
        /// 释放由 <see cref="NativeByteMemory"/> 分配的非托管内存。
        /// </summary>
        public void Dispose()
        {
            Marshal.FreeHGlobal(_nativePtr);
            _nativePtr.Dispose();
        }

        #region CopyTo

        /// <summary>
        /// 将此 <see cref="NativeByteMemory"/> 的内容复制到目标 <see cref="Span{T}"/>。
        /// </summary>
        /// <param name="destination">要复制到的目标 <see cref="Span{T}"/>。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Span<byte> destination)
        {
            Span.CopyTo(destination);
        }

        /// <summary>
        /// 将此 <see cref="NativeByteMemory"/> 的内容复制到目标指针。
        /// </summary>
        /// <param name="destination">要复制到的目标指针。</param>
        /// <param name="length">要复制的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(byte* destination, int length)
        {
            if (destination == null && length > 0)
                throw new ArgumentNullException(nameof(destination));
            if (length < 0 || length > Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0) return;
            Span.Slice(0, length).CopyTo(new Span<byte>(destination, length));
        }

        /// <summary>
        /// 将此 <see cref="NativeByteMemory"/> 的内容复制到另一个 <see cref="NativeByteMemory"/>。
        /// </summary>
        /// <param name="destination">要复制到的目标 <see cref="NativeByteMemory"/>。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeByteMemory destination)
        {
            Span.CopyTo(destination.Span);
        }

        /// <summary>
        /// 将此 <see cref="NativeByteMemory"/> 的指定长度内容复制到另一个 <see cref="NativeByteMemory"/>。
        /// </summary>
        /// <param name="destination">要复制到的目标 <see cref="NativeByteMemory"/>。</param>
        /// <param name="length">要复制的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeByteMemory destination, int length)
        {
            if (length < 0 || length > Length || length > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0) return;
            Span.Slice(0, length).CopyTo(destination.Span.Slice(0, length));
        }

        /// <summary>
        /// 将此 <see cref="NativeByteMemory"/> 的内容复制到目标 <see cref="Memory{T}"/>。
        /// </summary>
        /// <param name="destination">要复制到的目标 <see cref="Memory{T}"/>。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Memory<byte> destination)
        {
            Span.CopyTo(destination.Span);
        }

        #endregion CopyTo

        /// <summary>
        /// 确定此实例是否与另一个 <see cref="NativeByteMemory"/> 实例相等。
        /// </summary>
        /// <param name="other">要比较的另一个 <see cref="NativeByteMemory"/> 实例。</param>
        /// <returns>如果两个实例相等，则为 <c>true</c>；否则为 <c>false</c>。</returns>
        public bool Equals(NativeByteMemory other)
        {
            if (IsEmpty && other.IsEmpty) return true;
            if (IsEmpty || other.IsEmpty) return false;
            if (Length != other.Length) return false;
            if (Ptr == other.Ptr) return true;
            return Span.SequenceEqual(other.Span);
        }

        /// <summary>
        /// 定义从 <see cref="NativeByteMemory"/> 到 <see cref="byte"/>* 的隐式转换。
        /// </summary>
        /// <param name="block">要转换的 <see cref="NativeByteMemory"/>。</param>
        public static implicit operator byte*(NativeByteMemory block)
            => block.Ptr;

        /// <summary>
        /// 定义从 <see cref="NativeByteMemory"/> 到 <see cref="Span{T}"/> 的隐式转换。
        /// </summary>
        /// <param name="block">要转换的 <see cref="NativeByteMemory"/>。</param>
        public static implicit operator Span<byte>(NativeByteMemory block)
            => block.Span;
    }
}