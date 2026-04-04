using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using ExtenderApp.Buffer.Primitives;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 提供从 Span、数组、<see cref="ReadOnlySequence{T}"/> 等来源创建 <see cref="AbstractBuffer{T}"/>（例如 <see cref="MemoryBlock{T}"/>、<see cref="SequenceBuffer{T}"/>）的工厂入口。
    /// </summary>
    /// <remarks>各重载返回的实例可能来自对象池或对外部缓冲的包装，释放与生命周期约定取决于具体类型。</remarks>
    public static class AbstractBuffer
    {
        /// <summary>
        /// 获取一个空的 <see cref="AbstractBuffer{T}"/> 实例，表示一个容量为 0 的缓冲区。
        /// </summary>
        /// <typeparam name="T">缓冲区元素的类型。</typeparam>
        public static AbstractBuffer<T> Empty<T>() => AbstractBuffer<T>.Empty;

        /// <summary>
        /// 获取一个来自 <see cref="MemoryBlock{T}"/> 的缓冲区实例，使用默认的尺寸策略分配或复用底层存储。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <returns>一个可写的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>() => MemoryBlock<T>.GetBuffer();

        /// <summary>
        /// 获取一个指定大小的 <see cref="MemoryBlock{T}"/> 缓冲区实例。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="size">请求的最小元素数量。</param>
        /// <returns>具有至少 <paramref name="size"/> 可用空间的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>(int size) => MemoryBlock<T>.GetBuffer(size);

        /// <summary>
        /// 使用给定的只读跨度创建一个基于该跨度的 <see cref="AbstractBuffer{T}"/>。 返回的缓冲区包装了传入的 <see cref="ReadOnlySpan{T}"/> 数据（通常会复制或封装为只读块，具体取决于实现）。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="span">用于初始化缓冲区的只读跨度。</param>
        /// <returns>表示 <paramref name="span"/> 内容的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>(ReadOnlySpan<T> span) => MemoryBlock<T>.GetBuffer(span);

        /// <summary>
        /// 使用给定的只读内存创建一个基于该内存的 <see cref="AbstractBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="memory">用于初始化缓冲区的只读内存。</param>
        /// <returns>表示 <paramref name="memory"/> 内容的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>(ReadOnlyMemory<T> memory) => MemoryBlock<T>.GetBuffer(memory);

        /// <summary>
        /// 使用给定的数组创建一个基于该数组的 <see cref="AbstractBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="array">用于初始化缓冲区的数组。</param>
        /// <returns>表示 <paramref name="array"/> 内容的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>(T[] array) => MemoryBlock<T>.GetBuffer(array);

        /// <summary>
        /// 使用给定的数组段创建一个基于该段的 <see cref="AbstractBuffer{T}"/>。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="segment">用于初始化缓冲区的数组段。</param>
        /// <returns>表示 <paramref name="segment"/> 内容的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetBlock<T>(ArraySegment<T> segment) => MemoryBlock<T>.GetBuffer(segment);

        /// <summary>
        /// 使用给定的 <see cref="ReadOnlySequence{T}"/> 创建一个序列缓冲区 <see cref="AbstractBuffer{T}"/>。 该方法适用于多段数据的场景（例如流式或分段内存）。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <param name="sequence">初始化缓冲区的只读序列。</param>
        /// <returns>表示 <paramref name="sequence"/> 的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetSequence<T>(ReadOnlySequence<T> sequence) => SequenceBuffer<T>.GetBuffer(sequence);

        /// <summary>
        /// 获取一个空的序列缓冲区实例（不包含任何段），通常用于表示空的 ReadOnlySequence 场景。
        /// </summary>
        /// <typeparam name="T">缓冲区元素类型。</typeparam>
        /// <returns>空的 <see cref="AbstractBuffer{T}"/> 实例。</returns>
        public static AbstractBuffer<T> GetSequence<T>() => SequenceBuffer<T>.GetBuffer();
    }

    /// <summary>
    /// 表示用于写入缓冲区的抽象写入器。
    /// </summary>
    /// <remarks>继承自 <see cref="FreezeObject"/>，在调用 <see cref="Release"/> 前会检查冻结状态以避免在被引用时回收。 实现者应提供用于推进写入位置和获取可写内存/跨度的具体逻辑。 实现此抽象类的类型通常用于可重用或池化的缓冲写入场景。</remarks>
    public abstract partial class AbstractBuffer<T> : FreezeObject, IBufferWriter<T>, IPinnable, IEnumerable<ReadOnlyMemory<T>>
    {
        private const string ErrorMessage = "当前缓存还被引用中，无法进行回收操作。";
        private const string WriteFrozenMessage = "当前缓存处于写入冻结状态，无法进行写入操作。";

        public static readonly AbstractBuffer<T> Empty = new EmptyBuffer<T>();

        /// <summary>
        /// 被固定次数（Pin 调用次数）和当前固定状态的标志。 由于 Pin/Unpin 的调用可能不成对（例如多次 Pin 而未及时 Unpin），因此通过 IsPinned 标志来跟踪当前是否处于固定状态，并通过 memoryHandle 来保存最后一次 Pin 返回的句柄以便在
        /// Unpin 时正确释放。
        /// </summary>
        private int pinCount;

        /// <summary>
        /// 当内存被固定时保存的 MemoryHandle，以便在 Unpin 时正确释放。 该字段仅在 IsPinned 为 true 时有效；当 IsPinned 为 false 时应为 default。 派生类在实现 PinProtected 时应确保正确设置此字段，并在
        /// UnpinProtected 中负责释放对应的内存句柄。
        /// </summary>
        private MemoryHandle memoryHandle;

        /// <summary>
        /// 写入冻结引用计数。大于 0 表示写入被冻结。
        /// </summary>
        private long writeFreezeCount;

        /// <summary>
        /// 当前缓冲区是否处于固定状态（被 Pin 调用且尚未 Unpin）。 该属性通过内部计数器 pinCount 来跟踪固定状态，确保在多次 Pin 调用后仍能正确反映当前是否处于固定状态。 派生类在实现 PinProtected 和 UnpinProtected 时应确保正确更新
        /// pinCount 以保持 IsPinned 的准确性。
        /// </summary>
        public bool IsPinned => Interlocked.CompareExchange(ref pinCount, 0, 0) > 0;

        /// <summary>
        /// 指示当前缓冲区是否处于写入冻结状态（引用计数大于 0 时视为冻结）。
        /// </summary>
        public bool IsWriteFrozen => Interlocked.Read(ref writeFreezeCount) > 0;

        /// <summary>
        /// 将写入操作设为冻结（支持嵌套）。在冻结期间，所有写入/提交相关操作将抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreezeWrite()
        {
            Interlocked.Increment(ref writeFreezeCount);
        }

        /// <summary>
        /// 解除一次写入冻结（引用计数递减）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnfreezeWrite()
        {
            var newCount = Interlocked.Decrement(ref writeFreezeCount);
            if (newCount < 0)
                Interlocked.Exchange(ref writeFreezeCount, 0);
        }

        /// <summary>
        /// 当缓冲区处于写入冻结状态时抛出异常。
        /// </summary>
        /// <param name="message">异常消息。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CheckWriteFrozen(string message = WriteFrozenMessage)
        {
            if (IsWriteFrozen)
                throw new InvalidOperationException(message);
        }

        /// <summary>
        /// 缓冲区的总容量（以元素数计）。
        /// </summary>
        public abstract long Capacity { get; }

        /// <summary>
        /// 当前缓冲区可供写入的元素数量（剩余可写空间）。
        /// </summary>
        /// <remarks>语义为从当前写入位置到底层存储末尾的可用元素数，不包含需要扩容后才可用的空间。</remarks>
        public abstract int Available { get; }

        /// <summary>
        /// 缓冲区中已提交（已写入）的元素总数。
        /// </summary>
        /// <remarks>
        /// 该值表示已准备好被读取或消费的数据长度（可用于构建 <see cref="CommittedSequence"/>）。 使用 <see cref="OnCommittedChanged"/> 或订阅 <see cref="CommittedChanged"/> 以在提交长度改变时接收通知。
        /// </remarks>
        public abstract long Committed { get; }

        /// <summary>
        /// 表示已提交数据的只读序列（ReadOnlySequence），用于将缓冲区内容作为可读序列暴露给调用方。
        /// </summary>
        public abstract ReadOnlySequence<T> CommittedSequence { get; }

        /// <summary>
        /// 获取一个值，指示当前缓冲区是否处于激活状态（已准备好接受写入）。 该属性由具体实现定义其语义，通常用于指示缓冲区是否已正确初始化并且可以安全地进行写入操作。 调用方可以根据此属性的值来决定是否执行写入或其他相关操作。
        /// </summary>
        public bool IsActive { get; protected set; } = false;

        /// <summary>
        /// 将写入位置向前推进指定数量的元素。
        /// </summary>
        /// <param name="count">要推进的元素数量（必须为非负值）。</param>
        public abstract void Advance(int count);

        /// <summary>
        /// 获取一个至少包含 <paramref name="sizeHint"/> 个元素的可写 <see cref="Memory{T}"/>。
        /// </summary>
        /// <param name="sizeHint">建议的最小可用大小（可为 0，表示不作特殊建议）。</param>
        /// <returns>用于写入的 <see cref="Memory{T}"/>。</returns>
        public abstract Memory<T> GetMemory(int sizeHint = 0);

        /// <summary>
        /// 获取一个至少包含 <paramref name="sizeHint"/> 个元素的可写 <see cref="Span{T}"/>。
        /// </summary>
        /// <param name="sizeHint">建议的最小可用大小（可为 0，表示不作特殊建议）。</param>
        /// <returns>用于写入的 <see cref="Span{T}"/>。</returns>
        public abstract Span<T> GetSpan(int sizeHint = 0);

        /// <summary>
        /// 获取当前缓冲区可供写入的内存区域（从当前写入位置开始）。 返回的内存长度应等于 <see cref="Available"/>，表示从当前写入位置到底层存储末尾的可用元素数。 调用方可以使用返回的内存进行写入，并在完成后调用 <see
        /// cref="Advance(int)"/> 推进写入位置。
        /// </summary>
        /// <returns>当前缓冲区剩余可供写入的内存区域。</returns>
        public Memory<T> GetAvailableMemory() => GetMemory(Available);

        /// <summary>
        /// 获取当前缓冲区可供写入的跨度（从当前写入位置开始）。 返回的跨度长度应等于 <see cref="Available"/>，表示从当前写入位置到底层存储末尾的可用元素数。 调用方可以使用返回的跨度进行写入，并在完成后调用 <see cref="Advance(int)"/> 推进写入位置。
        /// </summary>
        /// <returns>当前缓冲区剩余可供写入的内存区域。</returns>
        public Span<T> GetAvailableSpan() => GetSpan(Available);

        /// <summary>
        /// 清空当前缓冲区的内容并重置写入位置。 调用此方法后， <see cref="Committed"/> 将重置为 0， <see cref="CommittedSequence"/> 将变为空序列。 具体实现可能会选择保留底层存储以供后续写入使用（例如池化场景），但应确保调用此方法后缓冲区状态被正确重置以允许重新使用。
        /// </summary>
        public abstract void Clear();

        #region Write

        /// <summary>
        /// 将指定的只读跨度 <paramref name="source"/> 的内容写入当前缓冲区（从当前写入位置开始），并将写入位置向前推进相应数量。
        /// </summary>
        /// <param name="source">要写入的只读数据跨度。</param>
        /// <remarks>实现通过调用 <see cref="GetSpan(int)"/> 获取足够的写入空间并调用 <see cref="Advance(int)"/> 推进写入位置。 如果底层缓冲无法满足写入需求，具体实现可能会扩容或抛出异常（由派生类决定）。</remarks>
        public virtual void Write(ReadOnlySpan<T> source)
        {
            CheckWriteFrozen();
            int length = source.Length;
            source.CopyTo(GetSpan(length));
            Advance(length);
        }

        /// <summary>
        /// 将指定的只读内存 <paramref name="memory"/> 的内容写入当前缓冲区（从当前写入位置开始），并推进写入位置。
        /// </summary>
        /// <param name="memory">要写入的只读内存。</param>
        public void Write(ReadOnlyMemory<T> memory)
        {
            CheckWriteFrozen();
            Write(memory.Span);
        }

        /// <summary>
        /// 将指定数组 <paramref name="array"/> 的全部内容写入当前缓冲区（从当前写入位置开始），并推进写入位置。
        /// </summary>
        /// <param name="array">要写入的数组（不能为 null）。</param>
        public void Write(T[] array)
        {
            CheckWriteFrozen();
            Write(array.AsSpan());
        }

        /// <summary>
        /// 将一个元素（按引用方式传入以减少拷贝）写入当前缓冲区并推进写入位置 1。
        /// </summary>
        /// <param name="item">要写入的元素（以 <c>in</c> 传入以避免复制开销）。</param>
        public void Write(in T item)
        {
            CheckWriteFrozen();
            GetSpan(1)[0] = item;
            Advance(1);
        }

        /// <summary>
        /// 将单个元素写入当前缓冲区并推进写入位置 1。
        /// </summary>
        /// <param name="item">要写入的元素。</param>
        public void Write(T item)
        {
            CheckWriteFrozen();
            GetSpan(1)[0] = item;
            Advance(1);
        }

        /// <summary>
        /// 将枚举序列 <paramref name="items"/> 的所有元素逐个写入当前缓冲区（每个元素通过 <see cref="Write(T)"/> 写入）。
        /// </summary>
        /// <param name="items">要写入的枚举集合，允许为任何实现了 <see cref="IEnumerable{T}"/> 的序列。</param>
        /// <remarks>
        /// 对于大型或高频写入场景，建议尽量使用批量写入（例如 <see cref="ReadOnlySpan{T}"/> / <see cref="ReadOnlyMemory{T}"/> 或 <see cref="ReadOnlySequence{T}"/>） 以减少多次扩容与调用开销。
        /// </remarks>
        public void Write(IEnumerable<T> items)
        {
            CheckWriteFrozen();
            foreach (var item in items)
                Write(item);
        }

        /// <summary>
        /// 将多段只读序列 <paramref name="memories"/> 中的所有段依次写入当前缓冲区。
        /// </summary>
        /// <param name="memories">要写入的只读序列（可能由多段组成）。</param>
        /// <remarks>逐段读取序列中的 <see cref="ReadOnlyMemory{T}"/> 并调用对应的写入方法以保证零拷贝或最小拷贝开销。</remarks>
        public void Write(ReadOnlySequence<T> memories)
        {
            CheckWriteFrozen();
            if (memories.IsSingleSegment)
            {
                Write(memories.First);
            }
            else
            {
                SequencePosition start = memories.Start;
                while (memories.TryGet(ref start, out ReadOnlyMemory<T> memory))
                    Write(memory);
            }
        }

        #endregion Write

        #region Update

        /// <summary>
        /// 更新已提交长度以反映已写入数据的状态变化。调用此方法时会验证传入的跨度和提交位置是否在有效范围内，以执行实际的提交状态更新逻辑。
        /// </summary>
        /// <param name="span">需要更新后的数据片段。</param>
        /// <param name="committedPosition">需要更新的坐标位置。</param>
        public void UpdateCommitted(ReadOnlySpan<T> span, long committedPosition = 0)
        {
            CheckWriteFrozen();
            if (span.IsEmpty)
                return;

            if (committedPosition < 0 || committedPosition + span.Length > Committed)
                throw new ArgumentOutOfRangeException(nameof(committedPosition), "committedPosition 必须在已写入范围内，且 span 能完全写入。");

            UpdateCommittedProtected(span, committedPosition);
        }

        /// <summary>
        /// 更新已提交长度的受保护方法，由 <see cref="UpdateCommitted"/> 调用以执行实际的提交状态更新逻辑。派生类应在此方法中实现根据传入的已写入数据跨度和相对于当前提交位置的偏移量来更新内部提交状态（例如调整已提交长度、更新相关缓存等）。 该方法由
        /// <see cref="UpdateCommitted"/> 进行参数验证后调用，因此派生类可以假设传入的参数已经过验证并且符合预期范围。
        /// </summary>
        /// <param name="span">需要更新后的数据片段。</param>
        /// <param name="committedPosition">需要更新的坐标位置。</param>
        protected abstract void UpdateCommittedProtected(ReadOnlySpan<T> span, long committedPosition);

        #endregion Update

        #region Release

        /// <summary>
        /// 尝试释放/回收当前缓冲区实例的资源。 在调用此方法时会先尝试解除冻结状态以允许回收（如果当前未被冻结）。 如果对象处于冻结状态（被引用）则不会进行释放并返回 false；如果成功释放则返回 true。
        /// </summary>
        /// <returns>如果成功释放则返回 true；如果当前对象处于冻结状态（被引用）则返回 false。</returns>
        public bool TryRelease()
        {
            if (!IsActive)
                return false;

            Unfreeze(); // 尝试解除冻结以允许回收（如果当前未被冻结）
            if (IsFrozen || IsWriteFrozen)
                return false;

            IsActive = false;
            return TryReleaseProtected();
        }

        /// <summary>
        /// 释放/回收当前缓冲区实例的资源。 在调用此方法时会先尝试解除冻结状态以允许回收（如果当前未被冻结）。 如果对象处于冻结状态（被引用）则会抛出 <see cref="InvalidOperationException"/>；如果成功释放则调用派生类的 <see
        /// cref="ReleaseProtected"/> 完成实际回收逻辑。
        /// </summary>
        /// <exception cref="InvalidOperationException">当当前缓冲区被引用（冻结）时抛出。</exception>
        public void Release()
        {
            if (!IsActive)
                throw new InvalidOperationException("当前缓冲区未处于激活状态，无法进行回收操作。");

            Unfreeze(); // 尝试解除冻结以允许回收（如果当前未被冻结）
            CheckWriteFrozen();
            CheckFrozen(ErrorMessage);
            ReleaseProtected();
            IsActive = false;
        }

        /// <summary>
        /// 派生类实现具体的释放/回收逻辑（例如归还池、释放底层资源等）。
        /// </summary>
        protected abstract void ReleaseProtected();

        /// <summary>
        /// 派生类实现具体的解除固定逻辑（例如释放固定句柄、允许内存移动等）。 该方法由基类的 <see cref="Unpin"/> 调用以执行实际的解除固定操作。 派生实现应确保在调用此方法后底层内存可以被安全地移动或回收，并且之前由 <see
        /// cref="PinProtected"/> 返回的 <see cref="MemoryHandle"/> 不再有效。
        /// </summary>
        /// <returns>如果成功解除固定则返回 true；如果当前未处于固定状态或无法解除固定则返回 false。</returns>
        protected abstract bool TryReleaseProtected();

        #endregion Release

        #region Pin

        /// <summary>
        /// 将指定索引处的元素固定在内存中并返回对应的 <see cref="MemoryHandle"/>，以便调用方可以安全地获取该元素的地址或传递给非托管代码。
        /// </summary>
        /// <param name="elementIndex">要固定的元素在缓冲区中的索引（相对于缓冲区起始位置的零基索引）。派生类应对越界情况进行验证并抛出合适的异常（例如 <see cref="ArgumentOutOfRangeException"/>）。</param>
        /// <returns>
        /// 一个 <see cref="MemoryHandle"/> 实例，表示已固定的内存句柄。调用方在完成对固定内存的使用后必须调用 <see cref="Unpin"/> 以释放固定状态。 如果缓冲区当前已经处于固定状态（ <see cref="Pin"/> 已被调用且尚未
        /// <see cref="Unpin"/>），则返回先前保存的同一 <see cref="MemoryHandle"/> 实例。
        /// </returns>
        /// <remarks>
        /// - 此方法通过内部标记防止重复固定（多次调用将返回相同的句柄），实际的固定逻辑由 <see cref="PinProtected"/> 实现。
        /// - 派生类在实现 <see cref="PinProtected"/> 时必须确保固定期间底层内存不会被移动或回收，并在 <see cref="UnpinProtected"/> 中正确释放固定状态。
        /// - 本方法及其派生实现通常不是线程安全的；如果需要在多线程场景中使用，请在调用方进行同步。
        /// </remarks>
        public MemoryHandle Pin(int elementIndex)
        {
            if (!IsPinned)
            {
                Freeze(); // 固定前先冻结以防止回收
                memoryHandle = PinProtected(elementIndex);
            }

            Interlocked.Increment(ref pinCount);
            return memoryHandle;
        }

        /// <summary>
        /// 派生类在此方法中实现将指定索引处元素固定到托管外的具体逻辑，并返回对应的 <see cref="MemoryHandle"/>。
        /// </summary>
        /// <param name="elementIndex">要固定的元素索引（从 0 开始）。实现应在索引无效时抛出 <see cref="ArgumentOutOfRangeException"/> 或其他合适的异常。</param>
        /// <returns>表示已固定内存的 <see cref="MemoryHandle"/>。实现必须保证返回的句柄在调用 <see cref="UnpinProtected"/> 前保持有效。</returns>
        /// <remarks>
        /// - 该方法由基类的 <see cref="Pin(int)"/> 调用；派生实现可以假设基类已更新固定标志（IsPinned）。
        /// - 实现应确保在固定期间底层内存不会被移动或回收，并负责为 <see cref="UnpinProtected"/> 提供对等的释放逻辑。
        /// - 建议实现避免分配大量托管资源，并遵循低延迟、高可靠性的约定。
        /// </remarks>
        protected abstract MemoryHandle PinProtected(int elementIndex);

        /// <summary>
        /// 释放先前通过 <see cref="Pin(int)"/> 固定的元素，允许该内存再次被移动或回收。
        /// </summary>
        /// <remarks>
        /// - 本方法首先调用派生类的 <see cref="UnpinProtected"/> 以执行具体释放逻辑，然后将内部固定标志清除并重置保存的 <see cref="MemoryHandle"/>。
        /// - 调用此方法后先前由 <see cref="Pin(int)"/> 返回的 <see cref="MemoryHandle"/> 将不再有效，不得再访问其地址。
        /// - 此方法对重复调用应为无害（幂等）：若当前未固定，派生实现应能安全处理无操作或快速返回。
        /// - 与 <see cref="Pin(int)"/> 一样，线程安全性由调用方负责；若在多线程场景下使用，请外部同步调用。
        /// </remarks>
        public virtual void Unpin()
        {
            Interlocked.Decrement(ref pinCount);
            if (IsPinned)
                return;

            Unfreeze(); // 解除冻结以允许回收
            memoryHandle.Dispose();
            memoryHandle = default;
        }

        #endregion Pin

        /// <summary>
        /// 对当前缓存区进行切片以创建一个新的缓冲区实例，当 <paramref name="start"/> 和 <paramref name="length"/> 都等于0时会直接返回当前实例。切片后的缓冲区与原缓冲区共享底层存储，但具有独立的写入位置和提交状态。该方法由基类的
        /// <see cref="Slice(long, long)"/> 调用，派生类在此方法中实现具体的切片逻辑（例如创建新的缓冲区实例并设置相应的起始位置和长度）。派生实现应确保切片后的缓冲区状态被正确初始化，并且后续对切片或原缓冲区的写入/提交操作不会相互影响。
        /// </summary>
        /// <param name="start">切片的起始位置（以元素数计）。默认为 0，表示从原缓冲区的起始位置开始。</param>
        /// <param name="length">切片的长度（以元素数计）。如果为 0，则表示从起始位置到原缓冲区末尾的所有元素。</param>
        /// <returns>一个新的 <see cref="AbstractBuffer{T}"/> 实例，表示原缓冲区中指定范围的切片。</returns>
        public virtual AbstractBuffer<T> Slice(long start = 0, long length = 0)
        {
            if (start == 0 && length == 0)
                return this;

            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "start 必须为非负值。");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length 必须为非负值。");
            if (start + length > Committed)
                throw new ArgumentOutOfRangeException(nameof(length), "start + length 必须不超过已提交长度。");

            if (length == 0)
                length = Committed - start;

            return SliceProtected(start, length);
        }

        /// <summary>
        /// 对当前缓冲区进行切片以创建一个新的缓冲区实例，该实例表示原缓冲区中指定范围的元素。 切片后的缓冲区与原缓冲区共享底层存储，但具有独立的写入位置和提交状态。 该方法由基类的 <see cref="Slice(long, long)"/>
        /// 调用，派生类在此方法中实现具体的切片逻辑（例如创建新的缓冲区实例并设置相应的起始位置和长度）。 派生实现应确保切片后的缓冲区状态被正确初始化，并且后续对切片或原缓冲区的写入/提交操作不会相互影响。
        /// </summary>
        /// <param name="start">切片的起始位置（以元素数计）。默认为 0，表示从原缓冲区的起始位置开始。</param>
        /// <param name="length">切片的长度（以元素数计）。如果为 0，则表示从起始位置到原缓冲区末尾的所有元素。</param>
        /// <returns>一个新的 <see cref="AbstractBuffer{T}"/> 实例，表示原缓冲区中指定范围的切片。</returns>
        protected abstract AbstractBuffer<T> SliceProtected(long start, long length);

        /// <summary>
        /// 为当前缓冲区创建一个新的实例，该实例与原缓冲区共享底层存储但具有独立的写入位置和提交状态。 具体实现由派生类提供，通常会返回一个新的缓冲区实例，该实例引用原缓冲区的底层存储但具有独立的写入位置和提交状态。 克隆后的缓冲区初始状态与原缓冲区相同（例如容量、已提交长度等），但后续对克隆或原缓冲区的写入/提交操作不会相互影响。
        /// </summary>
        /// <returns>新的 <see cref="AbstractBuffer{T}"/> 实例</returns>
        public abstract AbstractBuffer<T> Clone();

        /// <summary>
        /// 缓冲区内容转换为数组。 具体实现由派生类提供，通常会返回一个包含已提交数据的数组副本。 调用此方法可能会涉及内存分配和数据复制，因此在性能敏感场景下应谨慎使用。
        /// </summary>
        /// <returns>转存好的数组实例</returns>
        public abstract T[] ToArray();

        public override string ToString()
            => $"AbstractBuffer<{typeof(T).Name}>: Capacity={Capacity}, Committed={Committed}, Available={Available}, IsFrozen={IsFrozen}, IsWriteFrozen={IsWriteFrozen}, IsPinned={IsPinned}";

        public IEnumerator<ReadOnlyMemory<T>> GetEnumerator()
        {
            var sequence = CommittedSequence;
            if (sequence.IsEmpty)
                yield break;

            var position = sequence.Start;
            while (sequence.TryGet(ref position, out ReadOnlyMemory<T> memory))
            {
                yield return memory;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 隐式将缓冲区转换为只读序列，便于将已提交数据作为 <see cref="ReadOnlySequence{T}"/> 传递。
        /// </summary>
        /// <param name="buffer">源缓冲区实例。</param>
        public static explicit operator ReadOnlySequence<T>(AbstractBuffer<T> buffer)
            => buffer.CommittedSequence;

        #region ToBuffer

        public static explicit operator AbstractBuffer<T>(Span<T> span)
            => MemoryBlock<T>.GetBuffer(span);

        public static explicit operator AbstractBuffer<T>(ReadOnlySpan<T> span)
            => MemoryBlock<T>.GetBuffer(span);

        public static implicit operator AbstractBuffer<T>(Memory<T> memory)
            => MemoryBlock<T>.GetBuffer(memory);

        public static implicit operator AbstractBuffer<T>(ReadOnlyMemory<T> memory)
            => MemoryBlock<T>.GetBuffer(memory);

        public static implicit operator AbstractBuffer<T>(T[] array)
            => MemoryBlock<T>.GetBuffer(array);

        public static implicit operator AbstractBuffer<T>(ArraySegment<T> segment)
            => MemoryBlock<T>.GetBuffer(segment);

        public static implicit operator AbstractBuffer<T>(ReadOnlySequence<T> memories)
            => SequenceBuffer<T>.GetBuffer(memories);

        #endregion ToBuffer
    }
}