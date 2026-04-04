using System.Buffers;
using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 提供 <see cref="AbstractBuffer{T}"/> 与 <see cref="MemoryBlock{T}"/> 的扩展方法。
    /// </summary>
    public static class MemoryBlockExtensions
    {
        /// <summary>
        /// 将 <see cref="AbstractBuffer{T}"/> 转换为 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>包含已提交数据的 <see cref="MemoryBlock{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> ToMemoryBlock<T>(this AbstractBuffer<T> buffer)
        {
            if (buffer is MemoryBlock<T> mb)
            {
                return mb;
            }

            MemoryBlock<T> memoryBlock = MemoryBlock<T>.GetBuffer((int)buffer.Committed);
            ReadOnlySequence<T> memories = buffer.CommittedSequence;
            SequencePosition position = memories.Start;
            while (memories.TryGet(ref position, out var memory))
            {
                memoryBlock.Write(memory);
            }
            return memoryBlock;
        }

        /// <summary>
        /// 将 <see cref="AbstractBuffer{T}"/> 克隆为新的 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>包含已提交数据的新 <see cref="MemoryBlock{T}"/>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> CloneToMemoryBlock<T>(this AbstractBuffer<T> buffer)
        {
            MemoryBlock<T> memoryBlock = MemoryBlock<T>.GetBuffer((int)buffer.Committed);
            if (buffer is MemoryBlock<T> mb)
            {
                Write(memoryBlock, mb);
                return memoryBlock;
            }

            ReadOnlySequence<T> memories = buffer.CommittedSequence;
            SequencePosition position = memories.Start;
            while (memories.TryGet(ref position, out var memory))
            {
                memoryBlock.Write(memory);
            }
            return memoryBlock;
        }

        #region Write

        /// <summary>
        /// 将 <see cref="MemoryBlock{T}"/> 的已提交内容写入另一个 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="sourceBuffer">源内存块。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> Write<T>(this MemoryBlock<T> buffer, MemoryBlock<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSpan);
            return buffer;
        }

        /// <summary>
        /// 将 <see cref="SequenceBuffer{T}"/> 的已提交内容写入 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">目标内存块。</param>
        /// <param name="sourceBuffer">源序列缓冲区。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> Write<T>(this MemoryBlock<T> buffer, SequenceBuffer<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSequence);
            return buffer;
        }

        /// <summary>
        /// 将 <see cref="MemoryBlock{T}"/> 的已提交内容写入 <see cref="AbstractBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="sourceBuffer">源内存块。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbstractBuffer<T> Write<T>(this AbstractBuffer<T> buffer, MemoryBlock<T> sourceBuffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            ArgumentNullException.ThrowIfNull(sourceBuffer, nameof(sourceBuffer));

            buffer.Write(sourceBuffer.CommittedSpan);
            return buffer;
        }

        #endregion Write

        #region Slice

        /// <summary>
        /// 获取当前 <see cref="MemoryBlock{T}"/> 中尚未提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从已提交末尾开始、长度为可用空间的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> AvailableSlice<T>(this MemoryBlock<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(buffer.Committed, buffer.Available);
        }

        /// <summary>
        /// 获取当前 <see cref="MemoryBlock{T}"/> 中已提交部分的切片。
        /// </summary>
        /// <typeparam name="T">缓冲区的元素类型。</typeparam>
        /// <param name="buffer">源缓冲区。</param>
        /// <returns>从起始位置开始、长度为已提交数据的切片。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryBlock<T> CommittedSlice<T>(this MemoryBlock<T> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
            return buffer.Slice(0, buffer.Committed);
        }

        #endregion Slice
    }
}