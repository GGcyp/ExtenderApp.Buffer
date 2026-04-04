using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 面向 byte 的缓冲块，内部直接复用 <see cref="MemoryBlock{T}"/>（T=byte）。
    /// - <see cref="Committed"/> 表示已写入字节数（写边界）。
    /// - 本类型不维护消费读指针；需要按消费进度读取时请使用 <see cref="ExtenderApp.Buffer.Reader.ValueMemoryBlockReader{T}"/> 或 <see cref="ExtenderApp.Buffer.Reader.MemoryBlockReader{T}"/> 等读取器。非线程安全；使用完毕请调用 <see cref="Dispose"/> 归还底层资源（视提供者而定）。
    /// </summary>
    public struct ByteBlock : IDisposable, IEquatable<ByteBlock>
    {
        /// <summary>
        /// 默认初始容量（字节）。用于默认构造时的初始分配，实际会根据写入需求自动扩展。
        /// </summary>
        private const int InitialCapacity = 16;

        /// <summary>
        /// 表示空的 <see cref="ByteBlock"/> 实例（不持有底层内存块）。
        /// </summary>
        public static readonly ByteBlock Empty = new(MemoryBlock<byte>.Empty);

        /// <summary>
        /// 底层内存块的封装，用于实际的写入/扩容/回收操作。
        /// </summary>
        public MemoryBlock<byte> Block;

        /// <summary>
        /// 已写入字节数（写指针/写边界）。等于底层 <see cref="MemoryBlock{T}.Committed"/>（转换为 int）。
        /// </summary>
        public int Committed => (int)Block.Committed;

        /// <summary>
        /// 底层缓冲区总容量（单位：字节）。
        /// </summary>
        public int Capacity => (int)Block.Capacity;

        /// <summary>
        /// 当前可立即写入的剩余字节数（剩余可用写空间）。
        /// </summary>
        public int Available => Block.Available;

        /// <summary>
        /// 是否为空（未持有有效底层块或未写入数据）。
        /// </summary>
        public bool IsEmpty => Block == MemoryBlock<byte>.Empty;

        public ReadOnlySpan<byte> CommittedSpan => Block.CommittedSpan;

        public ReadOnlyMemory<byte> CommittedMemory => Block.CommittedMemory;

        public ArraySegment<byte> CommittedSegment => Block.CommittedSegment;

        #region Constructor

        /// <summary>
        /// 使用默认初始容量创建 <see cref="ByteBlock"/> 实例（从共享提供者获取底层块）。
        /// </summary>
        public ByteBlock() : this(InitialCapacity)
        {
        }

        /// <summary>
        /// 使用指定初始容量创建 <see cref="ByteBlock"/>，并从共享提供者租用底层内存块。
        /// </summary>
        /// <param name="initialCapacity">初始容量（字节）。</param>
        public ByteBlock(int initialCapacity) : this(MemoryBlockProvider<byte>.Shared.GetBuffer(initialCapacity))
        {
        }

        /// <summary>
        /// 使用指定的托管数组创建一个 <see cref="ByteBlock"/> 实例（零拷贝包装）。
        /// </summary>
        /// <param name="bytes">用于初始化的托管字节数组（不能为 null）。</param>
        /// <remarks>该构造函数通过提供者将指定数组包装为底层内存块——不会复制数组内容。调用方负责数组的生命周期和并发访问； TryRelease 行为取决于提供者实现，可能不会将数组归还。</remarks>
        public ByteBlock(byte[] bytes) : this(FixedArrayBlockProvider<byte>.Default.GetBuffer(bytes))
        {
        }

        /// <summary>
        /// 使用指定的 <see cref="IMemoryOwner{T}"/> 创建 <see cref="ByteBlock"/>（将 owner 作为底层存储）。
        /// </summary>
        /// <param name="memoryOwner">要包装的 <see cref="IMemoryOwner{byte}"/>（不能为 null）。</param>
        /// <remarks>某些提供者在回收时可能会 TryRelease 该 owner，调用方应明确所有权与生命周期约定，避免在本实例 TryRelease 之后继续使用 owner。</remarks>
        public ByteBlock(IMemoryOwner<byte> memoryOwner) : this(MemoryOwnerBlockProvider<byte>.Default.GetBuffer(memoryOwner))
        {
        }

        /// <summary>
        /// 以指定只读跨度的数据初始化并写入到新缓冲中（根据数据长度预分配容量）。
        /// </summary>
        /// <param name="span">初始数据。</param>
        public ByteBlock(ReadOnlySpan<byte> span) : this(span.Length)
        {
            Write(span);
        }

        /// <summary>
        /// 以指定只读内存的数据初始化并写入到新缓冲中（根据数据长度预分配容量）。
        /// </summary>
        /// <param name="memory">初始数据。</param>
        public ByteBlock(ReadOnlyMemory<byte> memory) : this(memory.Length)
        {
            Write(memory);
        }

        /// <summary>
        /// 使用只读序列初始化并写入到新缓冲中（根据序列长度预分配容量）。
        /// </summary>
        /// <param name="memories">初始只读序列。</param>
        public ByteBlock(ReadOnlySequence<byte> memories) : this((int)memories.Length)
        {
            Write(memories);
        }

        /// <summary>
        /// 使用已有的 <see cref="MemoryBlock{byte}"/> 包装为本结构（不写拷贝；沿用该块当前的已提交长度与容量）。
        /// </summary>
        /// <param name="block">源内存块，不能为 null（视实现可能为默认块）。</param>
        public ByteBlock(MemoryBlock<byte> block)
        {
            Block = block;
        }

        #endregion Constructor

        /// <summary>
        /// 获取用于写入的可写内存（从当前写指针开始）。返回的内存仅在推进写指针或进行下一次写请求之前有效。
        /// </summary>
        /// <param name="sizeHint">建议的最小可写字节数（允许为 0）。</param>
        /// <returns>可写的 <see cref="Memory{byte}"/>。</returns>
        public Memory<byte> GetMemory(int sizeHint = 0) => Block.GetMemory(sizeHint);

        /// <summary>
        /// 获取用于写入的可写跨度（从当前写指针开始）。返回的跨度仅在推进写指针或进行下一次写请求之前有效。
        /// </summary>
        /// <param name="sizeHint">建议的最小可写字节数（允许为 0）。</param>
        /// <returns>可写的 <see cref="Span{byte}"/>。</returns>
        public Span<byte> GetSpan(int sizeHint = 0) => Block.GetSpan(sizeHint);

        /// <summary>
        /// 将写指针前进指定字节数（调用者需保证已实际写入该长度的数据）。
        /// </summary>
        /// <param name="count">推进的字节数（必须为非负值且不超过可写空间）。</param>
        public void Advance(int count) => Block.Advance(count);

        /// <summary>
        /// 将已提交长度（写边界）回退 <paramref name="count"/> 字节，等价于底层 <see cref="MemoryBlock{T}.Rewind(int)"/>。
        /// </summary>
        /// <param name="count">要回退的字节数（非负，且不超过当前 <see cref="Committed"/>）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当参数超出合法范围时抛出。</exception>
        public void Rewind(int count) => Block.Rewind(count);

        /// <summary>
        /// 调用底层 <see cref="MemoryBlock{T}.Clear()"/>：在含引用元素的块类型上会清除已提交区内的引用；对字节块无引用元素时常无可见的字节擦除语义。
        /// </summary>
        public void Clear()
        {
            Block.Clear();
        }

        /// <summary>
        /// 复制当前已提交区 <c>[0..<see cref="Committed"/>)</c> 为新数组（会分配），不改变写边界。
        /// </summary>
        /// <returns>已提交字节的副本；若无已提交数据则返回空数组。</returns>
        public byte[] ToArray() => Block.ToArray();

        /// <summary>
        /// 复制当前已写入区 <c>[0..<see cref="Committed"/>)</c> 为新数组（与 <see cref="ToArray"/> 范围一致，均来自底层已提交内存）。
        /// </summary>
        /// <returns>已提交字节的副本；若无已写入数据则返回空数组。</returns>
        public byte[] ToAllArray() => Block.Memory.ToArray();

        /// <summary>
        /// 将已提交整段字节的顺序反转（不改变 <see cref="Committed"/>）。
        /// </summary>
        public void Reverse()
        {
            Block.Reverse();
        }

        /// <summary>
        /// 反转已提交区内 <paramref name="start"/> 起连续 <paramref name="length"/> 字节的顺序（不改变写边界）。
        /// </summary>
        /// <param name="start">相对于已写入区起始的索引。</param>
        /// <param name="length">要反转的字节数。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="start"/> 与 <paramref name="length"/> 未落在已提交范围内时抛出。</exception>
        public void Reverse(int start, int length)
        {
            Block.Reverse(start, length);
        }

        /// <summary>
        /// 尝试将底层缓冲归还提供者或池。调用后不应再使用此实例（对外暴露的切片/内存将失效）。
        /// </summary>
        public void Dispose()
        {
            Block.TryRelease();
        }

        /// <summary>
        /// 返回已写入范围内字节的十六进制表示（带空格分隔），用于调试输出。
        /// </summary>
        /// <returns>十六进制字符串表示（每字节格式为 "XX "）。</returns>
        public override string ToString()
        {
            StringBuilder sb = new(Committed);
            ReadOnlySpan<byte> span = CommittedSpan;
            for (int i = 0; i < Committed; i++)
            {
                sb.AppendFormat("{0:X2} ", span[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 基于当前已提交字节 <see cref="CommittedSpan"/> 计算哈希码（SHA256 后取摘要前 4 字节转为 int）。
        /// </summary>
        /// <returns>用于哈希集合的哈希码。</returns>
        public override int GetHashCode()
        {
            Span<byte> span = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(CommittedSpan, span);
            return BitConverter.ToInt32(span);
        }

        /// <inheritdoc/>
        public bool Equals(ByteBlock other)
        {
            if (IsEmpty && other.IsEmpty) return true;
            if (IsEmpty || other.IsEmpty) return false;

            return CommittedSpan.SequenceEqual(other.CommittedSpan);
        }

        public static bool operator ==(ByteBlock left, ByteBlock right) => left.Equals(right);

        public static bool operator !=(ByteBlock left, ByteBlock right) => !left.Equals(right);

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is ByteBlock block && Equals(block);

        #region Write

        /// <summary>
        /// 写入单个字节到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="value">要写入的字节。</param>
        public void Write(byte value)
            => Block.Write(value);

        /// <summary>
        /// 写入整个字节数组到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="value">要写入的字节数组（不能为 null）。</param>
        public void Write(byte[] value)
            => Block.Write(value.AsMemory());

        /// <summary>
        /// 写入字节数组的一部分到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="value">要写入的字节数组。</param>
        /// <param name="offset">数组中开始写入的偏移量。</param>
        /// <param name="count">要写入的字节数。</param>
        public void Write(byte[] value, int offset, int count)
            => Block.Write(new ReadOnlyMemory<byte>(value, offset, count));

        /// <summary>
        /// 写入字节数组段到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="segment">要写入的字节数组段。</param>
        public void Write(ArraySegment<byte> segment)
            => Block.Write(segment.AsMemory());

        /// <summary>
        /// 将字符串按指定编码写入当前写指针位置（不包含长度或终止符），并推进写指针。
        /// </summary>
        /// <param name="value">要写入的字符串；若为 null 或空字符串则不执行任何操作。</param>
        /// <param name="encoding">字符编码，默认 UTF-8。</param>
        public void Write(string value, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(value))
                return;

            encoding ??= Encoding.UTF8;
            var byteCount = encoding.GetByteCount(value);
            var memory = GetMemory(byteCount);
            encoding.GetBytes(value, memory.Span);
            Advance(byteCount);
        }

        /// <summary>
        /// 写入一段连续字节到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="span">要写入的数据切片。</param>
        public void Write(ReadOnlySpan<byte> span)
            => Block.Write(span);

        /// <summary>
        /// 写入一段只读内存到当前写指针位置并推进写指针。
        /// </summary>
        /// <param name="memory">要写入的数据内存。</param>
        public void Write(ReadOnlyMemory<byte> memory)
            => Block.Write(memory);

        /// <summary>
        /// 写入一个只读字节序列（可能由多段组成），并推进写指针。
        /// </summary>
        /// <param name="value">只读序列。</param>
        public void Write(in ReadOnlySequence<byte> value)
            => Block.Write(value);

        /// <summary>
        /// 将另一个 <see cref="ByteBlock"/> 的已提交数据写入当前块（按对方 <see cref="CommittedSpan"/> 复制），并推进写指针。
        /// </summary>
        /// <param name="value">来源 <see cref="ByteBlock"/>。</param>
        public void Write(in ByteBlock value)
            => Write(value.CommittedSpan);

        /// <summary>
        /// 将另一个 <see cref="MemoryBlock{byte}"/> 的已提交数据写入当前块（按 <see cref="MemoryBlock{T}.CommittedMemory"/> 复制），并推进写指针。
        /// </summary>
        /// <param name="block">来源内存块，其已写入范围将被追加到当前块。</param>
        public void Write(MemoryBlock<byte> block)
            => Block.Write(block.CommittedSpan);

        /// <summary>
        /// 将 <see cref="NativeByteMemory"/> 的内容写入当前块并推进写指针。
        /// </summary>
        /// <param name="memory">指定原生内存块。</param>
        public void Write(NativeByteMemory memory)
            => Write(memory.Span);

        #endregion Write

        #region FromByteBlock

        /// <summary>
        /// 隐式将 <see cref="ByteBlock"/> 转换为其底层 <see cref="MemoryBlock{byte}"/>（可能为 default/Empty）。
        /// </summary>
        /// <param name="block">源 <see cref="ByteBlock"/>。</param>
        public static implicit operator MemoryBlock<byte>(ByteBlock block)
            => block.Block;

        /// <summary>
        /// 隐式将 <see cref="ByteBlock"/> 转换为当前已提交字节的 <see cref="ReadOnlySpan{byte}"/>（<see cref="CommittedSpan"/>）。
        /// </summary>
        /// <param name="block">源 <see cref="ByteBlock"/>。</param>
        public static implicit operator ReadOnlySpan<byte>(ByteBlock block)
            => block.CommittedSpan;

        /// <summary>
        /// 隐式将 <see cref="ByteBlock"/> 转换为当前已提交字节的 <see cref="ReadOnlyMemory{byte}"/>（<see cref="CommittedMemory"/>）。
        /// </summary>
        /// <param name="block">源 <see cref="ByteBlock"/>。</param>
        public static implicit operator ReadOnlyMemory<byte>(ByteBlock block)
            => block.CommittedMemory;

        /// <summary>
        /// 隐式将 <see cref="ByteBlock"/> 转换为当前已提交字节的 <see cref="ArraySegment{byte}"/>（<see cref="CommittedSegment"/>）。
        /// </summary>
        /// <param name="block">源 <see cref="ByteBlock"/>。</param>
        public static implicit operator ArraySegment<byte>(ByteBlock block)
            => block.CommittedSegment;

        /// <summary>
        /// 隐式将 <see cref="ByteBlock"/> 转换为单段 <see cref="ReadOnlySequence{byte}"/>（基于 <see cref="CommittedMemory"/>）。
        /// </summary>
        /// <param name="block">源 <see cref="ByteBlock"/>。</param>
        public static implicit operator ReadOnlySequence<byte>(ByteBlock block)
            => new ReadOnlySequence<byte>(block.CommittedMemory);

        #endregion FromByteBlock

        #region ToByteBlock

        /// <summary>
        /// 显式从 <see cref="ReadOnlySpan{byte}"/> 创建 <see cref="ByteBlock"/>（会复制数据到新缓冲）。
        /// </summary>
        /// <param name="span">源数据。</param>
        public static explicit operator ByteBlock(ReadOnlySpan<byte> span)
            => new ByteBlock(span);

        /// <summary>
        /// 显式从 <see cref="ReadOnlyMemory{byte}"/> 创建 <see cref="ByteBlock"/>（会复制数据到新缓冲）。
        /// </summary>
        /// <param name="memory">源数据。</param>
        public static explicit operator ByteBlock(ReadOnlyMemory<byte> memory)
            => new ByteBlock(memory);

        /// <summary>
        /// 显式从 <see cref="ReadOnlySequence{byte}"/> 创建 <see cref="ByteBlock"/>（会复制数据到新缓冲）。
        /// </summary>
        /// <param name="sequence">源序列。</param>
        public static explicit operator ByteBlock(ReadOnlySequence<byte> sequence)
            => new ByteBlock(sequence);

        /// <summary>
        /// 显式从 <see cref="MemoryBlock{byte}"/> 创建 <see cref="ByteBlock"/>（不复制底层块，只包装）。
        /// </summary>
        /// <param name="block">源内存块。</param>
        public static explicit operator ByteBlock(MemoryBlock<byte> block)
            => new ByteBlock(block);

        #endregion ToByteBlock
    }
}