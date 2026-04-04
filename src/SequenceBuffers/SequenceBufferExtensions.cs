using System.Runtime.CompilerServices;
using ExtenderApp.Buffer.SequenceBuffers;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 提供 <see cref="SequenceBuffer{T}"/> 的扩展方法。
    /// </summary>
    public static class SequenceBufferExtensions
    {
        /// <summary>
        /// 将 <see cref="AbstractBuffer{T}"/> 转换为 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>转换后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> ToSequenceBuffer<T>(this AbstractBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            if (buffer is SequenceBuffer<T> sequence)
                return sequence;

            sequence = SequenceBuffer<T>.GetBuffer();
            sequence.Append(buffer);

            return sequence;
        }

        /// <summary>
        /// 将 <see cref="AbstractBuffer{T}"/> 克隆为新的 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>克隆后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> CloneToSequenceBuffer<T>(this AbstractBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            if (buffer is SequenceBuffer<T> sequenceBuffer)
                return sequenceBuffer.Clone();

            var clonedBuffer = SequenceBuffer<T>.GetBuffer();
            clonedBuffer.Append(buffer);
            return clonedBuffer;
        }

        /// <summary>
        /// 将 <see cref="SequenceBuffer{T}"/> 转换为 <see cref="IList{ArraySegment{T}}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>转换后的 <see cref="IList{ArraySegment{T}}"/>，如果缓冲区为空则返回 null。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IList<ArraySegment<T>>? ToArraySegments<T>(this SequenceBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            return buffer.First;
        }

        #region Write

        /// <summary>
        /// 将 <see cref="MemoryBlock{T}"/> 的已提交部分写入 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">目标序列缓冲区。</param>
        /// <param name="sourceBuffer">源内存块。</param>
        /// <returns>写入后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> Write<T>(this SequenceBuffer<T> buffer, MemoryBlock<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSpan);
            return buffer;
        }

        /// <summary>
        /// 将 <see cref="SequenceBuffer{T}"/> 的已提交部分写入另一个 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">目标序列缓冲区。</param>
        /// <param name="sourceBuffer">源序列缓冲区。</param>
        /// <returns>写入后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> Write<T>(this SequenceBuffer<T> buffer, SequenceBuffer<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSequence);
            return buffer;
        }

        public static AbstractBuffer<T> Write<T>(this AbstractBuffer<T> buffer, SequenceBuffer<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSequence);
            return buffer;
        }

        #endregion Write

        #region Append

        /// <summary>
        /// 将 <see cref="MemoryBlock{T}"/> 追加到 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="sequenceBuffer">目标序列缓冲区。</param>
        /// <param name="memoryBlock">要追加的内存块。</param>
        /// <returns>追加后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> Append<T>(this SequenceBuffer<T> sequenceBuffer, MemoryBlock<T> memoryBlock)
        {
            ArgumentNullException.ThrowIfNull(sequenceBuffer, nameof(sequenceBuffer));
            ArgumentNullException.ThrowIfNull(memoryBlock, nameof(memoryBlock));

            var segment = SequenceBufferSegmentProvider<T>.Shared.GetSegment(memoryBlock);
            sequenceBuffer.Append(segment);
            return sequenceBuffer;
        }

        /// <summary>
        /// 将源缓冲区内容追加到目标 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="targetBuffer">目标缓冲区，必须为 <see cref="SequenceBuffer{T}"/>。</param>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>追加后的 <see cref="SequenceBuffer{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> Append<T>(this AbstractBuffer<T> targetBuffer, AbstractBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(targetBuffer, nameof(targetBuffer));
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            if (targetBuffer is not SequenceBuffer<T> sequenceBuffer)
                throw new ArgumentException("目标缓冲区必须是 SequenceBuffer<T> 类型。", nameof(targetBuffer));

            if (buffer is MemoryBlock<T> memoryBlock)
            {
                sequenceBuffer.Append(memoryBlock);
            }
            else if (buffer is SequenceBuffer<T> sourceSequenceBuffer)
            {
                var segment = sourceSequenceBuffer.First;
                while (segment != null)
                {
                    sequenceBuffer.Append(segment.Clone());
                    segment = segment.Next;
                }
            }

            return sequenceBuffer;
        }

        #endregion Append

        #region Slice

        /// <summary>
        /// 获取当前 <see cref="SequenceBuffer{T}"/> 中尚未提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从已提交末尾开始、长度为可用空间的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> AvailableSlice<T>(this SequenceBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(buffer.Committed, buffer.Available);
        }

        /// <summary>
        /// 获取当前 <see cref="SequenceBuffer{T}"/> 中已提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从起始位置开始、长度为已提交数据的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequenceBuffer<T> CommittedSlice<T>(this SequenceBuffer<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(0, buffer.Committed);
        }

        #endregion Slice
    }
}