using System.Buffers;
using ExtenderApp.Buffer.SequenceBuffers;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 基础序列实现，使用段提供者管理多个段并实现 <see cref="IBufferWriter{T}"/>。 提供在首部/末尾/中间插入段以及按位置查找段的功能。
    /// </summary>
    /// <typeparam name="T">序列中元素的类型。</typeparam>
    public sealed partial class SequenceBuffer<T> : AbstractBuffer<T>
    {
        public new static readonly SequenceBuffer<T> Empty = new SequenceBuffer<T>();

        private static readonly ReadOnlySequence<T> EmptySequence = new ReadOnlySequence<T>(SequenceBufferSegment<T>.Empty, 0, SequenceBufferSegment<T>.Empty, 0);

        private readonly SequenceBufferSegmentProvider<T> _segmentProvider;

        /// <summary>
        /// 获取或设置序列的最小跨度（已提交）长度，供自动增长策略使用。
        /// </summary>
        private int minimumSpanCommitted;

        private SequenceBufferProvider<T>? ownerProvider;

        /// <summary>
        /// 获取或设置是否自动增加最小跨度长度。
        /// </summary>
        public bool AutoIncreaseMinimumSpanCommitted { get; set; }

        /// <summary>
        /// 返回表示已提交数据的只读序列。
        /// </summary>
        public override ReadOnlySequence<T> CommittedSequence => this;

        /// <summary>
        /// 当前序列中已提交（已写入）元素的总数。
        /// </summary>
        public override long Committed
        {
            get
            {
                if (First == null || Last == null)
                    return 0;

                return Last.RunningIndex + Last.Committed;
            }
        }

        /// <summary>
        /// 序列可见的总容量（已提交 + 最后一段剩余可写空间）。
        /// </summary>
        public override long Capacity
        {
            get
            {
                if (First == null || Last == null)
                    return 0;

                return Last.RunningIndex + Last.Committed + Last.Available;
            }
        }

        /// <summary>
        /// 当前可直接写入的元素数量（最后一段的剩余可写空间）。
        /// </summary>
        public override int Available
        {
            get
            {
                if (Last == null)
                    return 0;

                return Last.Available;
            }
        }

        /// <summary>
        /// 获取当前序列中段的数量（即链表中的段数）。如果序列为空，则返回 0。
        /// </summary>
        public int Count => First?.Count ?? 0;

        /// <summary>
        /// 序列中的第一段，或 null（如果序列为空）。 该属性提供对段链表头部的访问，允许外部代码遍历或操作段链表。
        /// </summary>
        internal SequenceBufferSegment<T>? First { get; private set; }

        /// <summary>
        /// 序列中的最后一段，或 null（如果序列为空）。 该属性提供对段链表尾部的访问，允许外部代码快速定位到当前写入位置所在的段。
        /// </summary>

        internal SequenceBufferSegment<T>? Last { get; private set; }

        public SequenceBuffer() : this(SequenceBufferSegmentProvider<T>.Shared)
        {
        }

        /// <summary>
        /// 使用指定的段提供者创建序列实例。
        /// </summary>
        /// <param name="segmentProvider">用于创建或复用序列段的提供者，不能为空。</param>
        public SequenceBuffer(SequenceBufferSegmentProvider<T> segmentProvider)
        {
            ownerProvider = default!;
            _segmentProvider = segmentProvider;
        }

        /// <summary>
        /// 获取具有至少 <paramref name="sizeHint"/> 可写空间的内存段，返回值从当前写入位置开始。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写元素数，0 表示不限。</param>
        /// <returns>用于写入的 <see cref="Memory{T}"/>。</returns>
        public override Memory<T> GetMemory(int sizeHint = 0) => GetSegment(sizeHint).GetMemory(sizeHint);

        /// <summary>
        /// 获取具有至少 <paramref name="sizeHint"/> 可写空间的跨度，返回值从当前写入位置开始。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写元素数，0 表示不限。</param>
        /// <returns>用于写入的 <see cref="Span{T}"/>。</returns>
        public override Span<T> GetSpan(int sizeHint = 0) => GetSegment(sizeHint).GetSpan(sizeHint);

        /// <summary>
        /// 将当前写入位置向前推进 <paramref name="count"/> 个元素。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负值，且不得超过当前段的可写范围）。</param>
        /// <exception cref="InvalidOperationException">若在未获取内存前调用该方法将抛出。</exception>
        public override void Advance(int count)
        {
            CheckWriteFrozen();
            if (Last is null)
                throw new InvalidOperationException("在获取内存之前不能进行推进操作");

            Last.Advance(count);
            ConsiderMinimumSizeIncrease();
        }

        /// <summary>
        /// 获取具有至少 <paramref name="sizeHint"/> 可写空间的段实例，优先使用当前最后一段的剩余空间，否则创建新段并追加到链表末尾。 该方法确保返回的段具有足够的可写空间以满足 sizeHint 的要求，并且在必要时会自动增加
        /// minimumSpanCommitted 以优化未来的段创建。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写元素数，0 表示不限。</param>
        /// <returns>具有足够可写空间的段实例。</returns>
        private SequenceBufferSegment<T> GetSegment(int sizeHint)
        {
            if (Last != null && Last.Available >= sizeHint)
                return Last;

            sizeHint = System.Math.Max(minimumSpanCommitted, sizeHint);
            return GetSegmentForProvider(sizeHint);
        }

        /// <summary>
        /// 获取具有至少 <paramref name="sizeHint"/> 可写空间的段实例，直接通过提供者获取新段并追加到链表末尾。 该方法不考虑当前最后一段的剩余空间，适用于需要强制创建新段的场景（如某些协议头的预留空间）。 该方法确保返回的段具有足够的可写空间以满足
        /// sizeHint 的要求，并且在必要时会自动增加 minimumSpanCommitted 以优化未来的段创建。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写元素数，0 表示不限。</param>
        /// <returns>具有足够可写空间的段实例。</returns>
        private SequenceBufferSegment<T> GetSegmentForProvider(int sizeHint)
        {
            // 使用 ownerProvider 获取新的段实例（实现可以从池中租用或新建）。
            var Segment = _segmentProvider.GetSegment(sizeHint);
            Append(Segment);
            return Segment;
        }

        #region Segment Operations

        /// <summary>
        /// 将指定段追加到序列末尾。若当前最后一段已部分消费，则直接链入；否则替换空段并释放原段链。
        /// </summary>
        /// <param name="segment">要追加的段，不能为空且应为未链接的段实例。</param>
        public void Append(SequenceBufferSegment<T> segment)
        {
            CheckWriteFrozen();
            ArgumentNullException.ThrowIfNull(segment, nameof(segment));

            // 防止重复插入已链接的段，要求传入段为未链接状态
            if (segment.Prev != null || segment.Next != null)
                throw new InvalidOperationException("要追加的段必须是未链接的段实例。");

            if (Last == null)
            {
                First = segment;
                Last = segment;
                return;
            }

            // 若 last 已被消费过一部分，直接在其后追加新段
            if (Last.Committed > 0)
            {
                Last.SetNext(segment);
                Last = segment;
                return;
            }

            // last 是空（或未消费）且需要替换为新段 —— 先完成链入再释放旧段，确保在释放期间链表保持可恢复状态
            var oldLast = Last;
            var prev = oldLast.Prev;

            if (prev == null)
                First = segment;      // oldLast 是首段，直接将新段设为首段
            else
                prev.SetNext(segment);// 将 prev 的 next 指向新段

            oldLast.Release();
            Last = segment;
            Last.UpdateRunningIndex(); // 确保新段的 RunningIndex 是正确的
        }

        /// <summary>
        /// 将指定段插入到序列头部，成为新的首段。 该方法适用于需要在序列前面添加数据的场景（如某些协议头的预留空间）。 传入的段必须是未链接状态。 若当前序列为空，则新段同时成为首段和尾段。
        /// </summary>
        /// <param name="segment">要插入的段，不能为空且应为未链接的段实例。</param>
        private void Prepend(SequenceBufferSegment<T> segment)
        {
            CheckWriteFrozen();
            if (First == null)
            {
                First = segment;
                Last = segment;
                return;
            }

            // 将新段作为头部，并连接原先的头部为下一个
            segment.SetNext(First);
            First = segment;
            First.UpdateRunningIndex();
        }

        /// <summary>
        /// 在指定的现有段之后插入一个新段，并在必要时更新尾部引用。
        /// </summary>
        /// <param name="existing">现有段，不能为空且必须属于当前序列。</param>
        /// <param name="segment">要插入的新段，不能为空。</param>
        private void InsertAfter(SequenceBufferSegment<T> existing, SequenceBufferSegment<T> segment)
        {
            segment.SetNext(existing.Next, false);
            existing.SetNext(segment);

            if (existing == Last)
                Last = segment;
        }

        /// <summary>
        /// 在指定的现有段之前插入一个新段（保证链表完整性）。
        /// </summary>
        /// <param name="existing">现有段，不能为空且必须属于当前序列。</param>
        /// <param name="segment">要插入的新段，不能为空。</param>
        private void InsertBefore(SequenceBufferSegment<T> existing, SequenceBufferSegment<T> segment)
        {
            if (existing.Prev == null)
            {
                Prepend(segment);
                return;
            }
            else
            {
                existing.Prev.SetNext(segment, false);
                segment.SetNext(existing);
            }
        }

        /// <summary>
        /// 在序列中根据绝对位置（从 0 开始）插入段，插入点位于包含该位置的段之前。 若 position 指向段首，则插入在该段之前；position 等于 <see cref="Committed"/> 等价于 Append。
        /// </summary>
        /// <param name="position">绝对位置（0..Committed）。</param>
        /// <param name="segment">要插入的新段，不能为空。</param>
        public void InsertAtPosition(long position, SequenceBufferSegment<T> segment)
        {
            CheckWriteFrozen();
            ArgumentNullException.ThrowIfNull(segment, nameof(segment));
            if (position < 0 || position > Committed)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (First == null)
            {
                // 空序列等价于 Append
                Append(segment);
                return;
            }

            if (position == 0)
            {
                Prepend(segment);
                return;
            }

            if (position == Committed)
            {
                Append(segment);
                return;
            }

            if (!TryGetSegmentByPosition(position, out SequenceBufferSegment<T> target, out var offset))
                throw new InvalidOperationException("无法定位到指定位置的段。");

            // 若 position 指向 target 段的首部（offset == 0），插在 target 之前
            if (offset == 0)
            {
                InsertBefore(target, segment);
            }
            else
            {
                // 否则插在 target 之后（留给消费/调用者决定如何拼接）
                InsertAfter(target, segment);
            }
        }

        /// <summary>
        /// 根据序列中的绝对位置定位到包含该位置的段并返回段内偏移（相对于段的起点）。
        /// </summary>
        /// <param name="position">绝对位置（从 0 开始）。</param>
        /// <param name="segment">找到的段（若返回 false 则为 default）。</param>
        /// <param name="offset">在段内的偏移（相对于段的 MemoryOwner 的起点）。</param>
        /// <returns>若找到则返回 true，否则返回 false。</returns>
        public bool TryGetSegmentByPosition(long position, out SequenceBufferSegment<T> segment, out int offset)
        {
            segment = default!;
            offset = 0;

            if (position < 0 || position >= Committed)
                return false;

            var current = First;
            while (current != null)
            {
                long segStart = current.RunningIndex;
                long segLen = current.Committed;
                if (position >= segStart && position < segStart + segLen)
                {
                    segment = current;
                    offset = (int)(position - segStart);
                    return true;
                }

                current = current.Next;
            }

            return false;
        }

        protected override void UpdateCommittedProtected(ReadOnlySpan<T> span, long committedPosition)
        {
            // 将指定的已写入数据 span 按绝对位置 committedPosition 写回到对应段的已提交区域。 先确保段的 RunningIndex 是最新的，然后定位到起始段并逐段复制。
            if (span.IsEmpty)
                return;

            if (First == null)
                throw new InvalidOperationException("序列为空，无法更新提交内容。");

            // 保证 RunningIndex 与段位置同步
            First.UpdateRunningIndex();

            // 先定位起始段与偏移
            if (!TryGetSegmentByPosition(committedPosition, out var segment, out var segOffset))
                throw new InvalidOperationException("无法定位到提交位置所在的段。");

            int remaining = span.Length;
            int srcIndex = 0;

            // 逐段复制数据到各段的已提交内存
            while (remaining > 0 && segment != null)
            {
                // 当前段内从 segOffset 到已提交末尾可写入的长度
                int segCommitted = (int)segment.Committed;
                int can = segCommitted - segOffset;
                if (can > remaining) can = remaining;
                if (can > 0)
                {
                    // 复制到段的已提交内存
                    span.Slice(srcIndex, can).CopyTo(segment.Memory.Span.Slice(segOffset, can));
                    srcIndex += can;
                    remaining -= can;
                }

                // 向下一段继续，重置偏移
                segOffset = 0;
                segment = segment.Next;
            }

            if (remaining != 0)
            {
                // 理论上不会发生，因为上层已验证 committedPosition + span.Capacity <= Committed
                throw new InvalidOperationException("更新已提交内容时未能完成全部复制。");
            }
        }

        /// <summary>
        /// 从首段开始更新所有段的 RunningIndex，以确保它们与当前链表结构和已提交长度保持同步。 该方法在段链发生结构性变更（如插入/删除段）后应被调用，以维护 RunningIndex 的正确性。
        /// </summary>
        public void UpdateRunningIndex()
        {
            First?.UpdateRunningIndex();
        }

        #endregion Segment Operations

        /// <summary>
        /// 根据当前提交长度和自动增长策略考虑是否增加 minimumSpanCommitted 的值，以指导未来的段创建。
        /// </summary>
        private void ConsiderMinimumSizeIncrease()
        {
            if (AutoIncreaseMinimumSpanCommitted && minimumSpanCommitted < MaximumSequenceSegmentSize)
            {
                int autoSize = System.Math.Min(MaximumSequenceSegmentSize, (int)System.Math.Min(int.MaxValue, Committed / 2));
                if (minimumSpanCommitted < autoSize)
                {
                    minimumSpanCommitted = autoSize;
                }
            }
        }

        #region Release

        ///<inheritdoc/>
        protected override void ReleaseProtected()
        {
            First?.Release();
            First = null;
            Last = null;
            minimumSpanCommitted = 0;

            if (ownerProvider == null)
                throw new InvalidOperationException("无法释放序列：未绑定提供者。");

            ownerProvider.Release(this);
            ownerProvider = default!;
        }

        ///<inheritdoc/>
        protected override bool TryReleaseProtected()
        {
            First?.Release();
            First = null;
            Last = null;
            minimumSpanCommitted = 0;

            if (ownerProvider == null)
                return false;

            ownerProvider.Release(this);
            ownerProvider = default;
            return true;
        }

        /// <summary>
        /// 由提供者在分配后调用以初始化序列的生命周期（绑定提供者并重置段链）。
        /// </summary>
        /// <param name="provider">分配此序列的提供者实例。</param>
        internal void Initialize(SequenceBufferProvider<T> provider)
        {
            ownerProvider = provider;
            First = null;
            Last = null;
            minimumSpanCommitted = 0;
            IsActive = true;
        }

        #endregion Release

        #region Write

        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<T> source)
        {
            if (Available >= source.Length || Available == 0)
            {
                base.Write(source);
                return;
            }

            int written = Available;
            base.Write(source.Slice(0, written));
            base.Write(source.Slice(written));
        }

        #endregion Write

        #region Pinning

        ///<inheritdoc/>
        protected override MemoryHandle PinProtected(int elementIndex)
        {
            if (First == null)
                throw new InvalidOperationException("序列为空，无法固定元素。");

            if (!TryGetSegmentByPosition(elementIndex, out var segment, out var offset))
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex), "指定的元素索引超出序列范围。");
            }

            while (segment != null)
            {
                var next = segment.Next;
                segment.Pin(0);
                segment = next;
            }
            return default;
        }

        ///<inheritdoc/>
        public override void Unpin()
        {
            base.Unpin();
            var segment = First;
            while (segment != null)
            {
                var next = segment.Next;
                segment.Unpin();
                segment = next;
            }
        }

        #endregion Pinning

        ///<inheritdoc/>
        public override string ToString()
            => $"SequenceBuffer (Committed: {Committed}, Capacity: {Capacity})";

        ///<inheritdoc/>
        public override void Clear()
        {
            First?.Release();
            First = null;
            Last = null;
            minimumSpanCommitted = 0;
        }

        ///<inheritdoc/>
        public override T[] ToArray() => ((ReadOnlySequence<T>)this).ToArray();

        ///<inheritdoc/>
        public override SequenceBuffer<T> Slice(long start = 0, long length = 0)
            => (SequenceBuffer<T>)base.Slice(start, length);

        ///<inheritdoc/>
        protected override SequenceBuffer<T> SliceProtected(long start, long length)
        {
            if (!TryGetSegmentByPosition(start, out var segment, out int offset))
                throw new ArgumentOutOfRangeException(nameof(start), "起始位置超出序列范围。");
            if (length < 0 || start + length > Committed)
                throw new ArgumentOutOfRangeException(nameof(length), "长度无效或超出序列范围。");

            // 创建一个新的 SequenceBuffer 作为切片结果，并将包含 start 的段及其后续段链接到新序列中，直到达到 length。
            var sliceBuffer = SequenceBuffer<T>.GetBuffer();

            int sliceSegmentLength = (int)System.Math.Min(segment.Committed - offset, length);
            SequenceBufferSegment<T>? first = segment.Slice(offset, sliceSegmentLength);
            sliceBuffer.Append(first);
            int remaining = (int)length;
            remaining -= sliceSegmentLength;

            while (remaining > 0 && segment != null)
            {
                sliceSegmentLength = (int)System.Math.Min(segment.Committed, length);
                var sliceSegment = segment.Slice(offset, sliceSegmentLength);
                sliceBuffer.Append(sliceSegment);
                segment = segment.Next;
                remaining -= sliceSegmentLength;
            }

            first.UpdateRunningIndex(); // 确保切片段的 RunningIndex 是正确的
            return sliceBuffer;
        }

        /// <summary>
        /// 深拷贝当前 <see cref="SequenceBuffer{T}"/> 实例及其所有段。
        /// </summary>
        /// <returns>拷贝后的 <see cref="SequenceBuffer{T}"/> 实例</returns>
        public override SequenceBuffer<T> Clone()
        {
            var cloneBuffer = SequenceBuffer<T>.GetBuffer();

            var current = First;
            while (current != null)
            {
                var cloneSegment = current.Clone();
                cloneBuffer.Append(cloneSegment);
                current = current.Next;
            }
            return cloneBuffer;
        }

        /// <summary>
        /// 将 <see cref="SequenceBuffer{T}"/> 隐式转换为 <see cref="ReadOnlySequence{T}"/>。
        /// </summary>
        /// <param name="sequence">源序列。</param>
        public static implicit operator ReadOnlySequence<T>(SequenceBuffer<T> sequence)
            => sequence.First is SequenceBufferSegment<T> first && sequence.Last is SequenceBufferSegment<T> last
                ? new ReadOnlySequence<T>(first, 0, last, (int)last.Committed)
                : EmptySequence;
    }
}