using System.Buffers;
using ExtenderApp.Buffer.MemoryBlocks;
using ExtenderApp.Buffer.SequenceBuffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 序列缓冲区提供者的抽象基类，定义了创建和回收 <see cref="SequenceBuffer{T}"/> 实例的基本接口和流程。 具体的缓冲区池实现（如 <see
    /// cref="DefaultSequenceBufferProvider{T}"/>）应继承此类并实现相关方法以管理缓冲区的生命周期。 该设计允许灵活替换不同的缓冲区池实现，以适应不同的性能需求和使用场景。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class SequenceBufferProvider<T> : AbstractBufferProvider<T, SequenceBuffer<T>>
    {
        private static readonly Lazy<SequenceBufferProvider<T>> _shared = new(() => new());

        public static SequenceBufferProvider<T> Shared => _shared.Value;

        private readonly ObjectPool<SequenceBuffer<T>> _objectPool =
            ObjectPool.Create(static () => new SequenceBuffer<T>());

        /// <summary>
        /// 根据给定的 <see cref="ReadOnlySequence{T}"/> 构建并返回一个包含对应段的 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <param name="sequence">要转换为序列缓冲区的只读序列，可能由多段组成。</param>
        /// <returns>已初始化并包含输入各段数据的 <see cref="SequenceBuffer{T}"/> 实例。</returns>
        public SequenceBuffer<T> GetBuffer(ReadOnlySequence<T> sequence)
        {
            var buffer = _objectPool.Get();
            buffer.Initialize(this);

            var segmentProvider = SequenceBufferSegmentProvider<T>.Shared;
            var blockProvider = FixedMemoryBlockProvider<T>.Default;

            SequencePosition position = sequence.Start;
            while (sequence.TryGet(ref position, out var memory))
            {
                var block = blockProvider.GetBuffer(memory);
                var segment = segmentProvider.GetSegment(block);
                buffer.Append(segment);
            }
            return buffer;
        }

        /// <summary>
        /// 根据给定的 <see cref="ValueSequenceBuffer{T}"/> 构建并返回一个包含对应段的 <see cref="SequenceBuffer{T}"/>。 该方法与 <see
        /// cref="GetBuffer(ReadOnlySequence{T})"/> 类似，但专门处理封装在 <see cref="ValueSequenceBuffer{T}"/> 中的数据。 该方法内部未对遍历过程中可能抛出的异常执行回收操作；若需要更强的异常安全性，可在调用方捕获异常后负责回收返回的缓冲区或修改本方法以在异常时释放已分配资源。
        /// </summary>
        /// <param name="valueSequence">给定的 <see cref="ValueSequenceBuffer{T}"/> 实例。</param>
        /// <returns>已初始化并包含输入各段数据的 <see cref="SequenceBuffer{T}"/> 实例。 返回的缓冲区来自内部对象池；调用方在不再使用时应通过相应的释放流程将其归还。</returns>
        public SequenceBuffer<T> GetBuffer(ValueSequenceBuffer<T> valueSequence) => GetBuffer(valueSequence.Buffer);

        /// <summary>
        /// 根据给定的 <see cref="FastSequence{T}"/> 构建并返回一个包含对应段的 <see cref="SequenceBuffer{T}"/>。
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal SequenceBuffer<T> GetBuffer(FastSequence<T> sequence)
        {
            if (sequence.IsEmpty)
                return SequenceBuffer<T>.Empty;

            var buffer = _objectPool.Get();
            buffer.Initialize(this);
            var blockProvider = ArrayPoolBlockProvider<T>.Default;
            var segmentProvider = SequenceBufferSegmentProvider<T>.Shared;

            sequence.PinSegmentArray();
            FastSequence<T>.FastSequenceSegment? Segment = sequence.First!;
            while (Segment != null)
            {
                var block = blockProvider.GetBuffer(Segment.TargetArray!, Segment.Length);
                var segment = segmentProvider.GetSegment(block);
                buffer.Append(segment);
                Segment = Segment.Next;
            }
            return buffer;
        }

        protected override SequenceBuffer<T> CreateBufferProtected(int sizeHint)
        {
            var buffer = _objectPool.Get();
            buffer.Initialize(this);
            buffer.GetSpan(sizeHint);
            return buffer;
        }

        protected override void ReleaseProtected(SequenceBuffer<T> buffer)
        {
            _objectPool.Release(buffer);
        }
    }
}