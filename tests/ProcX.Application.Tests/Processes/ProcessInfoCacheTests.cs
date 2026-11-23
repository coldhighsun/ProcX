using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.Application.Tests.Processes;

public class ProcessInfoCacheTests
{
    private readonly CountingProvider _provider = new();
    private readonly ProcessInfoCache _cache;

    public ProcessInfoCacheTests()
    {
        _cache = new ProcessInfoCache(_provider);
    }

    [Fact]
    public void Get_SameKeyTwice_QueriesProviderOnce()
    {
        // Arrange
        var key = Samples.Process(1).Key;

        // Act
        var first = _cache.Get(key);
        var second = _cache.Get(key);

        // Assert
        second.Should().BeSameAs(first);
        _provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Get_ConcurrentCallers_QueriesProviderOnce()
    {
        // Arrange
        var key = Samples.Process(1).Key;

        // Act
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => _cache.Get(key), TestContext.Current.CancellationToken)));

        // Assert
        _provider.Calls.Should().Be(1);
    }

    [Fact]
    public void Evict_ExitedProcess_QueriesProviderAgainOnNextGet()
    {
        // Arrange
        var key = Samples.Process(1).Key;
        _cache.Get(key);

        // Act
        _cache.Evict([key]);
        _cache.Get(key);

        // Assert
        _provider.Calls.Should().Be(2);
        _cache.Count.Should().Be(1);
    }

    private sealed class CountingProvider : IProcessInfoProvider
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public ProcessStaticInfo GetStaticInfo(ProcessKey key)
        {
            Interlocked.Increment(ref _calls);
            Thread.Sleep(10);
            return new ProcessStaticInfo($"C:\\{key.Pid}.exe", "user", null, IsAccessDenied: false);
        }
    }
}
