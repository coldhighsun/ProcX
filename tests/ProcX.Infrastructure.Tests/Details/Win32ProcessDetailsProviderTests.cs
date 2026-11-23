using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ProcX.Application.Details;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Details;
using ProcX.Infrastructure.Processes;

namespace ProcX.Infrastructure.Tests.Details;

public sealed class Win32ProcessDetailsProviderTests : IDisposable
{
    private readonly Win32ProcessDetailsProvider _provider = new();
    private readonly NtProcessSnapshotProvider _snapshots = new();

    public void Dispose() => _snapshots.Dispose();

    private ProcessKey KeyOf(int pid) => _snapshots.Capture().Processes.Single(p => p.Pid == pid).Key;

    private ProcessKey CurrentKey => KeyOf(Environment.ProcessId);

    [Fact]
    public void GetGeneral_CurrentProcess_ReturnsCommandLineArchitectureAndToken()
    {
        // Act
        var result = _provider.GetGeneral(CurrentKey, imagePath: null);

        // Assert
        result.Status.Should().Be(DetailsStatus.Ok);
        var info = result.Value!;
        info.CommandLine.Should().Contain(Path.GetFileNameWithoutExtension(Environment.ProcessPath!));
        info.Architecture.Should().Be(RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => ProcessArchitecture.Arm64,
            Architecture.X64 => ProcessArchitecture.X64,
            _ => ProcessArchitecture.X86,
        });
        info.IsElevated.Should().Be(Environment.IsPrivilegedProcess);
        info.IntegrityLevel.Should().Be(Environment.IsPrivilegedProcess ? IntegrityLevel.High : IntegrityLevel.Medium);
    }

    [Fact]
    public async Task GetGeneral_ChildProcess_ReturnsItsArguments()
    {
        // Arrange
        using var child = Process.Start(new ProcessStartInfo("ping.exe", "-n 30 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
        try
        {
            // Act
            var result = _provider.GetGeneral(KeyOf(child.Id), imagePath: null);

            // Assert
            result.Value!.CommandLine.Should().Contain("-n 30 127.0.0.1");
            result.Value.CompanyName.Should().Contain("Microsoft");
            result.Value.FileVersion.Should().NotBeNullOrEmpty();
        }
        finally
        {
            child.Kill();
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void GetGeneral_StaleKey_ReturnsNotFound()
    {
        // Arrange
        var stale = CurrentKey with { CreateTime = DateTimeOffset.UnixEpoch };

        // Act
        var result = _provider.GetGeneral(stale, imagePath: null);

        // Assert
        result.Status.Should().Be(DetailsStatus.NotFound);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void GetThreads_CurrentProcess_MatchesSystemDiagnosticsAndResolvesStartSymbols()
    {
        // Arrange
        using var current = Process.GetCurrentProcess();
        var expectedIds = current.Threads.Cast<ProcessThread>().Select(static t => t.Id).ToHashSet();

        // Act
        var result = _provider.GetThreads(CurrentKey);

        // Assert
        result.Status.Should().Be(DetailsStatus.Ok);
        var threads = result.Value!;
        threads.Select(static t => t.ThreadId).Should().BeInAscendingOrder();
        threads.Select(static t => t.ThreadId).Intersect(expectedIds).Should().HaveCountGreaterThan(expectedIds.Count / 2);
        threads.Should().Contain(static t => t.StartSymbol != null && t.StartSymbol.Contains('+'));
        threads.Where(static t => t.State == ThreadSchedulingState.Waiting).Should().OnlyContain(static t => t.WaitReason != null);
    }

    [Fact]
    public void GetThreads_StaleKey_ReturnsNotFound()
    {
        // Act
        var result = _provider.GetThreads(CurrentKey with { CreateTime = DateTimeOffset.UnixEpoch });

        // Assert
        result.Status.Should().Be(DetailsStatus.NotFound);
    }

    [Fact]
    public void GetModules_CurrentProcess_ContainsSystemDllsWithVersionInfo()
    {
        // Act
        var result = _provider.GetModules(CurrentKey);

        // Assert
        result.Status.Should().Be(DetailsStatus.Ok);
        var modules = result.Value!;
        modules.Select(static m => m.BaseAddress).Should().BeInAscendingOrder();
        var ntdll = modules.Should().ContainSingle(static m => m.Name.Equals("ntdll.dll", StringComparison.OrdinalIgnoreCase)).Subject;
        ntdll.Path.Should().StartWithEquivalentOf(Environment.SystemDirectory);
        ntdll.Size.Should().BePositive();
        ntdll.CompanyName.Should().Contain("Microsoft");
        modules.Should().Contain(static m => m.Name.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetModules_SystemProcess_ReturnsNoModules()
    {
        // Act
        var result = _provider.GetModules(KeyOf(4));

        // Assert: denied for normal users; an elevated caller may open it but the kernel has no user-mode modules.
        (result.Status != DetailsStatus.Ok || result.Value!.Count == 0).Should().BeTrue();
    }

    [Fact]
    public void GetHandles_CurrentProcess_NamesFilesKeysEventsAndProcesses()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"procx-handle-test-{Guid.NewGuid():N}.tmp");
        var eventName = $"procx-test-{Guid.NewGuid():N}";
        using var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        using var key = Registry.CurrentUser.OpenSubKey("Software")!;
        using var namedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        using var self = Process.GetCurrentProcess();
        _ = self.Handle;

        // Act
        var result = _provider.GetHandles(CurrentKey, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(DetailsStatus.Ok);
        var handles = result.Value!;
        handles.Select(static h => h.Handle).Should().BeInAscendingOrder();
        handles.Should().Contain(h => h.TypeName == "File" && string.Equals(h.Name, path, StringComparison.OrdinalIgnoreCase));
        handles.Should().Contain(h => h.TypeName == "Key" && h.Name != null && h.Name.StartsWith(@"HKU\", StringComparison.Ordinal) && h.Name.EndsWith(@"\Software", StringComparison.OrdinalIgnoreCase));
        handles.Should().Contain(h => h.TypeName == "Event" && h.Name != null && h.Name.EndsWith(eventName, StringComparison.Ordinal));
        handles.Should().Contain(h => h.TypeName == "Process" && h.Name == $"PID {Environment.ProcessId}");
        handles.Should().Contain(h => h.TypeName == "Thread" && h.Name != null && h.Name.StartsWith("TID ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetHandles_SynchronousPipeWithPendingRead_DoesNotHang()
    {
        // Arrange: a synchronous pipe with a blocked read; querying its name blocks inside the kernel.
        var pipeName = $"procx-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
        var connect = Task.Run(server.WaitForConnection, TestContext.Current.CancellationToken);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await connect;
        var blockedRead = new Thread(() => _ = server.Read(new byte[1], 0, 1)) { IsBackground = true };
        blockedRead.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        try
        {
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await Task.Run(() => _provider.GetHandles(CurrentKey, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            stopwatch.Stop();

            // Assert
            result.Status.Should().Be(DetailsStatus.Ok);
            result.Value.Should().Contain(static h => h.TypeName == "File");
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        }
        finally
        {
            // Release the blocked read.
            client.WriteByte(1);
            client.Flush();
            blockedRead.Join(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void GetHandles_Cancelled_Throws()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _provider.GetHandles(CurrentKey, cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }
}
