using ExtenderApp.Buffer.Primitives;

namespace ExtenderApp.Buffer.Reader
{
    /// <summary>
    /// 抽象的读取器提供者基类，负责读取器的回收/释放逻辑。
    /// 实现类可复写回收策略（例如将读取器放回池中或释放资源）。
    /// </summary>
    /// <typeparam name="T">缓冲区元素的类型。</typeparam>
    public abstract class AbstractBufferReaderProvider<T> : DisposableObject
    {
        /// <summary>
        /// 释放或回收指定的读取器实例。
        /// 调用实现应确保读取器被正确重置并且不再持有对底层缓冲区的强引用（如有必要）。
        /// </summary>
        /// <param name="reader">要释放或回收的读取器实例，可能为 null 时实现应能安全处理（若不允许 null 可在实现中抛出）。</param>
        public abstract void Release(AbstractBufferReader<T> reader);
    }

    /// <summary>
    /// 特化的读取器提供者基类，针对特定缓冲区类型提供读取器获取接口。
    /// </summary>
    /// <typeparam name="T">缓冲区元素的类型。</typeparam>
    /// <typeparam name="TBuffer">具体的缓冲区类型，必须继承自 <see cref="AbstractBuffer{T}"/>。</typeparam>
    public abstract class AbstractBufferReaderProvider<T, TBuffer> : AbstractBufferReaderProvider<T>
        where TBuffer : AbstractBuffer<T>
    {
        /// <summary>
        /// 获取一个可用于读取指定缓冲区的读取器实例。
        /// 实现可以选择复用池中已有实例或新建实例。调用方在使用结束后应调用对应的 <see cref="AbstractBufferReaderProvider{T}.Release(AbstractBufferReader{T})"/> 进行回收。
        /// </summary>
        /// <param name="buffer">要为其创建或分配读取器的缓冲区实例，不能为空。</param>
        /// <returns>一个绑定到 <paramref name="buffer"/> 的 <see cref="AbstractBufferReader{T}"/> 实例。</returns>
        public AbstractBufferReader<T> GetReader(TBuffer buffer)
        {
            buffer.Freeze();
            return GetReaderProtected(buffer);
        }

        /// <summary>
        /// 获取一个可用于读取指定缓冲区的读取器实例的受保护方法，供 <see cref="GetReader(TBuffer)"/> 调用。
        /// </summary>
        /// <param name="buffer">要为其创建或分配读取器的缓冲区实例，不能为空。</param>
        /// <returns>一个绑定到 <paramref name="buffer"/> 的 <see cref="AbstractBufferReader{T}"/> 实例。</returns>
        protected abstract AbstractBufferReader<T> GetReaderProtected(TBuffer buffer);
    }
}