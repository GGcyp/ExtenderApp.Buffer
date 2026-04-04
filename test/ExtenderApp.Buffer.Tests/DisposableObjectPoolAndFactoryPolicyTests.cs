namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 覆盖 <see cref="DisposableObjectPool{T}"/> 与 <see cref="FactoryPooledObjectPolicy{T}"/>。
/// </summary>
public class DisposableObjectPoolAndFactoryPolicyTests
{
    private sealed class TrackDispose : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 验证 <see cref="FactoryPooledObjectPolicy{T}.Create"/> 委托被调用以创建池内对象。
    /// </summary>
    [Fact]
    public void FactoryPooledObjectPolicy_Create_InvokesFactory()
    {
        var n = 0;
        var policy = new FactoryPooledObjectPolicy<int>(() => ++n);
        Assert.Equal(1, policy.Create());
        Assert.Equal(2, policy.Create());
    }

    /// <summary>
    /// 验证 <see cref="DisposableObjectPool{T}.Dispose"/> 后 <see cref="DisposableObjectPool{T}.Get"/> 抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    [Fact]
    public void DisposableObjectPool_AfterDispose_GetThrows()
    {
        var policy = new FactoryPooledObjectPolicy<TrackDispose>(() => new TrackDispose());
        var pool = new DisposableObjectPool<TrackDispose>(policy);
        pool.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pool.Get());
    }

    /// <summary>
    /// 验证 <see cref="DisposableObjectPool{T}.Dispose"/> 会释放队列中仍保留的 <see cref="IDisposable"/> 项。
    /// </summary>
    [Fact]
    public void DisposableObjectPool_Dispose_DisposesRetainedItems()
    {
        var policy = new DefaultPooledObjectPolicy<TrackDispose>();
        var pool = new DisposableObjectPool<TrackDispose>(policy, maximumRetained: 4);
        var a = new TrackDispose();
        pool.Release(a);
        pool.Dispose();
        Assert.Equal(1, a.DisposeCount);
    }
}
