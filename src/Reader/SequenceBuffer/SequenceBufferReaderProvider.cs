namespace ExtenderApp.Buffer.Reader
{
    /// <summary>
    /// 可重用的 <see cref="SequenceBufferReader{T}"/> 提供者，用于管理读取器实例的获取与回收。
    /// 实现类似于其他缓冲区读取器提供者，使用对象池以复用读取器实例并在分配时冻结目标缓冲。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public class SequenceBufferReaderProvider<T> : AbstractBufferReaderProvider<T, SequenceBuffer<T>>
    {
        private static readonly Lazy<SequenceBufferReaderProvider<T>> _default = new(() => new());

        public static SequenceBufferReaderProvider<T> Default => _default.Value;

        private readonly ObjectPool<SequenceBufferReader<T>> _readerPool = ObjectPool.Create<SequenceBufferReader<T>>();

        protected override AbstractBufferReader<T> GetReaderProtected(SequenceBuffer<T> buffer)
        {
            var reader = _readerPool.Get();
            reader.ReaderProvider = this;
            reader.Initialize(this, buffer);
            return reader;
        }

        public override void Release(AbstractBufferReader<T> reader)
        {
            if (reader is SequenceBufferReader<T> seqReader)
            {
                seqReader.ReaderProvider = default!;
                _readerPool.Release(seqReader);
            }
            else
            {
                reader.Dispose();
            }
        }
    }
}