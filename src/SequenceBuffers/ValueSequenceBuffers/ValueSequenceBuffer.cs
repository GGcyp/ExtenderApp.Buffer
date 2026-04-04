using System.Buffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer.SequenceBuffers
{
    /// <summary>
    /// 值类型的序列缓冲区包装器（轻量 ref struct），用于在栈上快速访问并操作 <see cref="SequenceBuffer{T}"/> 实例。 构造时会冻结底层缓冲区以防止被回收；使用完成后应调用 <see cref="Dispose"/> 以尝试释放底层缓冲。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public struct ValueSequenceBuffer<T>
    {
        /// <summary>
        /// 底层序列缓冲区实例（由构造器注入或从池中租用）。
        /// </summary>
        internal FastSequence<T> Buffer;

        /// <summary>
        /// 当前缓冲区已提交数据的只读序列视图。
        /// </summary>
        public ReadOnlySequence<T> CommittedSequence => Buffer;

        /// <summary>
        /// 当前缓冲区已提交的元素数量。
        /// </summary>
        public long Committed => Buffer.Committed;

        /// <summary>
        /// 表示包装器是否为空（内部持有空的 <see cref="FastSequence{T}"/> 实例）。
        /// </summary>
        public bool IsEmpty => Buffer == null;

        public ValueSequenceBuffer() : this(default!)
        {
        }

        /// <summary>
        /// 使用指定的 <see cref="FastSequence{T}"/> 构造包装器。构造时会对传入缓缓冲区调用 <see cref="FastSequence{T}.Freeze"/>。
        /// </summary>
        /// <param name="buffer">要包装并持有的序列缓冲区。</param>
        public ValueSequenceBuffer(FastSequence<T> buffer)
        {
            Buffer = buffer;
        }

        /// <summary>
        /// 申请一个可写的 <see cref="Span{T}"/>（起始于当前写入位置）。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写连续容量（提示值，允许为 0）。</param>
        /// <returns>可直接写入的 <see cref="Span{T}"/>。</returns>
        public Span<T> GetSpan(int sizeHint = 0) => Buffer.GetSpan(sizeHint);

        /// <summary>
        /// 申请一个可写的 <see cref="Memory{T}"/>（起始于当前写入位置）。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写连续容量（提示值，允许为 0）。</param>
        /// <returns>用于写入的 <see cref="Memory{T}"/>。</returns>
        public Memory<T> GetMemory(int sizeHint = 0) => Buffer.GetMemory(sizeHint);

        /// <summary>
        /// 将指定的只读跨度写入底层缓冲并推进写入位置。
        /// </summary>
        /// <param name="value">要写入的数据。</param>
        public void Write(scoped ReadOnlySpan<T> value) => Buffer.Write(value);

        /// <summary>
        /// 将指定的只读内存写入底层缓冲并推进写入位置。
        /// </summary>
        /// <param name="value">要写入的数据。</param>
        public void Write(ReadOnlyMemory<T> value) => Buffer.Write(value.Span);

        /// <summary>
        /// 将指定的只读序列写入底层缓冲并推进写入位置。
        /// </summary>
        /// <param name="value">要写入的数据序列。</param>
        public void Write(ReadOnlySequence<T> value)
        {
            foreach (var segment in value)
            {
                Buffer.Write(segment.Span);
            }
        }

        /// <summary>
        /// 提交此前通过 <see cref="GetSpan(int)"/> 或 <see cref="GetMemory(int)"/> 获取的写缓冲中已写入的元素数，推进写入位置并使读取快照失效。
        /// </summary>
        /// <param name="count">已写入且需要提交的元素数量。</param>
        public void Advance(int count) => Buffer.Advance(count);

        /// <summary>
        /// 释放内部持有的缓冲引用并尝试回收底层缓冲（若为租用/持有）。调用后不应再使用该实例进行读写。
        /// </summary>
        public void Dispose() => Buffer.TryRelease();

        /// <summary>
        /// 将当前未读内容转换为十六进制字符串，便于调试输出。
        /// </summary>
        /// <returns>十六进制表示的未读内容字符串。</returns>
        public override string ToString() => Buffer?.ToString() ?? "当前缓冲区为空";

        /// <summary>
        /// 隐式将包装器转换为其当前的未读 <see cref="ReadOnlySequence{T}"/> 快照。
        /// </summary>
        /// <param name="buffer">源包装器（按 ref readonly 传入）。</param>
        public static implicit operator ReadOnlySequence<T>(in ValueSequenceBuffer<T> buffer)
            => buffer.CommittedSequence;

        /// <summary>
        /// 隐式将包装器转换为其内部的 <see cref="SequenceBuffer{T}"/> 实例（按 ref 传入）。
        /// </summary>
        public static implicit operator SequenceBuffer<T>(in ValueSequenceBuffer<T> buffer)
            => SequenceBufferProvider<T>.Shared.GetBuffer(buffer);
    }
}