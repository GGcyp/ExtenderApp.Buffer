using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// 池化序列类，实现了 <see cref="IBufferWriter{T}"/> 接口，允许通过对象池高效地管理内存。 该类使用内部的段对象池来管理序列段，并使用数组池来租用和回收底层数组。 通过实现 <see cref="IDisposable"/> 接口，确保在使用完成后可以正确释放资源并将实例返回到对象池以供重用。
    /// </summary>
    /// <typeparam name="T">序列中元素的类型。</typeparam>
    public sealed class FastSequence<T> : IBufferWriter<T>, IEnumerable<ArraySegment<T>>
    {
        /// <summary>
        /// 获取一个池化的 <see cref="FastSequence{T}"/> 实例。 该方法从对象池中获取一个实例，确保高效的内存管理和重用。 使用完成后，请确保调用 <see cref="TryRelease"/> 方法以释放资源并将实例返回到对象池。
        /// </summary>
        /// <returns>池化的 <see cref="FastSequence{T}"/> 实例。</returns>
        public static FastSequence<T> GetBuffer() => FastSequenceProvider<T>.Shared.GetBuffer();

        /// <summary>
        /// 从数组池中租用的默认长度。
        /// </summary>
        private static readonly int DefaultLengthFromArrayPool = 1 + (4095 / Unsafe.SizeOf<T>());

        /// <summary>
        /// 空的只读序列。
        /// </summary>
        private static readonly ReadOnlySequence<T> Empty = new ReadOnlySequence<T>(FastSequenceSegment.Empty, 0, FastSequenceSegment.Empty, 0);

        /// <summary>
        /// 序列段对象池。
        /// </summary>
        private static readonly ObjectPool<FastSequenceSegment> SegmentPool = ObjectPool.Create<FastSequenceSegment>();

        /// <summary>
        /// 数组池。
        /// </summary>
        private readonly ArrayPool<T> _arrayPool;

        /// <summary>
        /// 标记当前序列是否已转换为 <c>SequenceBufferSegment</c>。 当为 <c>true</c> 时，回收段时将不会把底层数组返回到数组池，因为数组的生命周期由外部的 <c>SequenceBufferSegment</c> 管理。
        /// </summary>
        private bool canReturnArray;

        /// <summary>
        /// 当前序列段所属提供者
        /// </summary>
        private FastSequenceProvider<T> ownerProvider;

        /// <summary>
        /// 序列中的第一个段。
        /// </summary>
        internal FastSequenceSegment? First { get; private set; }

        /// <summary>
        /// 序列中的最后一个段。
        /// </summary>
        private FastSequenceSegment? last;

        /// <summary>
        /// 获取或设置序列的最小跨度长度。
        /// </summary>
        public int MinimumSpanLength { get; set; }

        /// <summary>
        /// 获取或设置是否自动增加最小跨度长度。
        /// </summary>
        public bool AutoIncreaseMinimumSpanLength { get; set; }

        /// <summary>
        /// 返回当前序列是否仅包含一个段。 当 <see cref="First"/> 和 <see cref="last"/> 引用同一个段时，返回 <c>true</c>；否则返回 <c>false</c>。
        /// </summary>
        public bool IsSingleSegment => First != null && First == last;

        /// <summary>
        /// 返回当前序列段是否为空。 当 <see cref="First"/> 为 null 时，返回 <c>true</c>；否则返回 <c>false</c>。
        /// </summary>
        public bool IsEmpty => First == null;

        /// <summary>
        /// 获取此序列的只读版本。
        /// </summary>
        public ReadOnlySequence<T> AsReadOnlySequence => this;

        /// <summary>
        /// 获取序列的长度。
        /// </summary>
        public long Committed { get; private set; }

        /// <summary>
        /// 使用默认的内存池初始化Sequence类的新实例。
        /// </summary>
        public FastSequence() : this(ArrayPool<T>.Shared)
        {
        }

        /// <summary>
        /// 使用指定的数组池初始化Sequence类的新实例。
        /// </summary>
        /// <param name="arrayPool">数组池。</param>
        public FastSequence(ArrayPool<T> arrayPool)

        {
            if (arrayPool == null)
                throw new ArgumentNullException(nameof(arrayPool));

            _arrayPool = arrayPool;
            MinimumSpanLength = 0;
            AutoIncreaseMinimumSpanLength = true;
            ownerProvider = default!;
            canReturnArray = true;
        }

        /// <summary>
        /// 初始化池化序列
        /// </summary>
        /// <param name="provider"></param>
        internal void Initialize(FastSequenceProvider<T> provider)
        {
            ownerProvider = provider;
        }

        /// <summary>
        /// 推进序列的位置。
        /// </summary>
        /// <param name="count">推进的数量。</param>
        public void Advance(int count)
        {
            if (last is null)
                throw new InvalidOperationException("在获取内存之前不能进行推进操作");

            last!.Advance(count);
            Committed += count;
            ConsiderMinimumSizeIncrease();
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void TryRelease()
        {
            Reset();
            ownerProvider.Release(this);
            ownerProvider = default!;
        }

        /// <summary>
        /// 重置序列。
        /// </summary>
        public void Reset()
        {
            var current = First;
            while (current != null)
            {
                current = RecycleAndGetNext(current);
            }

            First = null;
            last = null;
            Committed = 0;
            // 重置转换标志，确保序列可被复用为普通 PooledSequence
            canReturnArray = true;
        }

        /// <summary>
        /// 获取指定大小的内存。
        /// </summary>
        /// <param name="sizeHint">所需内存的大小提示。</param>
        /// <returns>指定大小的内存。</returns>
        public Memory<T> GetMemory(int sizeHint = 0) => GetSegment(sizeHint).RemainingMemory;

        /// <summary>
        /// 获取指定大小的跨度。
        /// </summary>
        /// <param name="sizeHint">所需跨度的大小提示。</param>
        /// <returns>指定大小的跨度。</returns>
        public Span<T> GetSpan(int sizeHint = 0) => GetSegment(sizeHint).RemainingSpan;

        /// <summary>
        /// 获取一个段，该段具有指定大小的可用内存。
        /// </summary>
        /// <param name="sizeHint">所需内存的大小提示。</param>
        /// <returns>具有指定大小可用内存的段。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FastSequenceSegment GetSegment(int sizeHint)
        {
            int minBufferSize = -1;
            bool needNewSegment = false;
            if (sizeHint == 0)
            {
                if (last == null || last.WritableBytes == 0)
                {
                    minBufferSize = -1;
                    needNewSegment = true;
                }
            }
            else
            {
                if (last == null || last.WritableBytes < sizeHint)
                {
                    minBufferSize = System.Math.Max(MinimumSpanLength, sizeHint);
                    needNewSegment = true;
                }
            }

            if (needNewSegment)
            {
                FastSequenceSegment segment = SegmentPool.Get();
                segment.Assign(_arrayPool.Rent(minBufferSize == -1 ? DefaultLengthFromArrayPool : minBufferSize));
                Append(segment);
            }

            return last!;
        }

        /// <summary>
        /// 将段附加到序列中。
        /// </summary>
        /// <param name="segment">要附加的段。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Append(FastSequenceSegment segment)
        {
            if (last == null)
            {
                First = segment;
                last = segment;
                return;
            }

            if (last.Length > 0)
            {
                last.SetNext(segment);
                last = segment;
                return;
            }

            var current = First;
            if (First != last)
            {
                while (current!.Next != last)
                {
                    current = current.Next;
                }
            }
            else
            {
                First = segment;
            }

            current!.SetNext(segment);
            RecycleAndGetNext(last);
            last = segment;
        }

        /// <summary>
        /// 回收段并将其从序列中移除。
        /// </summary>
        /// <param name="segment">要回收的段。</param>
        /// <returns>回收后的下一个段。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FastSequenceSegment? RecycleAndGetNext(FastSequenceSegment segment)
        {
            var recycledSegment = segment;
            var nextSegment = segment.Next;
            // 如果序列已经被转换为 SequenceBufferSegment，则不要将数组返回到数组池。
            recycledSegment.ResetMemory(_arrayPool, canReturnArray);
            SegmentPool.Release(recycledSegment);
            return nextSegment;
        }

        /// <summary>
        /// 考虑是否增加最小跨度长度。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConsiderMinimumSizeIncrease()
        {
            if (AutoIncreaseMinimumSpanLength && MinimumSpanLength < AbstractBuffer<T>.MaximumSequenceSegmentSize)
            {
                int autoSize = System.Math.Min(AbstractBuffer<T>.MaximumSequenceSegmentSize, (int)System.Math.Min(int.MaxValue, Committed / 2));
                if (MinimumSpanLength < autoSize)
                {
                    MinimumSpanLength = autoSize;
                }
            }
        }

        /// <summary>
        /// 固定当前序列的底层数组，防止在回收段时将数组返回到数组池。 该方法通常在将当前序列转换为外部管理的段（例如 <c>SequenceBufferSegment</c>）时调用，以确保数组的生命周期由外部管理，而不是由当前序列管理。 调用此方法后，当前序列将不再负责管理底层数组的生命周期，因此在回收段时不会将数组返回到数组池。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PinSegmentArray() => canReturnArray = false;

        /// <summary>
        /// 解除固定当前序列的底层数组，允许在回收段时将数组返回到数组池。 该方法通常在当前序列不再被外部管理的段（例如 <c>SequenceBufferSegment</c>）使用时调用，以恢复当前序列对底层数组生命周期的管理。 调用此方法后，当前序列将负责管理底层数组的生命周期，因此在回收段时会将数组返回到数组池。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnpinSegmentArray() => canReturnArray = true;

        /// <summary>
        /// 返回一个结构化的枚举器以遍历内部段的底层数组，避免在使用具体类型时产生迭代器分配。
        /// </summary>
        public Enumerator GetEnumerator() => new(First);

        IEnumerator<ArraySegment<T>> IEnumerable<ArraySegment<T>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ArraySegment<T>>)this).GetEnumerator();

        public static implicit operator ReadOnlySequence<T>(FastSequence<T> sequence)
            => sequence.First is FastSequenceSegment first && sequence.last is FastSequenceSegment last
                ? new ReadOnlySequence<T>(first, first.Start, last, last.End)
                : Empty;

        public static implicit operator AbstractBuffer<T>(FastSequence<T> sequence)
            => sequence.ToBuffer();

        /// <summary>
        /// 结构化枚举器，用于无分配地遍历 <see cref="FastSequence{T}"/> 的底层数组段。 当通过接口 <see cref="IEnumerable{T}"/> 调用时会发生装箱；直接对 <see cref="FastSequence{T}"/> 调用
        /// foreach 可避免装箱。
        /// </summary>
        public struct Enumerator : IEnumerator<ArraySegment<T>>
        {
            private FastSequenceSegment? _start;
            private FastSequenceSegment? _current;

            public Enumerator(FastSequenceSegment? start)
            {
                _start = start;
                _current = null;
            }

            public ArraySegment<T> Current => _current!.CommittedSegment;

            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                if (_current == null)
                    _current = _start;
                else
                    _current = _current.Next;

                while (_current != null)
                {
                    if (_current.TargetArray != null)
                        return true;

                    _current = _current.Next;
                }

                return false;
            }

            public void Reset()
            {
                _current = null;
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// 表示一个序列段，用于处理序列中的一段数据。
        /// </summary>
        /// <typeparam name="T">序列中元素的类型。</typeparam>
        public sealed class FastSequenceSegment : ReadOnlySequenceSegment<T>
        {
            /// <summary>
            /// 获取一个空的 <see cref="FastSequenceSegment"/> 实例。
            /// </summary>
            internal static readonly FastSequenceSegment Empty = new FastSequenceSegment();

            /// <summary>
            /// 获取一个值，指示序列中的元素类型是否可能包含引用。
            /// </summary>
            private static readonly bool MayContainReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

            /// <summary>
            /// 获取或设置用于存储序列段数据的数组。
            /// </summary>
            internal T[]? TargetArray;

            /// <summary>
            /// 获取序列段的起始索引。
            /// </summary>
            internal int Start { get; private set; }

            /// <summary>
            /// 获取序列段的结束索引。
            /// </summary>
            internal int End { get; private set; }

            /// <summary>
            /// 获取剩余的内存部分。
            /// </summary>
            internal Memory<T> RemainingMemory => AvailableMemory.Slice(End);

            /// <summary>
            /// 获取剩余的跨度部分。
            /// </summary>
            internal Span<T> RemainingSpan => RemainingMemory.Span;

            /// <summary>
            /// 获取可用的内存。
            /// </summary>
            internal Memory<T> AvailableMemory => TargetArray ?? default;

            /// <summary>
            /// 获取一个表示已提交数据的数组段。 如果 <see cref="TargetArray"/> 为 null，则返回一个默认的 <see cref="ArraySegment{T}"/>，表示没有已提交的数据。
            /// </summary>
            internal ArraySegment<T> CommittedSegment => TargetArray != null ? new ArraySegment<T>(TargetArray, Start, End - Start) : default;

            /// <summary>
            /// 获取序列段的长度。
            /// </summary>
            internal int Length => End - Start;

            /// <summary>
            /// 获取可写入的字节数。
            /// </summary>
            internal int WritableBytes => AvailableMemory.Length - End;

            /// <summary>
            /// 获取或设置下一个序列段。
            /// </summary>
            internal new FastSequenceSegment? Next
            {
                get => (FastSequenceSegment?)base.Next;
                set => base.Next = value;
            }

            /// <summary>
            /// 获取一个值，指示序列段是否使用外部内存。
            /// </summary>
            internal bool IsForeignMemory => TargetArray == null;

            /// <summary>
            /// 将数组分配给序列段。
            /// </summary>
            /// <param name="array">数组。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Assign(T[] array)
            {
                this.TargetArray = array;
                Memory = array;
            }

            /// <summary>
            /// 重置序列段的内存。
            /// </summary>
            /// <param name="arrayPool">数组池。</param>
            /// <param name="returnArray">指示在重置时是否应将托管数组返回到数组池。通常在序列被转换为外部管理的段（例如 <c>SequenceBufferSegment</c>）时传入 <c>false</c>。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ResetMemory(ArrayPool<T>? arrayPool, bool returnArray)
            {
                ClearReferences(Start, End - Start);
                Memory = default;
                Next = null;
                RunningIndex = 0;
                Start = 0;
                End = 0;
                if (TargetArray != null && returnArray)
                {
                    arrayPool!.Return(TargetArray);
                }
                TargetArray = null;
            }

            /// <summary>
            /// 设置下一个序列段。
            /// </summary>
            /// <param name="segment">下一个序列段。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SetNext(FastSequenceSegment segment)
            {
                Next = segment;
                segment.RunningIndex = RunningIndex + Start + Length;

                if (!IsForeignMemory)
                {
                    Memory = AvailableMemory.Slice(Start, Length);
                }
            }

            /// <summary>
            /// 将序列段的结束索引向前移动指定的数量。
            /// </summary>
            /// <param name="count">要移动的数量。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Advance(int count)
            {
                if (count < 0 || End + count > Memory.Length)
                    throw new ArgumentOutOfRangeException(nameof(count), "count 必须是非负数，且移动后的结束索引不能超过内存长度。");

                End += count;
            }

            /// <summary>
            /// 将序列段的起始索引设置为指定的偏移量。
            /// </summary>
            /// <param name="offset">偏移量。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AdvanceTo(int offset)
            {
                ClearReferences(Start, offset - Start);
                Start = offset;
            }

            /// <summary>
            /// 清除指定范围内的引用。
            /// </summary>
            /// <param name="startIndex">起始索引。</param>
            /// <param name="length">长度。</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearReferences(int startIndex, int length)
            {
                if (MayContainReferences)
                {
                    AvailableMemory.Span.Slice(startIndex, length).Clear();
                }
            }

            public static implicit operator ArraySegment<T>(FastSequenceSegment segment)
                => segment.CommittedSegment;
        }
    }
}