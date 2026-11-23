using Microsoft.Extensions.Logging.Abstractions;
using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;
using ProcX.Application.Security;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeSnapshots _snapshots = new();
    private readonly FakeController _controller = new();
    private readonly FakeShell _shell = new();
    private readonly FakeElevation _elevation = new();
    private readonly ProcessListViewModel _list = new(new NullEnricher());

    private MainWindowViewModel CreateViewModel() => new(
        _list,
        new ProcessTerminator(_snapshots, _controller),
        new ProcessMonitor(_snapshots, new FixedSystemInformation(), TimeProvider.System, NullLogger<ProcessMonitor>.Instance),
        _elevation,
        _shell,
        new ProcessDetailsViewModel(new FakeDetailsProvider(), _list) { IsActive = false },
        _controller);

    private ProcessSample AddProcess(int pid, string name, int parentPid = 0, int createdAfterSeconds = 0)
    {
        var sample = new ProcessSample
        {
            Pid = pid,
            ParentPid = parentPid,
            Name = name,
            CreateTime = BaseTime.AddSeconds(createdAfterSeconds),
        };
        _snapshots.Processes.Add(sample);
        _list.Apply(Rows.Delta(added: [new ProcessMetrics(sample, 0)]));
        return sample;
    }

    private void Select(ProcessSample sample) => _list.SelectedRow = _list.Rows.Single(r => r.Key == sample.Key);

    [Fact]
    public async Task EndTask_RegularProcess_TerminatesWithoutConfirmation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var notepad = AddProcess(100, "notepad.exe");
        Select(notepad);

        // Act
        await viewModel.EndTaskCommand.ExecuteAsync(null);

        // Assert
        _controller.Terminated.Should().Equal(notepad.Key);
        _shell.Confirmations.Should().BeEmpty();
        _shell.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task EndTask_CriticalProcessAndUserCancels_DoesNotTerminate()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(600, "csrss.exe"));
        _shell.ConfirmResult = false;

        // Act
        await viewModel.EndTaskCommand.ExecuteAsync(null);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("critical");
        _controller.Terminated.Should().BeEmpty();
    }

    [Fact]
    public async Task EndTask_CriticalProcessAndUserConfirms_Terminates()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var csrss = AddProcess(600, "csrss.exe");
        Select(csrss);
        _shell.ConfirmResult = true;

        // Act
        await viewModel.EndTaskCommand.ExecuteAsync(null);

        // Assert
        _controller.Terminated.Should().Equal(csrss.Key);
    }

    [Fact]
    public async Task EndTask_AccessDeniedWhileNotElevated_OffersRestartAsAdministrator()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var service = AddProcess(200, "service.exe");
        Select(service);
        _controller.Denied.Add(service.Key);
        _shell.ConfirmResult = true;

        // Act
        await viewModel.EndTaskCommand.ExecuteAsync(null);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("administrator");
        _elevation.StartCount.Should().Be(1);
        _shell.ShutdownRequested.Should().BeTrue();
    }

    [Fact]
    public async Task EndTask_AccessDeniedWhileElevated_ShowsMessage()
    {
        // Arrange
        _elevation.IsElevated = true;
        var viewModel = CreateViewModel();
        var service = AddProcess(200, "service.exe");
        Select(service);
        _controller.Denied.Add(service.Key);

        // Act
        await viewModel.EndTaskCommand.ExecuteAsync(null);

        // Assert
        _shell.Messages.Should().ContainSingle().Which.Should().Contain("service.exe").And.Contain("access denied");
        _elevation.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task EndProcessTree_UserConfirms_TerminatesDescendantsFirst()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var parent = AddProcess(100, "parent.exe", createdAfterSeconds: 1);
        var child = AddProcess(200, "child.exe", parentPid: 100, createdAfterSeconds: 2);
        Select(parent);
        _shell.ConfirmResult = true;

        // Act
        await viewModel.EndProcessTreeCommand.ExecuteAsync(null);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("2 process(es)");
        _controller.Terminated.Should().Equal(child.Key, parent.Key);
    }

    [Fact]
    public async Task EndProcessTree_UserCancels_DoesNotTerminate()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(100, "parent.exe"));
        _shell.ConfirmResult = false;

        // Act
        await viewModel.EndProcessTreeCommand.ExecuteAsync(null);

        // Assert
        _controller.Terminated.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcessPriority.High)]
    [InlineData(ProcessPriority.BelowNormal)]
    public async Task SetPriority_RegularPriority_AppliesWithoutConfirmation(ProcessPriority priority)
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = AddProcess(100, "app.exe");
        Select(app);

        // Act
        await viewModel.SetPriorityCommand.ExecuteAsync(priority);

        // Assert
        _controller.PriorityChanges.Should().Equal((app.Key, priority));
        _shell.Confirmations.Should().BeEmpty();
    }

    [Fact]
    public async Task SetPriority_RealtimeAndUserCancels_DoesNotApply()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(100, "app.exe"));
        _shell.ConfirmResult = false;

        // Act
        await viewModel.SetPriorityCommand.ExecuteAsync(ProcessPriority.Realtime);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("administrator");
        _controller.PriorityChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task SetPriority_AccessDeniedWhileNotElevated_OffersRestartAsAdministrator()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(100, "service.exe"));
        _controller.OperationStatus = ProcessOperationStatus.AccessDenied;
        _shell.ConfirmResult = true;

        // Act
        await viewModel.SetPriorityCommand.ExecuteAsync(ProcessPriority.High);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("administrator");
        _elevation.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task SetAffinity_UserPicksSubset_AppliesMask()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = AddProcess(100, "app.exe");
        Select(app);
        _shell.PickedAffinity = AffinityMask.FromProcessors([0, 2]);

        // Act
        await viewModel.SetAffinityCommand.ExecuteAsync(null);

        // Assert
        _controller.AffinityChanges.Should().Equal((app.Key, AffinityMask.FromProcessors([0, 2])));
    }

    [Fact]
    public async Task SetAffinity_UserCancelsOrKeepsSameMask_DoesNotApply()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(100, "app.exe"));

        // Act
        _shell.PickedAffinity = null;
        await viewModel.SetAffinityCommand.ExecuteAsync(null);
        _shell.PickedAffinity = AffinityMask.All(4);
        await viewModel.SetAffinityCommand.ExecuteAsync(null);

        // Assert
        _controller.AffinityChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAffinity_UnsupportedSystem_ShowsMessageWithoutPicker()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(100, "app.exe"));
        _controller.OperationStatus = ProcessOperationStatus.Unsupported;
        _shell.PickedAffinity = AffinityMask.All(1);

        // Act
        await viewModel.SetAffinityCommand.ExecuteAsync(null);

        // Assert
        _shell.Messages.Should().ContainSingle().Which.Should().Contain("processor group");
        _controller.AffinityChanges.Should().BeEmpty();
    }

    [Fact]
    public void OpenSettings_Executed_ShowsSettingsWindow()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenSettingsCommand.Execute(null);

        // Assert
        _shell.SettingsShown.Should().Be(1);
    }

    [Fact]
    public void Commands_NoSelection_CannotExecute()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.EndTaskCommand.CanExecute(null).Should().BeFalse();
        viewModel.EndProcessTreeCommand.CanExecute(null).Should().BeFalse();
        viewModel.CopyPidCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void OpenFileLocation_PathArrivesAfterSelection_BecomesExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var process = AddProcess(100, "app.exe");
        Select(process);
        var before = viewModel.OpenFileLocationCommand.CanExecute(null);

        // Act
        _list.SelectedRow!.ApplyStaticInfo(new ProcessStaticInfo(@"C:\app.exe", "user", null, false), null);
        viewModel.OpenFileLocationCommand.Execute(null);

        // Assert
        before.Should().BeFalse();
        _shell.OpenedLocations.Should().Equal(@"C:\app.exe");
    }

    [Fact]
    public void CopyPid_SelectedProcess_CopiesPid()
    {
        // Arrange
        var viewModel = CreateViewModel();
        Select(AddProcess(1234, "app.exe"));

        // Act
        viewModel.CopyPidCommand.Execute(null);

        // Assert
        _shell.Clipboard.Should().Be("1234");
    }

    [Theory]
    [InlineData(false, "ProcX", true)]
    [InlineData(true, "ProcX (Administrator)", false)]
    public void Title_ElevationState_ReflectsPrivileges(bool elevated, string expectedTitle, bool canElevate)
    {
        // Arrange
        _elevation.IsElevated = elevated;

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Title.Should().Be(expectedTitle);
        viewModel.CanElevate.Should().Be(canElevate);
        viewModel.RestartAsAdministratorCommand.CanExecute(null).Should().Be(canElevate);
    }

    [Fact]
    public void RestartAsAdministrator_UserDeclinesUac_KeepsRunning()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _elevation.StartResult = false;

        // Act
        viewModel.RestartAsAdministratorCommand.Execute(null);

        // Assert
        _shell.ShutdownRequested.Should().BeFalse();
    }

    private sealed class FakeSnapshots : IProcessSnapshotProvider
    {
        public List<ProcessSample> Processes { get; } = [];

        public ProcessSnapshot Capture() => new(BaseTime, [.. Processes]);
    }

    private sealed class FakeController : IProcessController
    {
        public List<ProcessKey> Terminated { get; } = [];

        public HashSet<ProcessKey> Denied { get; } = [];

        public TerminationResult Terminate(ProcessKey key)
        {
            if (Denied.Contains(key))
            {
                return new TerminationResult(key, TerminationStatus.AccessDenied, 5);
            }

            Terminated.Add(key);
            return new TerminationResult(key, TerminationStatus.Succeeded);
        }

        public List<(ProcessKey Key, ProcessPriority Priority)> PriorityChanges { get; } = [];

        public List<(ProcessKey Key, AffinityMask Mask)> AffinityChanges { get; } = [];

        public ProcessOperationStatus OperationStatus { get; set; } = ProcessOperationStatus.Succeeded;

        public AffinityInfo Affinity { get; set; } = new(AffinityMask.All(4), AffinityMask.All(4));

        public ProcessOperationResult<bool> SetPriority(ProcessKey key, ProcessPriority priority)
        {
            PriorityChanges.Add((key, priority));
            return OperationStatus == ProcessOperationStatus.Succeeded
                ? ProcessOperationResult.Ok(true)
                : ProcessOperationResult.Fail<bool>(OperationStatus, 5);
        }

        public ProcessOperationResult<AffinityInfo> GetAffinity(ProcessKey key) =>
            OperationStatus == ProcessOperationStatus.Succeeded
                ? ProcessOperationResult.Ok(Affinity)
                : ProcessOperationResult.Fail<AffinityInfo>(OperationStatus, 5);

        public ProcessOperationResult<bool> SetAffinity(ProcessKey key, AffinityMask mask)
        {
            AffinityChanges.Add((key, mask));
            return ProcessOperationResult.Ok(true);
        }
    }

    private sealed class FakeShell : IShellInteraction
    {
        public bool ConfirmResult { get; set; }

        public List<string> Confirmations { get; } = [];

        public List<string> Messages { get; } = [];

        public List<string> OpenedLocations { get; } = [];

        public string? Clipboard { get; private set; }

        public bool ShutdownRequested { get; private set; }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            Confirmations.Add($"{title}\n{message}");
            return Task.FromResult(ConfirmResult);
        }

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add($"{title}\n{message}");
            return Task.CompletedTask;
        }

        public void OpenFileLocation(string path) => OpenedLocations.Add(path);

        public AffinityMask? PickedAffinity { get; set; }

        public Task<AffinityMask?> PickAffinityAsync(string processName, AffinityMask current, AffinityMask available) =>
            Task.FromResult(PickedAffinity);

        public int SettingsShown { get; private set; }

        public void ShowSettings() => SettingsShown++;

        public void CopyToClipboard(string text) => Clipboard = text;

        public void Shutdown() => ShutdownRequested = true;
    }

    private sealed class FakeElevation : IElevationService
    {
        public bool IsElevated { get; set; }

        public bool StartResult { get; set; } = true;

        public int StartCount { get; private set; }

        public bool TryStartElevated(string arguments = "")
        {
            StartCount++;
            return StartResult;
        }

        public int? RunElevatedAndWait(string arguments) => throw new NotSupportedException();
    }

    private sealed class FixedSystemInformation : ISystemInformation
    {
        public int LogicalProcessorCount => 4;
    }

    private sealed class NullEnricher : IProcessRowEnricher
    {
        public void Enqueue(ProcessRowViewModel row)
        {
        }
    }
}
