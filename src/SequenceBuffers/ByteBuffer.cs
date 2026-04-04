using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 基于 <see cref="SequenceBuffer{byte}"/> 的轻量字节写入/读取包装器。 提供高性能的写入（通过 <see cref="IBufferWriter{byte}"/>）与读取视图（通过内部的 <see cref="ValueSequenceBufferReader{byte}"/>），并支持池化场景的便捷构造与释放。
    /// </summary>
    /// <remarks>
    /// - 该结构体在构造时会 Freeze 底层 <see cref="SequenceBuffer{T}"/>（若由外部传入或从池中租用），以防止在持有期间被回收；调用 <see cref="Dispose"/> 会释放读取器并尝试回收缓冲区（通过 <see cref="SequenceBuffer{T}.TryRelease"/>）。
    /// - 写操作通过 <see cref="GetSpan(int)"/> / <see cref="GetMemory(int)"/> 获取写入缓冲并使用 <see cref="Advance(int)"/> 提交。
    /// </remarks>
    public struct ByteBuffer : IBufferWriter<byte>, IDisposable
    {
        /// <summary>
        /// 获取一个空的 <see cref="ByteBuffer"/> 实例（内部持有一个空的冻结缓冲区）。该实例可用于表示无数据或作为默认值，但不应进行写入操作。
        /// </summary>
        public static readonly ByteBuffer Empty = new(SequenceBuffer<byte>.Empty);

        /// <summary>
        /// 内部泛型缓冲区实现，封装实际的写入/读取逻辑。由构造器注入或从池中租用。
        /// </summary>
        public SequenceBuffer<byte> Buffer;

        /// <summary>
        /// 序列总长度（可见容量，包含已提交数据与最后一段的剩余可写空间）。
        /// </summary>
        public long Capacity => Buffer.Capacity;

        /// <summary>
        /// 判断当前包装器是否为空（内部持有空的 <see cref="SequenceBuffer{byte}"/> 实例）。
        /// </summary>
        public bool IsEmpty => Buffer == SequenceBuffer<byte>.Empty;

        /// <summary>
        /// 获取当前缓冲区已提交数据的只读序列视图（快照）。
        /// </summary>
        public ReadOnlySequence<byte> CommittedSequence => Buffer.CommittedSequence;

        /// <summary>
        /// 当前缓冲区已提交的字节数。
        /// </summary>
        public long Committed => Buffer.Committed;

        /// <summary>
        /// 从共享提供者租用缓冲并创建 <see cref="ByteBuffer"/> 的快捷构造。等同于 <see cref="ByteBuffer(DefaultSequenceBufferProvider{byte})"/> 使用共享池。
        /// </summary>
        public ByteBuffer() : this(SequenceBuffer<byte>.GetBuffer())
        {
        }

        /// <summary>
        /// 使用只读内存创建 <see cref="ByteBuffer"/> 并将数据写入内部缓冲（从共享池租用缓冲）。
        /// </summary>
        /// <param name="memory">要写入的只读内存。</param>
        public ByteBuffer(ReadOnlyMemory<byte> memory) : this()
        {
            Write(memory);
        }

        /// <summary>
        /// 使用只读序列创建 <see cref="ByteBuffer"/> 并将数据写入内部缓冲（从共享池租用缓冲）。
        /// </summary>
        /// <param name="sequence">要写入的只读序列。</param>
        public ByteBuffer(ReadOnlySequence<byte> sequence) : this()
        {
            Write(sequence);
        }



        /// <summary>
        /// 使用已有的 <see cref="SequenceBuffer{byte}"/> 创建包装器。构造时会对传入缓冲调用 <see cref="SequenceBuffer{T}.Freeze"/> 以防止在使用期间被回收。
        /// </summary>
        /// <param name="buffer">要包装并持有的序列缓冲区。</param>
        public ByteBuffer(SequenceBuffer<byte> buffer)
        {
            buffer.Freeze();
            Buffer = buffer;
        }

        /// <summary>
        /// 使用指定的内存块创建 <see cref="ByteBuffer"/> 并将该内存追加到内部缓冲（从共享池租用缓冲）。
        /// </summary>
        /// <param name="block">要追加的 <see cref="MemoryBlock{byte}"/>。</param>
        public ByteBuffer(MemoryBlock<byte> block) : this()
        {
            Buffer.Append(block);
        }

        /// <summary>
        /// 申请一个可写的 <see cref="Span{T}"/>（起始于当前写入位置）。申请写缓冲会使读取视图失效，下一次读取会刷新。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写连续容量（可为 0）。</param>
        /// <returns>可直接写入的 <see cref="Span{byte}"/>。</returns>
        /// <exception cref="ObjectDisposedException">当未持有可写序列或序列已释放时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0) => Buffer.GetSpan(sizeHint);

        /// <summary>
        /// 申请一个可写的 <see cref="Memory{T}"/>（起始于当前写入位置），适用于异步或延迟写入场景。申请写缓冲会使读取视图失效，下一次读取会刷新。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写连续容量（可为 0）。</param>
        /// <returns>可用于写入的 <see cref="Memory{byte}"/>。</returns>
        /// <exception cref="ObjectDisposedException">当未持有可写序列或序列已释放时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint = 0) => Buffer.GetMemory(sizeHint);

        #region Write

        /// <summary>
        /// 追加单个字节到缓冲区（并推进写入位置）。
        /// </summary>
        /// <param name="value">要追加的字节。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte value) => Buffer.Write(value);

        /// <summary>
        /// 追加一个字节数组到缓冲区并推进写入位置。
        /// </summary>
        /// <param name="bytes">要追加的字节数组（不能为空）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> 为 <c>null</c> 时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            Buffer.Write(bytes.AsMemory());
        }

        /// <summary>
        /// 追加多个字节数组（数组的数组），逐项写入并推进写入位置。
        /// </summary>
        /// <param name="bytes">要追加的字节数组集合（不能为空）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> 为 <c>null</c> 时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[][] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            foreach (var item in bytes)
            {
                Buffer.Write(item.AsMemory());
            }
        }

        /// <summary>
        /// 追加一段只读跨度数据并推进写入位置。
        /// </summary>
        /// <param name="value">要追加的数据。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(scoped ReadOnlySpan<byte> value) => Buffer.Write(value);

        /// <summary>
        /// 追加一段只读内存数据并推进写入位置。
        /// </summary>
        /// <param name="value">要追加的数据。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlyMemory<byte> value) => Buffer.Write(value);

        /// <summary>
        /// 追加一段只读序列数据并推进写入位置。
        /// </summary>
        /// <param name="value">要追加的数据序列。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySequence<byte> value) => Buffer.Write(value);

        #endregion Write

        /// <summary>
        /// 提交此前通过 <see cref="GetSpan(int)"/> 或 <see cref="GetMemory(int)"/> 获取的写缓冲中已写入的字节数，推进写入位置并使读取快照失效。
        /// </summary>
        /// <param name="count">已写入且需要提交的字节数量。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count) => Buffer.Advance(count);

        /// <summary>
        /// 获取指向当前可写缓冲区起始位置的引用。等价于调用 <see cref="GetSpan(int)"/> 后使用 <see cref="Span{byte}.GetPinnableReference"/>。 返回的引用可用于通过 ref 直接写入，写入完成后应调用 <see
        /// cref="Advance(int)"/> 提交已写入的元素数。
        /// </summary>
        /// <param name="sizeHint">期望的最小连续容量（提示值，允许为 0）。</param>
        /// <returns>可写缓冲区第一个元素的引用。</returns>
        /// <exception cref="ObjectDisposedException">当未持有可写序列或序列已释放时抛出。</exception>
        /// <remarks>
        /// - 返回的引用仅在下一次申请写缓冲或调用 <see cref="Advance(int)"/> 之前有效；请勿跨越这些调用缓存该引用。
        /// - 引用本身并未被固定；若需要固定以传递给非托管代码，请在 <c>fixed</c> 中使用该引用。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetPointer(int sizeHint = 0) => ref GetSpan(sizeHint).GetPinnableReference();

        /// <summary>
        /// 释放内部读取器并尝试回收底层缓冲（若为租用/持有），调用后不应再使用该实例进行读写。
        /// </summary>
        public void Dispose()
        {
            Buffer.TryRelease();
        }

        /// <summary>
        /// 将未读内容格式化为十六进制字符串，便于调试输出。每个字节以 "XX " 格式追加。
        /// </summary>
        /// <returns>十六进制表示的未读内容字符串。</returns>
        public override string ToString()
        {
            StringBuilder sb = new((int)Committed);
            foreach (var memory in CommittedSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;
                for (int i = 0; i < span.Length; i++)
                {
                    sb.AppendFormat("{0:X2} ", span[i]);
                }
            }
            return sb.ToString();
        }

        #region FormByteBuffer

        /// <summary>
        /// 将 <see cref="ByteBuffer"/> 隐式转换为其当前的未读 <see cref="ReadOnlySequence{byte}"/> 快照。
        /// </summary>
        /// <param name="buffer">源 <see cref="ByteBuffer"/> 实例（按 ref readonly 传入）。</param>
        public static implicit operator ReadOnlySequence<byte>(in ByteBuffer buffer)
            => buffer.CommittedSequence;

        /// <summary>
        /// 将 <see cref="ByteBuffer"/> 隐式转换为其内部的 <see cref="SequenceReader{byte}"/> 视图副本。
        /// </summary>
        /// <param name="buffer">源 <see cref="ByteBuffer"/> 实例（按 ref readonly 传入）。</param>
        public static implicit operator SequenceReader<byte>(in ByteBuffer buffer)
            => new(buffer);

        #endregion FormByteBuffer

        #region ToByteBuffer

        /// <summary>
        /// 使用指定的 <see cref="SequenceBuffer{byte}"/> 隐式创建一个 <see cref="ByteBuffer"/>（构造器会 Freeze 传入缓冲）。
        /// </summary>
        /// <param name="buffer">源 <see cref="SequenceBuffer{byte}"/>。</param>
        public static implicit operator ByteBuffer(in SequenceBuffer<byte> buffer)
            => new ByteBuffer(buffer);


        /// <summary>
        /// 使用只读序列显式创建 <see cref="ByteBuffer"/>（将序列内容复制到新缓冲）。
        /// </summary>
        /// <param name="sequence">用于初始化的新序列。</param>
        public static explicit operator ByteBuffer(ReadOnlySequence<byte> sequence)
            => new ByteBuffer(sequence);

        /// <summary>
        /// 使用只读内存显式创建 <see cref="ByteBuffer"/>（将内存内容复制到新缓冲）。
        /// </summary>
        /// <param name="memory">用于初始化的新内存。</param>
        public static explicit operator ByteBuffer(ReadOnlyMemory<byte> memory)
            => new ByteBuffer(memory);

        #endregion ToByteBuffer
    }
}