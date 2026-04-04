namespace ExtenderApp.Buffer.Reader
{
    public class MemoryBlockReaderProvider<T> : AbstractBufferReaderProvider<T, MemoryBlock<T>>
    {
        private static readonly Lazy<MemoryBlockReaderProvider<T>> _default =
            new(() => new());

        public static MemoryBlockReaderProvider<T> Default => _default.Value;

        private readonly ObjectPool<MemoryBlockReader<T>> _readerPool =
            ObjectPool.Create<MemoryBlockReader<T>>();

        protected override AbstractBufferReader<T> GetReaderProtected(MemoryBlock<T> buffer)
        {
            var reader = _readerPool.Get();
            reader.ReaderProvider = this;
            reader.Initialize(this, buffer);
            return reader;
        }

        public override void Release(AbstractBufferReader<T> reader)
        {
            if (reader is MemoryBlockReader<T> memoryBlockReader)
            {
                memoryBlockReader.ReaderProvider = default!;
                _readerPool.Release(memoryBlockReader);
            }
            else
            {
                reader.Dispose();
            }
        }
    }
}