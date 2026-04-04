using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// 提供用于 <see cref="FastSequence{T}"/> 的扩展方法。
    /// </summary>
    public static class FastSequenceExtensions
    {
        /// <summary>
        /// 将给定的 <see cref="FastSequence{T}"/> 转换为 <see cref="AbstractBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">序列中元素的类型。</typeparam>
        /// <param name="sequence">要转换的 <see cref="FastSequence{T}"/> 实例。</param>
        /// <returns>
        /// 如果 <paramref name="sequence"/> 为空，则返回 <see cref="AbstractBuffer{T}.Empty"/>；
        /// 否则返回一个表示序列数据的 <see cref="AbstractBuffer{T}"/>。
        /// 当序列仅包含单个段时，会尝试从数组池获取缓冲区并复制/引用该段；
        /// 否则将通过 <see cref="SequenceBufferProvider{T}.Shared"/> 创建一个序列缓冲区并返回。
        /// </returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="sequence"/> 为 <c>null</c> 时抛出。</exception>
        /// <remarks>
        /// 本方法会在必要时调用 <see cref="FastSequence{T}.PinSegmentArray"/> 来固定段数组，
        /// 并根据序列是否为单段选择更高效的缓冲获取路径。
        /// </remarks>
        public static AbstractBuffer<T> ToBuffer<T>(this FastSequence<T> sequence)
        {
            ArgumentNullException.ThrowIfNull(sequence, nameof(sequence));

            if (sequence.IsEmpty)
                return AbstractBuffer<T>.Empty;

            sequence.PinSegmentArray();
            AbstractBuffer<T>? buffer = default!;
            if (sequence.IsSingleSegment)
            {
                var fristSegment = sequence.First!.CommittedSegment;
                buffer = ArrayPoolBlockProvider<T>.Default.GetBuffer(fristSegment.Array!, fristSegment.Count);
            }
            return buffer ?? SequenceBufferProvider<T>.Shared.GetBuffer(sequence);
        }
    }
}