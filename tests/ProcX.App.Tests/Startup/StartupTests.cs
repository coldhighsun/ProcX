using System.IO;
using ProcX.App.Startup;
using ProcX.Application.Replacement;

namespace ProcX.App.Tests.Startup;

public class StartupTests
{
    [Theory]
    [InlineData(new string[0], StartupMode.Normal)]
    [InlineData(new[] { "--register-replacement" }, StartupMode.RegisterReplacement)]
    [InlineData(new[] { "--UNREGISTER-REPLACEMENT" }, StartupMode.UnregisterReplacement)]
    [InlineData(new[] { @"C:\WINDOWS\system32\taskmgr.exe" }, StartupMode.Normal)]
    [InlineData(new[] { @"C:\WINDOWS\system32\taskmgr.exe", "/7" }, StartupMode.Normal)]
    [InlineData(new[] { "--unknown", "--register-replacement" }, StartupMode.RegisterReplacement)]
    public void Parse_Arguments_ReturnsMode(string[] args, StartupMode expected)
    {
        // Act
        var options = StartupOptions.Parse(args);

        // Assert
        options.Mode.Should().Be(expected);
    }

    [Fact]
    public void Execute_Register_EnablesExecutableAndReturnsSuccess()
    {
        // Arrange
        var service = new RecordingService();

        // Act
        var code = ReplacementCommand.Execute(StartupMode.RegisterReplacement, service, @"C:\p\ProcX.exe");

        // Assert
        code.Should().Be(ReplacementCommand.Success);
        service.Calls.Should().Equal(@"Enable C:\p\ProcX.exe");
    }

    [Fact]
    public void Execute_Unregister_DisablesAndReturnsSuccess()
    {
        // Arrange
        var service = new RecordingService();

        // Act
        var code = ReplacementCommand.Execute(StartupMode.UnregisterReplacement, service, "ignored");

        // Assert
        code.Should().Be(ReplacementCommand.Success);
        service.Calls.Should().Equal("Disable");
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), ReplacementCommand.AccessDenied)]
    [InlineData(typeof(IOException), ReplacementCommand.Failed)]
    [InlineData(typeof(ArgumentException), ReplacementCommand.Failed)]
    public void Execute_ServiceThrows_MapsToExitCode(Type exceptionType, int expected)
    {
        // Arrange
        var service = new RecordingService { Failure = (Exception)Activator.CreateInstance(exceptionType)! };

        // Act
        var code = ReplacementCommand.Execute(StartupMode.RegisterReplacement, service, @"C:\p\ProcX.exe");

        // Assert
        code.Should().Be(expected);
    }

    [Fact]
    public void Execute_NormalMode_Throws()
    {
        // Act
        var act = () => ReplacementCommand.Execute(StartupMode.Normal, new RecordingService(), "x");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class RecordingService : ITaskManagerReplacementService
    {
        public List<string> Calls { get; } = [];

        public Exception? Failure { get; set; }

        public ReplacementStatus GetStatus(string currentExecutable) => new(ReplacementState.Disabled, null);

        public void Enable(string executable)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Calls.Add($"Enable {executable}");
        }

        public void Disable()
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Calls.Add("Disable");
        }
    }
}
