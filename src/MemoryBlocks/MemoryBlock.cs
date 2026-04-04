using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 内存块的抽象基类，表示一块可写入的内存区域，支持动态扩展和已写入数据的管理。
    /// </summary>
    /// <typeparam name="T">内存块中元素的类型。</typeparam>
    public abstract partial class MemoryBlock<T> : AbstractBuffer<T>, IEquatable<MemoryBlock<T>>
    {
        // 使用 RuntimeHelpers.IsReferenceOrContainsReferences<T>() 在运行时判定是否需要清理引用。
        private static readonly bool MayContainReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        /// <summary>
        /// 获取为空块的单例实例（表示不包含任何数据的占位块）。 派生类型需提供一个空实现： <see cref="EmptyMemoryBlock{T}"/>.
        /// </summary>
        public static readonly MemoryBlock<T> Empty = new EmptyMemoryBlock<T>();

        private int committed;

        /// <summary>
        /// 获取当前块内已写入的内存部分（相对于底层可用内存的切片）。
        /// </summary>
        protected internal Memory<T> Memory => AvailableMemory.Slice(0, committed);

        /// <summary>
        /// 获取当前块内已写入的跨度部分（同步访问，非线程安全）。
        /// </summary>
        protected internal Span<T> Span => Memory.Span;

        /// <summary>
        /// 当前块所属的内存块提供者（如果有）。 派生类在分配或扩展底层存储时应将此属性设置为提供者实例，以便在释放时正确归还资源。 该属性不应由外部调用方直接使用；它是供派生类和提供者内部管理资源生命周期的机制。
        /// </summary>
        private MemoryBlockProvider<T>? ownerProvider;

        /// <summary>
        /// 获取当前块已使用的元素数量。
        /// </summary>
        public override sealed long Committed => committed;

        /// <summary>
        /// 获取当前块剩余可写入的元素数量，等于 <see cref="AvailableMemory"/> 的长度减去 <see cref="end"/>。
        /// </summary>
        public override sealed int Available => AvailableMemory.Length - committed;

        /// <summary>
        /// 当前内存块的总容量，等于 <see cref="AvailableMemory"/> 的长度。
        /// </summary>
        public override sealed long Capacity => AvailableMemory.Length;

        ///<inheritdoc/>
        public override sealed ReadOnlySequence<T> CommittedSequence => this;

        /// <summary>
        /// 派生类应提供的底层可用内存（包含已写入与未写入部分）。 该属性不应分配新的底层存储；派生类负责提供稳定的 MemoryOwner 实现（例如来自池或 MemoryOwner）。
        /// </summary>
        protected abstract Memory<T> AvailableMemory { get; }

        /// <summary>
        /// 获取剩余的内存部分（可写区域），供调用方写入数据。
        /// </summary>
        public Memory<T> RemainingMemory => AvailableMemory.Slice(committed);

        /// <summary>
        /// 获取剩余的跨度部分（可写区域），供调用方写入数据。
        /// </summary>
        public Span<T> RemainingSpan => RemainingMemory.Span;

        /// <summary>
        /// 获取当前块内已写入的只读内存部分。
        /// </summary>
        public ReadOnlyMemory<T> CommittedMemory => Memory;

        /// <summary>
        /// 获取当前块内已写入的只读跨度部分。
        /// </summary>
        public ReadOnlySpan<T> CommittedSpan => Span;

        /// <summary>
        /// 获取当前块内未读的数组块（如果底层 MemoryOwner 是基于数组的）。 该属性仅在底层 MemoryOwner 实现为数组时有效，否则返回一个空的 ArraySegment。 注意：返回的 ArraySegment 的偏移和长度是相对于底层数组的，且包含已写入部分（从
        /// start 到 end）。 调用方应根据需要调整偏移和长度以访问特定范围。 派生类不应重写此属性；它基于 <see cref="AvailableMemory"/> 的实现自动提供。
        /// </summary>
        public ArraySegment<T> CommittedSegment
        {
            get
            {
                if (MemoryMarshal.TryGetArray(Memory, out ArraySegment<T> segment))
                {
                    return segment;
                }
                else
                {
                    return new ArraySegment<T>(Array.Empty<T>(), 0, 0);
                }
            }
        }

        /// <summary>
        /// 实现 IBufferWriter{T}：返回至少包含 <paramref name="sizeHint"/> 个可写元素的 MemoryOwner。 派生类通过重写 <see cref="EnsureCapacity(int)"/> 来确保容量。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写元素数（可选），当为 0 时仅保证至少 1 个可写元素（视实现而定）。</param>
        /// <returns>用于写入的可写内存片块（从当前写入位置到底层可用内存末尾）。</returns>
        public override sealed Memory<T> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return RemainingMemory;
        }

        /// <summary>
        /// 实现 IBufferWriter{T}：返回至少包含 <paramref name="sizeHint"/> 个可写元素的 Span。 派生类通过重写 <see cref="EnsureCapacity(int)"/> 来确保容量。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写元素数（可选）。</param>
        /// <returns>用于写入的可写跨度（从当前写入位置到底层可用内存末尾）。</returns>
        public override sealed Span<T> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return RemainingSpan;
        }

        ///<inheritdoc/>
        protected override sealed MemoryHandle PinProtected(int elementIndex)
        {
            return AvailableMemory.Slice(elementIndex).Pin();
        }

        /// <summary>
        /// 将结束索引向前移动指定数量，表示向块中写入了更多元素。 实现了 IBufferWriter{T}.Advance 的语义检查。
        /// </summary>
        /// <param name="count">要前移的元素数量（非负）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 为负或移动后 <see cref="end"/> 超过底层内存长度时抛出。</exception>
        public override sealed void Advance(int count)
        {
            CheckWriteFrozen();
            if (count < 0 || committed + count > AvailableMemory.Length)
                throw new ArgumentOutOfRangeException(nameof(count), "count 必须是非负数，且移动后的结束索引不能超过内存长度。");

            committed += count;
        }

        /// <summary>
        /// 将结束索引向后回退指定数量（相当于从块尾移除数据）。 回退时会在被移除的范围内清除引用（如必要）。
        /// </summary>
        /// <param name="count">要回退的元素数量（非负）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 为负或回退后 <see cref="end"/> 小于 <see cref="start"/> 时抛出。</exception>
        public void Rewind(int count)
        {
            CheckWriteFrozen();
            if (count < 0 || committed - count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "count 必须是非负数，且移动后的结束索引不能小于 0。");

            committed -= count;
            ClearReferences(committed, count);
        }

        /// <summary>
        /// 在指定范围内清除引用类型的元素（设置为默认值），以便被回收并防止数据泄露。 仅当元素类型为引用或包含引用字块时才执行（由 <see cref="MayContainReferences"/> 判定）。
        /// </summary>
        /// <param name="startIndex">范围起始索引（相对于块的可用内存）。</param>
        /// <param name="length">要清除的元素数量。</param>
        protected void ClearReferences(int startIndex, int length)
        {
            if (MayContainReferences)
            {
                AvailableMemory.Span.Slice(startIndex, length).Clear();
            }
        }

        /// <summary>
        /// 尝试将当前序列块已写入的数据复制到另一块序列块中。 仅在目标块有足够可写空间时才会复制全部已写入数据。
        /// </summary>
        /// <param name="segment">目标序列块。</param>
        /// <returns>如果复制成功（全部已写入数据被复制）则返回 true，否则返回 false。</returns>
        /// <remarks>复制使用 <see cref="Memory.CopyTo(Memory{T})"/> 语义：从本块已写入部分到目标块的可写部分的直接复制；成功时会在目标块上调用 <see cref="Advance(int)"/>，使已提交长度增加与本块相同的元素数。</remarks>
        public bool TryCopyTo(MemoryBlock<T> segment)
        {
            if (segment == null || segment.Available < committed)
                return false;

            Memory.CopyTo(segment.RemainingMemory);
            segment.Advance((int)committed);
            return true;
        }

        /// <summary>
        /// 在已提交范围内按需清除引用类型元素（由 <see cref="MayContainReferences"/> 判定），以便 GC 回收；不会减小 <see cref="Committed"/> 或擦除纯值类型字节内容。
        /// </summary>
        public override sealed void Clear()
        {
            CheckWriteFrozen();
            ClearReferences(0, committed);
        }

        /// <summary>
        /// 反转已写入区域中从 <paramref name="start"/> 开始的指定长度块内的元素顺序。
        /// </summary>
        /// <param name="start">相对于已写入区域起始的起始索引。</param>
        /// <param name="length">要反转的长度。</param>
        /// <exception cref="ArgumentOutOfRangeException">当参数不定义一个有效的已写入范围时抛出。</exception>
        public void Reverse(int start, int length)
        {
            CheckWriteFrozen();
            if (start < 0 || length < 0 || start + length > committed)
                throw new ArgumentOutOfRangeException("start 和 length 必须定义一个有效的已写入范围。");
            Span.Slice(start, length).Reverse();
        }

        /// <summary>
        /// 对整个已写入区域执行反转操作（将已写入元素顺序反转）。
        /// </summary>
        public void Reverse()
        {
            CheckWriteFrozen();
            Span.Reverse();
        }

        ///<inheritdoc/>
        protected override sealed void ReleaseProtected()
        {
            ClearReferences(0, committed);
            committed = 0;
            if (ownerProvider == null)
                throw new InvalidOperationException("无法释放内存块：未绑定提供者。请确保块已正确初始化并由提供者分配。");

            ownerProvider.Release(this);
            ownerProvider = null;
        }

        ///<inheritdoc/>
        protected override sealed bool TryReleaseProtected()
        {
            ClearReferences(0, committed);
            committed = 0;
            if (ownerProvider == null)
                return false;

            ownerProvider.Release(this);
            ownerProvider = null;
            return true;
        }

        /// <summary>
        /// 由对应的提供者在分配后调用以初始化块的生命周期信息（绑定提供者并重置已提交计数）。
        /// </summary>
        /// <param name="provider">分配此块的提供者实例。</param>
        protected internal virtual void Initialize(MemoryBlockProvider<T> provider)
        {
            ownerProvider = provider;
            committed = 0;
            IsActive = true;
        }

        ///<inheritdoc/>
        protected override sealed void UpdateCommittedProtected(ReadOnlySpan<T> span, long committedPosition)
        {
            span.CopyTo(Span.Slice((int)committedPosition));
        }

        /// <summary>
        /// 确保块有足够的可写空间以容纳指定数量的元素。 派生类应在此方法内扩展或分配底层存储（若必要），并保证调用者随后从 <see cref="RemainingMemory"/> / <see cref="RemainingSpan"/> 获取到的容量至少满足 <paramref name="sizeHint"/>。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写空间大小（当为 0 时实现可选择保持当前容量或最低策略）。</param>
        protected void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint), "sizeHint 必须是非负数。");
            if (sizeHint == 0 || Available >= sizeHint)
                return;

            EnsureCapacityProtected(sizeHint);
        }

        /// <summary>
        /// 确保块有足够的可写空间以容纳指定数量的元素的受保护方法。 派生类应在此方法内扩展或分配底层存储（若必要），并保证调用者随后从 <see cref="RemainingMemory"/> / <see cref="RemainingSpan"/> 获取到的容量至少满足
        /// <paramref name="sizeHint"/>。
        /// </summary>
        /// <param name="sizeHint">所需的最小可写空间大小（当为 0 时实现可选择保持当前容量或最低策略）。</param>
        protected abstract void EnsureCapacityProtected(int sizeHint);

        ///<inheritdoc/>
        public override T[] ToArray() => CommittedMemory.ToArray();

        ///<inheritdoc/>
        public override sealed MemoryBlock<T> Slice(long start = 0, long length = 0)
            => (MemoryBlock<T>)base.Slice(start, length);

        ///<inheritdoc/>
        protected override sealed AbstractBuffer<T> SliceProtected(long start, long length)
            => ArrayPoolBlockProvider<T>.Default.GetBuffer(CommittedSpan.Slice((int)start, (int)length));

        /// <summary>
        /// 从当前块内已写入的元素范围创建一个新的 <see cref="MemoryBlock{T}"/> 实例，表示该范围的独立副本。 派生类应通过克隆底层存储来实现深复制，以确保新实例与原实例之间没有共享状态。
        /// </summary>
        /// <returns>返回 <see cref="MemoryBlock{T}"/> 实例</returns>
        /// <exception cref="InvalidOperationException">当无法创建新块时生成</exception>
        public override sealed MemoryBlock<T> Clone()
        {
            var clone = ownerProvider?.GetBuffer(Memory.Length);
            if (clone == null)
                throw new InvalidOperationException("无法克隆内存块：未绑定提供者。请确保块已正确初始化并由提供者分配。");

            clone.Write(CommittedSpan);
            return clone;
        }

        public bool Equals(MemoryBlock<T>? other) => other?.CommittedSpan.SequenceEqual(CommittedSpan) ?? false;

        public override sealed bool Equals(object? obj) => obj is MemoryBlock<T> other && Equals(other);

        public override sealed int GetHashCode() => HashCode.Combine(CommittedMemory);

        public override sealed string ToString()
        {
            return $"MemoryBlock<{typeof(T).Name}>: Committed={Committed}, Available={Available}, Capacity={Capacity}";
        }

        public static bool operator ==(MemoryBlock<T>? left, MemoryBlock<T>? right) => Equals(left, right);

        public static bool operator !=(MemoryBlock<T>? left, MemoryBlock<T>? right) => !Equals(left, right);

        public static implicit operator ReadOnlySequence<T>(MemoryBlock<T> block)
            => new ReadOnlySequence<T>(block);

        public static implicit operator ReadOnlyMemory<T>(MemoryBlock<T> block)
            => block.CommittedMemory;

        public static implicit operator ReadOnlySpan<T>(MemoryBlock<T> block)
            => block.CommittedSpan;

        public static implicit operator ArraySegment<T>(MemoryBlock<T> block)
            => block.CommittedSegment;
    }
}