using System.Collections;

namespace ExtenderApp.Buffer.SequenceBuffers
{
    public partial class SequenceBufferSegment<T> : IList<ArraySegment<T>>
    {
        ArraySegment<T> IList<ArraySegment<T>>.this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int currentIndex = 0;
                foreach (var segment in this)
                {
                    if (currentIndex == index)
                        return segment.CommittedArraySegment;
                    currentIndex++;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
            set => value = null!;
        }

        bool ICollection<ArraySegment<T>>.IsReadOnly => true;

        void ICollection<ArraySegment<T>>.Add(ArraySegment<T> item)
        {
        }

        void ICollection<ArraySegment<T>>.Clear()
        {
        }

        bool ICollection<ArraySegment<T>>.Contains(ArraySegment<T> item)
        {
            return ((IList<ArraySegment<T>>)this).IndexOf(item) >= 0;
        }

        void ICollection<ArraySegment<T>>.CopyTo(ArraySegment<T>[] array, int arrayIndex)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            int remaining = array.Length - arrayIndex;
            int required = Count;
            if (required > remaining)
                throw new ArgumentException("目标数组空间不足。", nameof(array));

            foreach (var segment in this)
            {
                array[arrayIndex++] = segment.CommittedArraySegment;
            }
        }

        int IList<ArraySegment<T>>.IndexOf(ArraySegment<T> item)
        {
            if (item.Array is null)
                return -1;

            int index = 0;
            foreach (var segment in this)
            {
                if (segment.CommittedArraySegment.Equals(item))
                    return index;
                index++;
            }
            return -1;
        }

        void IList<ArraySegment<T>>.Insert(int index, ArraySegment<T> item)
        {
        }

        bool ICollection<ArraySegment<T>>.Remove(ArraySegment<T> item)
        {
            return false;
        }

        void IList<ArraySegment<T>>.RemoveAt(int index)
        {
        }

        IEnumerator<ArraySegment<T>> IEnumerable<ArraySegment<T>>.GetEnumerator()
        {
            return new ArraySegmentEnumerator(this);
        }

        private struct ArraySegmentEnumerator : IEnumerator<ArraySegment<T>>
        {
            private SegmentEnumerator enumerator;
            private ArraySegment<T> current;

            public ArraySegmentEnumerator(SequenceBufferSegment<T> segment)
            {
                enumerator = new SegmentEnumerator(segment);
                current = default;
            }

            public ArraySegment<T> Current => current;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (!enumerator.MoveNext())
                {
                    current = default;
                    return false;
                }

                current = enumerator.Current.CommittedArraySegment;
                return true;
            }

            public void Reset()
            {
            }

            public void Dispose()
            {
                enumerator.Dispose();
            }
        }
    }
}