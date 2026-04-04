using System.Buffers;
using System.Collections;

namespace ExtenderApp.Buffer.SequenceBuffers
{
    /// <summary>
    /// 表示序列中单个缓冲段的抽象基类，封装段的已提交长度、可用空间以及与相邻段的链表关系。
    /// </summary>
    /// <remarks>继承自 <see cref="ReadOnlySequenceSegment{T}"/>，用于在基于段的序列实现中维护 RunningIndex、前驱/后继关系并暴露段内内存与计数信息。 派生类需要提供具体的内存访问、已提交长度、可写容量以及推进/重置的实现。</remarks>
    public sealed partial class SequenceBufferSegment<T> : ReadOnlySequenceSegment<T>, IPinnable, IEnumerable<SequenceBufferSegment<T>>
    {
        public static readonly SequenceBufferSegment<T> Empty = new SequenceBufferSegment<T>();

        internal SequenceBufferSegmentProvider<T>? SegmentProvider;

        private MemoryBlock<T> memoryBlock;

        /// <summary>
        /// 当前段在整个序列中的起始索引（相对于序列起点）。
        /// </summary>
        internal long SegmentStart { get; set; }

        /// <summary>
        /// 当前段在序列中的结束索引（不包含），等于 <see cref="SegmentStart"/> + <see cref="Committed"/>.
        /// </summary>
        internal long SegmentEnd => SegmentStart + Committed;

        /// <summary>
        /// 当前段所持有的可读/已提交内存片（派生类提供具体实现）。
        /// </summary>
        internal new Memory<T> Memory => memoryBlock.Memory;

        /// <summary>
        /// 已提交（已写入）内存片，长度等于 <see cref="Committed"/>。派生类提供具体实现以反映当前段中已写入的数据范围。
        /// </summary>
        internal ReadOnlyMemory<T> CommittedMemory => memoryBlock.CommittedMemory;

        /// <summary>
        /// 已提交（已写入）元素的跨度，长度等于 <see cref="Committed"/>。派生类提供具体实现以反映当前段中已写入的数据范围。调用方可以使用此属性获取当前段中已写入的数据作为只读跨度进行处理。
        /// </summary>
        internal ReadOnlySpan<T> CommittedSpan => memoryBlock.CommittedSpan;

        /// <summary>
        /// 已提交（已写入）元素的数组片段，长度等于 <see cref="Committed"/>。派生类提供具体实现以反映当前段中已写入的数据范围。调用方可以使用此属性获取当前段中已写入的数据作为数组片段进行处理（如与旧 API
        /// 兼容）。如果当前段的内存不是基于数组的，或者已提交范围不连续，则实现应抛出 InvalidOperationException 或返回一个空的 ArraySegment。
        /// </summary>
        internal ArraySegment<T> CommittedArraySegment => memoryBlock.CommittedSegment;

        /// <summary>
        /// 当前段中已提交（已写入）元素的数量。
        /// </summary>
        internal long Committed => memoryBlock.Committed;

        /// <summary>
        /// 当前段尚可写入的元素数量（剩余可用空间）。
        /// </summary>
        internal int Available { get; }

        /// <summary>
        /// 从当前段起至链表尾部的段数量（含当前段）。沿 <see cref="Next"/> 迭代计数，避免深链递归导致栈溢出。
        /// </summary>
        public int Count
        {
            get
            {
                int c = 0;
                for (SequenceBufferSegment<T>? n = this; n != null; n = n.Next)
                    c++;
                return c;
            }
        }

        /// <summary>
        /// 获取当前段在序列中的前一个段（如果存在），否则为 null。
        /// </summary>
        internal SequenceBufferSegment<T>? Prev { get; set; }

        /// <summary>
        /// 获取当前段在序列中的下一个段（如果存在），否则为 null。 派生类通过 <see cref="SetNext"/> 方法设置后续段以维护链表关系和运行索引更新。
        /// </summary>
        internal new SequenceBufferSegment<T>? Next
        {
            get => base.Next as SequenceBufferSegment<T>;
            set => base.Next = value;
        }

        public SequenceBufferSegment()
        {
            Prev = null;
            Next = null;
            SegmentStart = 0;
            RunningIndex = 0;
            memoryBlock = MemoryBlock<T>.Empty;
        }

        /// <summary>
        /// 由提供者在分配后调用以初始化段的生命周期（绑定提供者并重置链表/索引状态）。
        /// </summary>
        /// <param name="provider">分配此段的提供者实例。</param>
        internal void Initialize(SequenceBufferSegmentProvider<T> provider, MemoryBlock<T> memoryBlock)
        {
            SegmentProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.memoryBlock = memoryBlock ?? throw new ArgumentNullException(nameof(memoryBlock));
            base.Memory = memoryBlock.CommittedMemory;
            memoryBlock.Freeze();

            Prev = null;
            Next = null;
            SegmentStart = 0;
            RunningIndex = 0;
        }

        /// <summary>
        /// 将指定段设置为当前段的下一个段，并自动更新后续段的 RunningIndex 以保持索引连续性。调用方应确保传入的段实例正确配置并且不与当前链表中的其他段冲突（如重复使用同一实例）。如果调用方已经在其他上下文中更新了 RunningIndex，可以将 <paramref
        /// name="needUpdateIndex"/> 设置为 false 以避免重复计算。
        /// </summary>
        /// <param name="segment">要设置为下一个段的实例（可以为 null 表示尾部）。</param>
        /// <param name="needUpdateIndex">指示是否需要更新后续段的 RunningIndex（默认为 true）。如果调用方已经在其他上下文中更新了 RunningIndex，可以将此参数设置为 false 以避免重复计算。</param>
        internal void SetNext(SequenceBufferSegment<T>? segment, bool needUpdateIndex = true)
        {
            Next = segment;
            if (segment != null)
            {
                segment.Prev = this;
                if (needUpdateIndex) segment.UpdateRunningIndex();
            }
        }

        /// <summary>
        /// 获取一个至少包含 <paramref name="sizeHint"/> 个元素的可写 <see cref="Memory{T}"/>。
        /// </summary>
        /// <param name="sizeHint">建议的最小可用大小（可为 0，表示不作特殊建议）。</param>
        /// <returns>用于写入的 <see cref="Memory{T}"/>。</returns>
        internal Memory<T> GetMemory(int sizeHint = 0) => memoryBlock.GetMemory(sizeHint);

        /// <summary>
        /// 获取一个至少包含 <paramref name="sizeHint"/> 个元素的可写 <see cref="Span{T}"/>。
        /// </summary>
        /// <param name="sizeHint">建议的最小可用大小（可为 0，表示不作特殊建议）。</param>
        /// <returns>用于写入的 <see cref="Span{T}"/>。</returns>
        internal Span<T> GetSpan(int sizeHint = 0) => memoryBlock.GetSpan(sizeHint);

        /// <summary>
        /// 重新计算并更新自身及后续所有段的 <see cref="RunningIndex"/> 与已提交内存视图。沿 <see cref="Next"/> 迭代，避免深链递归导致栈溢出。
        /// </summary>
        internal void UpdateRunningIndex()
        {
            for (SequenceBufferSegment<T>? node = this; node != null; node = node.Next)
            {
                node.RunningIndex = node.Prev != null ? node.Prev.RunningIndex + node.Prev.Committed : 0;
                node.SetCommittedMemoryAsSequenceMemory();
            }
        }

        /// <summary>
        /// 将基类序列视图同步为当前段已提交内存（供 <see cref="UpdateRunningIndex"/> 迭代路径调用）。
        /// </summary>
        private void SetCommittedMemoryAsSequenceMemory() => base.Memory = CommittedMemory;

        /// <summary>
        /// 将当前段的已提交长度向前推进指定数量。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负值）。</param>
        internal void Advance(int count)
        {
            memoryBlock.Advance(count);
            base.Memory = CommittedMemory;
        }

        /// <summary>
        /// 将当前段及其后继段链重置为初始状态并归还资源。沿 <see cref="Next"/> 迭代释放，避免极长段链下递归导致的栈溢出。
        /// </summary>
        public void Release()
        {
            SequenceBufferSegment<T>? node = this;
            while (node != null)
            {
                SequenceBufferSegment<T>? next = node.Next;

                if (node.Prev != null && node.Prev.Next == node)
                    node.Prev.Next = null;
                node.Prev = null;
                node.Next = null;

                node.ResetSegmentForPool();
                node = next;
            }
        }

        /// <summary>
        /// 在已从链上摘离后，重置运行索引、序列视图与底层块并归还段至提供者。
        /// </summary>
        private void ResetSegmentForPool()
        {
            RunningIndex = 0;
            base.Memory = ReadOnlyMemory<T>.Empty;
            SegmentProvider?.ReleaseSegment(this);
            SegmentProvider = null;
            memoryBlock.TryRelease();
            memoryBlock = null!;
        }

        /// <summary>
        /// 从指定元素索引处固定当前段的内存，返回一个 <see cref="MemoryHandle"/> 用于访问固定的内存位置。派生类应实现具体的固定逻辑（如调用 GCHandle.Alloc 或其他机制），并确保在不再需要访问时调用 <see cref="Unpin"/> 释放固定资源。
        /// </summary>
        /// <param name="elementIndex">指定索引</param>
        /// <returns><see cref="MemoryHandle"/> 实例</returns>
        public MemoryHandle Pin(int elementIndex) => memoryBlock.Pin(elementIndex);

        /// <summary>
        /// 释放之前通过 <see cref="Pin(int)"/> 固定的内存资源。派生类应实现具体的释放逻辑（如调用 GCHandle.Free 或其他机制），以确保固定的内存能够被垃圾回收器正确管理。
        /// </summary>
        public void Unpin() => memoryBlock.Unpin();

        /// <summary>
        /// 从当前段的指定起始位置和长度创建一个新的段实例，表示当前段内的一个子范围。派生类应实现具体的切片逻辑，确保返回的新段正确反映指定范围内的数据，并且与当前段共享相同的底层内存（如果适用）。调用方应确保传入的 <paramref name="start"/> 和
        /// <paramref name="length"/> 参数在当前段的有效范围内。返回的新段实例应具有独立的前后段关系，以便在序列中正确链接和管理。
        /// </summary>
        /// <param name="start">切片的起始位置（相对于当前段的起点）。</param>
        /// <param name="length">切片的长度（必须为非负值）。</param>
        /// <returns>返回 <see cref="SequenceBufferSegment{T}"/> 实例</returns>
        public SequenceBufferSegment<T> Slice(int start, int length)
            => SegmentProvider?.GetSegment(memoryBlock.Slice(start, length)) ?? Empty;

        /// <summary>
        /// 将当前段克隆为一个新的段实例，通常用于在需要创建当前段的副本（如分割、复制等场景）时调用。派生类应实现具体的克隆逻辑，确保返回的新段实例具有与当前段相同的数据内容和状态，但在链表关系上是独立的。调用方应注意，克隆后的段实例可能需要重新配置前后段关系以正确链接到序列中。
        /// </summary>
        /// <returns>返回新 <see cref="SequenceBufferSegment{T}"/> 实例</returns>
        public SequenceBufferSegment<T> Clone()
            => SegmentProvider?.GetSegment(memoryBlock.Clone()) ?? Empty;

        #region Enumerable 实现 - 支持 foreach 遍历段链表

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<SequenceBufferSegment<T>>)this).GetEnumerator();
        }

        IEnumerator<SequenceBufferSegment<T>> IEnumerable<SequenceBufferSegment<T>>.GetEnumerator()
        {
            return new SegmentEnumerator(this);
        }

        /// <summary>
        /// 返回用于遍历段链表的结构体枚举器。
        /// </summary>
        /// <returns>段枚举器实例。</returns>
        public SegmentEnumerator GetEnumerator()
        {
            return new SegmentEnumerator(this);
        }

        /// <summary>
        /// 段链表的结构体枚举器。
        /// </summary>
        public struct SegmentEnumerator : IEnumerator<SequenceBufferSegment<T>>
        {
            private readonly SequenceBufferSegment<T>? head;
            private SequenceBufferSegment<T>? current;
            private bool started;

            internal SegmentEnumerator(SequenceBufferSegment<T> head)
            {
                this.head = head;
                current = null;
                started = false;
            }

            /// <inheritdoc/>
            public SequenceBufferSegment<T> Current => current!;

            /// <inheritdoc/>
            object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (!started)
                {
                    current = head;
                    started = true;
                    return current is not null;
                }

                if (current?.Next is { } next)
                {
                    current = next;
                    return true;
                }

                current = null;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }

        #endregion Enumerable 实现 - 支持 foreach 遍历段链表
    }
}