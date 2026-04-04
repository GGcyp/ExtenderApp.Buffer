using System.Buffers;

namespace ExtenderApp.Buffer
{
    public partial class SequenceBuffer<T>
    {
        public static SequenceBuffer<T> GetBuffer()
            => SequenceBufferProvider<T>.Shared.GetBuffer();

        public static SequenceBuffer<T> GetBuffer(ReadOnlySequence<T> memories)
            => SequenceBufferProvider<T>.Shared.GetBuffer(memories);
    }
}