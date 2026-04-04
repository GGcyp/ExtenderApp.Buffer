using System.Buffers;

namespace ExtenderApp.Buffer
{
    public class EmptyBuffer<T> : AbstractBuffer<T>
    {
        public override long Capacity => 0;

        public override int Available => 0;

        public override long Committed => 0;

        public override ReadOnlySequence<T> CommittedSequence => ReadOnlySequence<T>.Empty;

        public override void Advance(int count)
        {
        }

        public override void Clear()
        {
        }

        public override AbstractBuffer<T> Clone()
        {
            return this;
        }

        public override Memory<T> GetMemory(int sizeHint = 0)
        {
            return Memory<T>.Empty;
        }

        public override Span<T> GetSpan(int sizeHint = 0)
        {
            return Span<T>.Empty;
        }

        public override AbstractBuffer<T> Slice(long start, long length)
        {
            return this;
        }

        public override T[] ToArray()
        {
            return Array.Empty<T>();
        }

        protected override MemoryHandle PinProtected(int elementIndex)
        {
            return new MemoryHandle();
        }

        protected override void ReleaseProtected()
        {
        }

        protected override AbstractBuffer<T> SliceProtected(long start, long length)
        {
            return this;
        }

        protected override bool TryReleaseProtected()
        {
            return true;
        }

        protected override void UpdateCommittedProtected(ReadOnlySpan<T> span, long committedPosition)
        {
        }
    }
}