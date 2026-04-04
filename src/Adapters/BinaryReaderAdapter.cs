using System.Buffers;
using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 栈上适配器：基于 <see cref="ReadOnlySequence{byte}"/> 提供零分配读取帮助方法。 作为 <c>ref struct</c>，此类型只能在栈上使用，适合热路径以最小化 GC 分配。
    /// </summary>
    public ref struct BinaryReaderAdapter
    {
        private SequenceReader<byte> _reader;

        /// <summary>
        /// 获取当前读取器是否没有任何数据可供读取（即底层序列为空）。请注意，即使 <see cref="IsEmpty"/> 返回 false，也可能存在剩余字节数为零的情况（例如，序列包含空段）。因此，在使用前建议同时检查 <see cref="Remaining"/> 属性以确保有足够的数据可供读取。
        /// </summary>
        public bool IsEmpty => _reader.Sequence.IsEmpty;

        /// <summary>
        /// 使用指定的只读序列创建读取器适配器。
        /// </summary>
        /// <param name="sequence">要读取的序列。</param>
        public BinaryReaderAdapter(ReadOnlySequence<byte> sequence)
        {
            _reader = new SequenceReader<byte>(sequence);
        }

        public BinaryReaderAdapter(SequenceReader<byte> reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// 是否已到达序列末尾。
        /// </summary>
        public readonly bool End => _reader.End;

        /// <summary>
        /// 已消费的字节数。
        /// </summary>
        public readonly long Consumed => _reader.Consumed;

        /// <summary>
        /// 剩余可读的字节数。
        /// </summary>
        public readonly long Remaining => _reader.Remaining;

        /// <summary>
        /// 获取当前读取位置之前未读取的字节序列。请注意，此属性返回的序列可能包含多个段，因此在使用时需要考虑跨段读取的情况。
        /// </summary>
        public readonly ReadOnlySequence<byte> UnreadSequence => _reader.UnreadSequence;

        /// <summary>
        /// 将读取位置向前移动指定的字节数。请确保在调用此方法前已验证剩余字节数足够，否则可能会导致异常或不正确的行为。
        /// </summary>
        /// <param name="count">要移动的字节数。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count) => _reader.Advance(count);

        /// <summary>
        /// 尝试预览下一个字节但不移动读取位置，若已到达末尾则返回 false。
        /// </summary>
        /// <param name="value">输出参数，用于存储预览的字节值。</param>
        /// <returns>如果成功预览到字节，则返回 true；否则返回 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(out byte value) => _reader.TryPeek(out value);

        /// <summary>
        /// 尝试预览指定长度的字节到目标跨度但不移动读取位置，若剩余字节不足则返回 false。
        /// </summary>
        /// <param name="destination">目标跨度，用于存储预览的字节。</param>
        /// <returns>如果成功预览到指定长度的字节，则返回 true；否则返回 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(scoped Span<byte> destination)
        {
            if (destination.IsEmpty || destination.Length == 0 || Remaining < destination.Length)
            {
                return false;
            }
            int length = destination.Length;
            var seq = _reader.Sequence.Slice(_reader.Position, length);
            int offset = 0;
            foreach (var segment in seq)
            {
                var span = segment.Span;
                span.CopyTo(destination.Slice(offset, span.Length));
                offset += span.Length;
            }
            return true;
        }

        /// <summary>
        /// 尝试读取下一个字节并移动读取位置，若已到达末尾则返回 false。
        /// </summary>
        /// <param name="value">输出参数，用于存储读取的字节值。</param>
        /// <returns>如果成功读取到字节，则返回 true；否则返回 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out byte value) => _reader.TryRead(out value);

        /// <summary>
        /// 尝试读取指定长度的字节到目标跨度，若剩余字节不足则返回 false。
        /// </summary>
        /// <param name="length">需要读取的字节数。</param>
        /// <param name="destination">目标跨度，长度应 &gt;= length。</param>
        /// <returns>若读取成功返回 true，否则返回 false。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(scoped Span<byte> destination)
        {
            if (destination.IsEmpty || destination.Length == 0 || Remaining < destination.Length)
            {
                return false;
            }

            int length = destination.Length;
            var seq = _reader.Sequence.Slice(_reader.Position, length);
            int offset = 0;
            foreach (var segment in seq)
            {
                var span = segment.Span;
                span.CopyTo(destination.Slice(offset, span.Length));
                offset += span.Length;
            }
            Advance(length);
            return true;
        }

        /// <summary>
        /// 将当前读取器的剩余字节转换为字节数组。请注意，这可能会导致 GC 分配，适用于需要完整数据副本的场景。
        /// </summary>
        /// <returns>返回包含当前读取器剩余字节的字节数组。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ToArray() => _reader.Sequence.ToArray();


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public bool TryReadTo(out ReadOnlySequence<byte> value, SequencePosition delimiter)
        //{
        //    // 直接暴露 SequenceReader.TryReadTo
        //    return _reader.TryReadTo(out value, delimiter);
        //}
    }
}