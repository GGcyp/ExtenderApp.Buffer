# ExtenderApp.Buffer 使用说明

面向 .NET 8 的缓冲抽象库，提供单段内存块（`MemoryBlock<T>`）、多段序列（`SequenceBuffer<T>`）、与 `IBufferWriter<T>` / `ReadOnlySequence<T>` 一致的读写模型，并内置读取器、对象池与多种内存提供者。本库依赖同解决方案中的 **ExtenderApp.Common**，引用本库时请确保该工程可被编译与解析。

## 1. 引用与命名空间

在应用或类库项目中添加对本仓库 `src/ExtenderApp.Buffer.csproj` 的项目引用（或通过你本地的包/解决方案引用方式接入）。

常用命名空间：

- `ExtenderApp.Buffer`：`AbstractBuffer<T>`、`MemoryBlock<T>`、`SequenceBuffer<T>`、静态工厂 `AbstractBuffer` 等。
- `ExtenderApp.Buffer.Reader`：`MemoryBlockReader<T>`、`SequenceBufferReader<T>` 等（多数场景下通过扩展方法 `GetReader()` 即可）。

## 2. 获取缓冲区

### 2.1 单段：`MemoryBlock<T>`

典型入口为静态方法 `MemoryBlock<T>.GetBuffer(...)`，根据数据来源选择重载：

| 场景 | 用法要点 |
|------|----------|
| 向池申请可写块（默认 `ArrayPool` 提供者） | `MemoryBlock<byte>.GetBuffer()` 或 `GetBuffer(initialCapacity)` |
| 从 `ReadOnlySpan` / `Span` 拷贝内容到新块 | `GetBuffer(span)` |
| 包装已有 `Memory<T>` / `ReadOnlyMemory<T>`（一般不拷贝） | `GetBuffer(memory)`，需保证底层内存在使用期内有效 |
| 包装数组或 `ArraySegment`（不拷贝数组） | `GetBuffer(array)`、`GetBuffer(array, start, length)`、`GetBuffer(segment)` |

也可使用静态类 **`AbstractBuffer`** 的统一工厂，例如：`AbstractBuffer.GetBlock<byte>()`、`AbstractBuffer.GetBlock<byte>(size)`、`AbstractBuffer.GetBlock<byte>(readOnlyMemory)` 等，语义上对应 `MemoryBlock` 路径。

### 2.2 多段：`SequenceBuffer<T>`

适用于多段累积、与 `ReadOnlySequence<T>` 交互的场景，例如：`AbstractBuffer.GetSequence<byte>(readOnlySequence)` 或 `SequenceBuffer<T>.GetBuffer(...)`（参见 XML 文档注释中的各重载）。

## 3. 写入与提交

`AbstractBuffer<T>` 实现 `IBufferWriter<T>`，典型模式：

1. `GetMemory(sizeHint)` / `GetSpan(sizeHint)` 取得可写区域；
2. 写入后调用 `Advance(count)` 提交本次写入长度；
3. 或使用便捷方法：`Write(ReadOnlySpan<T>)`、`Write(ReadOnlySequence<T>)` 等（内部会处理 `GetSpan` + `Advance`）。

只读视图使用 **`CommittedSequence`**（`ReadOnlySequence<T>`），表示当前已提交数据。

其他常用 API：

- **`Clear()`**：逻辑清空已提交内容并重置写入状态（池化场景下可能保留底层数组以便复用）。
- **`FreezeWrite()` / `UnfreezeWrite()`**：嵌套冻结写入，冻结期间写入类操作会按设计抛出异常；读取器绑定缓冲区时会冻结写入，避免读写并发破坏数据。

## 4. 释放与生命周期

缓冲区在仍被“冻结”（存在未配对 `Unfreeze` 的 `Freeze`，或仍处于 Pin/读取器占用等状态）时，**不能**强行 `Release()`，否则会抛出 `InvalidOperationException`。

推荐约定：

- 需要归还池或释放底层资源时，优先在确认无额外冻结后调用 **`Release()`**；若不确定是否仍被引用，可使用 **`TryRelease()`**（失败表示仍被冻结，需稍后重试或先解除引用）。
- `AbstractBuffer<T>` 继承链支持 **`Dispose()`**，与资源释放模式一致；具体项目可统一采用 `using` / `try/finally` 调用 `Dispose()` 或显式 `Release()`/`TryRelease()`，与现有测试代码风格保持一致即可。

使用 **`Pin` / `Unpin`** 时须成对调用，避免固定句柄泄漏。

## 5. 读取器

对任意 **`AbstractBuffer<T>`** 可调用扩展方法 **`GetReader()`**（定义于 `AbstractBufferReaderExtensions`），将按实际类型分派到 `MemoryBlockReader` 或 `SequenceBufferReader` 的提供者（通常带对象池）。

读取器构造时会 **`Freeze`** 缓冲区并 **`FreezeWrite`**，使用结束后必须释放读取器以解冻缓冲，例如：

```csharp
var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3 });
var reader = block.GetReader();
try
{
    // 使用 reader.UnreadSequence、Read、Advance、TryPeek 等
}
finally
{
    reader.Release(); // 或依赖 Dispose 路径释放托管引用（见 AbstractBufferReader 实现）
}
```

值类型读取场景还可使用 **`ValueMemoryBlockReader<T>`**、**`ValueSequenceBufferReader<T>`** 等结构体实现，按 API 文档选择以降低分配。

## 6. 流与其它组件

- **`AbstractBufferStream`**：在缓冲与 `Stream` 语义之间桥接（内部会获取读取器等，注意与缓冲生命周期的配合）。
- **`SpanReader` / `SpanWriter`**：基于 `Span<T>` 的轻量读写。
- **对象池、ValueCache、Native 内存**等：按命名空间浏览 `ObjectPools`、`ValueCaches`、`NativeIntPtrs`，以 XML 注释为准。

## 7. 构建、测试与基准

```text
dotnet build ExtenderApp.Buffer.slnx
dotnet test test/ExtenderApp.Buffer.Tests
dotnet run -c Release --project benchmark/ExtenderApp.Buffer.Benchmarks
```

基准项目支持 BenchmarkDotNet 的 **`--filter`** 等参数；正式对比性能时请在 **Release** 下运行并视需要调整迭代次数。

## 8. 许可

本项目采用 MIT 许可，详见仓库根目录 **`LICENSE.txt`**（著作权人为 GGGcyp）。
