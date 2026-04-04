namespace ExtenderApp.Buffer
{
    public partial class AbstractBuffer<T>
    {
        /// <summary>
        /// 最大序列段大小，单位为字节。这个值决定了在处理数据时，每个序列段的最大容量。选择适当的大小可以平衡内存使用和性能，过大可能导致内存压力，过小可能增加处理开销。
        /// </summary>
        public const int MaximumSequenceSegmentSize = 32 * 1024;
    }
}