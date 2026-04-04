using System.Buffers;
using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 栈上适配器：包装一个 <see cref="IBufferWriter{byte}"/> 实例以提供零分配写入帮助方法。 作为 <c>ref struct</c>，此类型只能在栈上使用，适合热路径以最小化 GC 分配。
    /// </summary>
    public struct BinaryWriterAdapter
    {
        private readonly IBufferWriter<byte> _writer;

        /// <summary>
        /// 通过此适配器已写入的字节总数。
        /// </summary>
        public long Written { get; private set; }

        /// <summary>
        /// 获取一个值，指示此适配器是否未包装任何写入器实例（即处于默认状态）。 在默认状态下，调用任何写入方法都会抛出 <see cref="NullReferenceException"/>。
        /// </summary>
        public bool IsEmpty => _writer == null;

        /// <summary>
        /// 创建一个新的适配器，包装指定的写入器实例。
        /// </summary>
        /// <param name="writer">要包装的目标 <see cref="IBufferWriter{byte}"/>，不能为空。</param>
        public BinaryWriterAdapter(IBufferWriter<byte> writer)
        {
            _writer = writer;
        }

        /// <summary>
        /// 获取一个用于写入的 <see cref="Span{byte}"/>，至少具有 <paramref name="sizeHint"/> 长度（由具体写入器决定）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0) => _writer.GetSpan(sizeHint);

        /// <summary>
        /// 提交此前通过 <see cref="GetSpan(int)"/> 获取并写入的字节数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            _writer.Advance(count);
            Written += count;
        }

        /// <summary>
        /// 写入单个字节并推进写入位置。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte value)
        {
            var span = _writer.GetSpan(1);
            span[0] = value;
            _writer.Advance(1);
        }

        /// <summary>
        /// 将 <paramref name="source"/> 的内容写入目标写入器并推进位置。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty)
                return;
            var dest = _writer.GetSpan(source.Length);
            source.CopyTo(dest);
            _writer.Advance(source.Length);
        }

        /// <summary>
        /// 将分段序列的所有段写入目标写入器（按顺序）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySequence<byte> sequence)
        {
            foreach (var segment in sequence)
            {
                Write(segment.Span);
            }
        }
    }
}