using System.Buffers;
using ExtenderApp.Buffer.SequenceBuffers;

namespace ExtenderApp.Buffer.Reader
{
    /// <summary>
    /// 针对内部的 <see cref="SequenceBuffer{T}"/> 实现的缓冲读取器，支持基于段的推进逻辑。 此类维护当前所在段及在段内的已消费偏移，并覆盖了 <see cref="AbstractBufferReader{T}.Advance(long)"/> 以实现跨段推进的行为。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public class SequenceBufferReader<T> : AbstractBufferReader<T>
    {
        /// <summary>
        /// 从指定的 <see cref="SequenceBuffer{T}"/> 获取一个读取器实例。此方法便捷地调用默认提供者。
        /// </summary>
        /// <param name="buffer">目标序列缓冲区。</param>
        /// <returns>绑定到指定缓冲区的读取器。</returns>
        public static SequenceBufferReader<T> GetReader(SequenceBuffer<T> buffer)
            => (SequenceBufferReader<T>)SequenceBufferReaderProvider<T>.Default.GetReader(buffer);

        /// <summary>
        /// 内部使用的读取器提供者引用（用于回收/复用）。 使用 <c>new</c> 隐藏基类成员，仅供同程序集或提供者实现访问。
        /// </summary>
        internal new SequenceBufferReaderProvider<T>? ReaderProvider { get; set; }

        /// <summary>
        /// 特化后的缓冲区引用，隐藏基类的同名属性以返回具体的 <see cref="SequenceBuffer{T}"/> 类型。 仅由框架内部设置。
        /// </summary>
        public new SequenceBuffer<T> Buffer => (SequenceBuffer<T>)base.Buffer;

        private SequenceBufferSegment<T>? currentSegment;
        private long segmentConsumed; // 已在当前段内消费的数量（相对于段起点）

        /// <summary>
        /// 获取从当前已消费位置到已提交末尾的只读序列视图。 若当前未定位到任何段则返回 <see cref="ReadOnlySequence{T}.Empty"/>。
        /// </summary>
        /// <remarks>
        /// 使用与 <see cref="MemoryBlockReader{T}.UnreadSequence"/> 相同的 <see cref="ReadOnlySequence{T}.Slice(long)"/> 方式构造，
        /// 避免用手工四参数构造函数时与段 <see cref="System.Buffers.ReadOnlySequenceSegment{T}.Memory"/> 边界不一致而导致 <see cref="ReadOnlySequence{T}.CopyTo"/> 抛出异常。
        /// </remarks>
        public override ReadOnlySequence<T> UnreadSequence
        {
            get
            {
                if (Buffer.First == null || Buffer.Last == null)
                    return ReadOnlySequence<T>.Empty;

                return Buffer.CommittedSequence.Slice(Consumed);
            }
        }

        /// <summary>
        /// 创建一个未初始化的读取器实例。实际使用前会由提供者或外部设置 <see cref="Buffer"/> 与定位信息。
        /// </summary>
        public SequenceBufferReader()
        {
            segmentConsumed = 0;
        }

        /// <summary>
        /// 初始化读取器状态并绑定到指定缓冲区。供提供者在分配读取器时调用。
        /// </summary>
        /// <param name="buffer">要绑定的序列缓冲区。</param>
        protected internal override void Initialize(AbstractBufferReaderProvider<T> provider, AbstractBuffer<T> buffer)
        {
            base.Initialize(provider, buffer);
            currentSegment = Buffer.First;
            segmentConsumed = 0;
            Consumed = 0;
        }

        public override void Release()
        {
            base.Release();
            currentSegment = null;
            segmentConsumed = 0;
        }

        /// <summary>
        /// 将读取位置向前推进指定数量的元素（支持跨段推进）。 此实现会在内部维护当前段与段内偏移，并递增 <see cref="AbstractBufferReader{T}.Consumed"/>。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负且不超过已提交的总长度）。</param>
        public override sealed void Advance(long count)
        {
            if (count < 0 || Consumed + count > Buffer.Committed)
                throw new ArgumentOutOfRangeException(nameof(count));

            // 若尚未定位到起始段，则尝试定位
            if (currentSegment == null)
            {
                if (!TryMoveToNextSegment())
                    ThrowIfNoFindNextSegment();
            }

            long remaining = count;
            while (remaining > 0)
            {
                if (currentSegment == null) ThrowIfNoFindNextSegment();

                long segCommitted = currentSegment.Committed;
                long avail = segCommitted - segmentConsumed;

                if (avail == 0)
                {
                    // 当前段已无可用数据，移动到下一段
                    if (!TryMoveToNextSegment()) ThrowIfNoFindNextSegment();
                    continue;
                }

                long step = Math.Min(avail, remaining);
                segmentConsumed += step;
                Consumed += step;
                remaining -= step;

                if (segmentConsumed >= segCommitted)
                {
                    // 如果恰好或超过段内已提交数，移动到下一段并重置段内偏移
                    if (!TryMoveToNextSegment())
                    {
                        // 如果没有下一段但已消费完请求的数量则正常结束，否则抛出
                        if (remaining > 0) ThrowIfNoFindNextSegment();
                    }
                }
            }
        }

        /// <summary>
        /// 尝试将当前段移动到下一段，并重置段内已消费偏移。返回是否成功找到下一段。
        /// </summary>
        /// <returns></returns>
        private bool TryMoveToNextSegment()
        {
            if (currentSegment == null)
            {
                currentSegment = Buffer.First;
                segmentConsumed = 0;
                return currentSegment != null;
            }
            if (currentSegment.Next != null)
            {
                currentSegment = currentSegment.Next;
                segmentConsumed = 0;
                return true;
            }
            // 无法移动到下一段时将 currentSegment 设为 null 以明确序列已结束，避免上层循环误判造成无限循环
            currentSegment = null;
            segmentConsumed = 0;
            return false;
        }

        /// <summary>
        /// 当无法找到下一个段时抛出异常，通常表示推进请求超过了已提交的总长度或当前段已无可用数据。
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void ThrowIfNoFindNextSegment()
        {
            if (currentSegment == null)
                throw new InvalidOperationException("无法找到下一个序列！");
        }

        public static implicit operator SequenceBuffer<T>(SequenceBufferReader<T> reader)
            => reader.Buffer;

        public static implicit operator SequenceBufferReader<T>(SequenceBuffer<T> buffer)
            => SequenceBufferReader<T>.GetReader(buffer);
    }
}