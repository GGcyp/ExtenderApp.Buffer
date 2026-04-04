using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 通用的非托管指针封装结构体，支持 <see cref="IntPtr"/> 与原生指针类型之间的转换。 适用于与底层库（如 FFmpeg、OpenCV 等）交互时的指针管理和传递。
    /// </summary>
    /// <typeparam name="T">非托管类型，通常为结构体或指针类型。</typeparam>
    public unsafe struct NativeIntPtr<T> : IEquatable<NativeIntPtr<T>>, IDisposable
        where T : unmanaged
    {
        /// <summary>
        /// 空指针实例。
        /// </summary>
        public static NativeIntPtr<T> Empty => new(IntPtr.Zero);

        /// <summary>
        /// 指针的托管表示。
        /// </summary>
        public IntPtr Ptr { get; private set; }

        /// <summary>
        /// 获取或设置实际的非托管指针。
        /// </summary>
        public T* Value
        {
            get => (T*)Ptr;
            set => Ptr = (IntPtr)value;
        }

        /// <summary>
        /// 设置实际的非托管指针的指针
        /// </summary>
        public T** ValuePtr
        {
            set => Ptr = (IntPtr)(*value);
        }

        /// <summary>
        /// 指针是否为空。
        /// </summary>
        public bool IsEmpty => Ptr == IntPtr.Zero;

        /// <summary>
        /// 通过指针的指针初始化 <see cref="NativeIntPtr{T}"/>。
        /// </summary>
        /// <param name="value">指向指针的指针。</param>
        public NativeIntPtr(T** value) : this(value == null ? IntPtr.Zero : (IntPtr)(*value))
        {
        }

        /// <summary>
        /// 通过原生指针初始化 <see cref="NativeIntPtr{T}"/>。
        /// </summary>
        /// <param name="value">原生指针。</param>
        public NativeIntPtr(T* value) : this((IntPtr)value)
        {
        }

        /// <summary>
        /// 通过 <see cref="IntPtr"/> 初始化 <see cref="NativeIntPtr{T}"/>。
        /// </summary>
        /// <param name="value">托管指针。</param>
        public NativeIntPtr(IntPtr value)
        {
            Ptr = value;
        }

        /// <summary>
        /// 判断与另一个 <see cref="NativeIntPtr{T}"/> 是否相等。
        /// </summary>
        /// <param name="other">另一个指针封装实例。</param>
        /// <returns>是否相等。</returns>
        public bool Equals(NativeIntPtr<T> other)
        {
            return Ptr == other.Ptr;
        }

        /// <summary>
        /// 判断两个 <see cref="NativeIntPtr{T}"/> 是否相等。
        /// </summary>
        public static bool operator ==(NativeIntPtr<T> left, NativeIntPtr<T> right)
            => left.Equals(right);

        /// <summary>
        /// 判断两个 <see cref="NativeIntPtr{T}"/> 是否不相等。
        /// </summary>
        public static bool operator !=(NativeIntPtr<T> left, NativeIntPtr<T> right)
            => !left.Equals(right);

        /// <summary>
        /// 将指针解引用为对目标值的可读写引用。 这允许您直接修改指针指向的内存。
        /// </summary>
        /// <returns>对 <typeparamref name="T"/> 类型值的引用。</returns>
        /// <exception cref="NullReferenceException">当指针为空时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef()
        {
            if (IsEmpty)
                ThrowIfNull();
            return ref *Value;
        }

        /// <summary>
        /// 将指针解引用为对目标值的只读引用。
        /// </summary>
        /// <returns>对 <typeparamref name="T"/> 类型值的只读引用。</returns>
        /// <exception cref="NullReferenceException">当指针为空时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T AsRefReadOnly()
        {
            if (IsEmpty)
                ThrowIfNull();
            return ref *Value;
        }

        /// <summary>
        /// 获取原生指针。
        /// </summary>
        /// <returns>返回原生指针</returns>
        public void* GetPointer()
        {
            if (IsEmpty)
                ThrowIfNull();
            return Value;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is NativeIntPtr<T> && Equals((NativeIntPtr<T>)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Ptr.GetHashCode();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Ptr = IntPtr.Zero;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Ptr.ToString();
        }

        private static void ThrowIfNull()
        {
            throw new NullReferenceException("当前指针为空");
        }

        #region Implicit

        public static implicit operator T*(NativeIntPtr<T> value) => value.Value;

        public static implicit operator NativeIntPtr<T>(T* value) => new NativeIntPtr<T>(value);

        public static implicit operator NativeIntPtr<T>(T** value) => new NativeIntPtr<T>(value);

        public static implicit operator nint(NativeIntPtr<T> value) => value.Ptr;

        public static implicit operator NativeIntPtr<T>(nint value) => new NativeIntPtr<T>(value);

        #endregion Implicit

        #region Explicit

        public static explicit operator long(NativeIntPtr<T> value) => value.Ptr;

        public static explicit operator NativeIntPtr<T>(long value) => new NativeIntPtr<T>((IntPtr)value);

        public static explicit operator int(NativeIntPtr<T> value) => (int)value.Ptr;

        public static explicit operator NativeIntPtr<T>(int value) => new NativeIntPtr<T>(value);

        public static explicit operator void*(NativeIntPtr<T> value) => (void*)value.Ptr;

        public static explicit operator NativeIntPtr<T>(void* value) => new NativeIntPtr<T>((IntPtr)value);

        #endregion Explicit
    }
}