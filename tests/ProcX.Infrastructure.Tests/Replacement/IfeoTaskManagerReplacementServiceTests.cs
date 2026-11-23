using Microsoft.Win32;
using ProcX.Application.Replacement;
using ProcX.Infrastructure.Replacement;

namespace ProcX.Infrastructure.Tests.Replacement;

/// <summary>
/// Tests against a scratch key under HKCU; the real Image File Execution Options in HKLM are never touched.
/// </summary>
public sealed class IfeoTaskManagerReplacementServiceTests : IDisposable
{
    private readonly string _optionsPath = $@"Software\ProcXTests\{Guid.NewGuid():N}\Image File Execution Options";
    private readonly string _exe = Environment.ProcessPath!;
    private readonly IfeoTaskManagerReplacementService _service;

    public IfeoTaskManagerReplacementServiceTests()
    {
        _service = new IfeoTaskManagerReplacementService(Registry.CurrentUser, _optionsPath);
    }

    public void Dispose()
    {
        var parent = _optionsPath[..(_optionsPath.IndexOf(@"\Image File", StringComparison.Ordinal))];
        Registry.CurrentUser.DeleteSubKeyTree(parent, throwOnMissingSubKey: false);

        // Remove the shared scratch root once the last test has cleaned up after itself.
        using (var root = Registry.CurrentUser.OpenSubKey(@"Software\ProcXTests"))
        {
            if (root is null || root.SubKeyCount > 0)
            {
                return;
            }
        }

        Registry.CurrentUser.DeleteSubKey(@"Software\ProcXTests", throwOnMissingSubKey: false);
    }

    private void SetDebugger(string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{_optionsPath}\taskmgr.exe");
        key.SetValue("Debugger", value);
    }

    [Fact]
    public void GetStatus_NothingConfigured_ReturnsDisabled()
    {
        // Act
        var status = _service.GetStatus(_exe);

        // Assert
        status.State.Should().Be(ReplacementState.Disabled);
        status.TargetPath.Should().BeNull();
    }

    [Fact]
    public void Enable_Executable_WritesQuotedDebuggerAndReportsEnabled()
    {
        // Act
        _service.Enable(_exe);

        // Assert
        using var key = Registry.CurrentUser.OpenSubKey($@"{_optionsPath}\taskmgr.exe")!;
        key.GetValue("Debugger").Should().Be($"\"{_exe}\"");
        _service.GetStatus(_exe).Should().Be(new ReplacementStatus(ReplacementState.Enabled, _exe));
    }

    [Fact]
    public void Enable_MissingExecutable_ThrowsAndWritesNothing()
    {
        // Act
        var act = () => _service.Enable(@"C:\does\not\exist.exe");

        // Assert
        act.Should().Throw<ArgumentException>();
        _service.GetStatus(_exe).State.Should().Be(ReplacementState.Disabled);
    }

    [Fact]
    public void Disable_AfterEnable_RemovesValueAndEmptyKey()
    {
        // Arrange
        _service.Enable(_exe);

        // Act
        _service.Disable();

        // Assert
        _service.GetStatus(_exe).State.Should().Be(ReplacementState.Disabled);
        using var key = Registry.CurrentUser.OpenSubKey($@"{_optionsPath}\taskmgr.exe");
        key.Should().BeNull();
    }

    [Fact]
    public void Disable_OtherValuesOnKey_KeepsKeyAndOtherValues()
    {
        // Arrange
        _service.Enable(_exe);
        using (var key = Registry.CurrentUser.OpenSubKey($@"{_optionsPath}\taskmgr.exe", writable: true)!)
        {
            key.SetValue("GlobalFlag", 0x200);
        }

        // Act
        _service.Disable();

        // Assert
        using var after = Registry.CurrentUser.OpenSubKey($@"{_optionsPath}\taskmgr.exe")!;
        after.GetValue("GlobalFlag").Should().Be(0x200);
        after.GetValue("Debugger").Should().BeNull();
    }

    [Fact]
    public void Disable_NothingConfigured_DoesNotThrow()
    {
        // Act
        var act = _service.Disable;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetStatus_DebuggerPointsToOtherExistingFile_ReturnsEnabledForOther()
    {
        // Arrange
        var other = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        SetDebugger($"\"{other}\"");

        // Act
        var status = _service.GetStatus(_exe);

        // Assert
        status.State.Should().Be(ReplacementState.EnabledForOther);
        status.TargetPath.Should().Be(other);
    }

    [Fact]
    public void GetStatus_DebuggerPointsToMissingFile_ReturnsBroken()
    {
        // Arrange
        SetDebugger(@"""C:\gone\ProcX.exe""");

        // Act
        var status = _service.GetStatus(_exe);

        // Assert
        status.State.Should().Be(ReplacementState.Broken);
    }

    [Fact]
    public void GetStatus_PathDiffersOnlyByCase_ReturnsEnabled()
    {
        // Arrange
        SetDebugger($"\"{_exe.ToUpperInvariant()}\"");

        // Act
        var status = _service.GetStatus(_exe);

        // Assert
        status.State.Should().Be(ReplacementState.Enabled);
    }

    [Theory]
    [InlineData(@"""C:\Program Files\ProcX\ProcX.exe""", @"C:\Program Files\ProcX\ProcX.exe")]
    [InlineData(@"""C:\Tools\a.exe"" /flag value", @"C:\Tools\a.exe")]
    [InlineData(@"C:\Tools\a.exe /flag", @"C:\Tools\a.exe")]
    [InlineData(@"C:\Program Files\x\a.exe -z", @"C:\Program Files\x\a.exe")]
    [InlineData("tool", "tool")]
    [InlineData("   ", null)]
    public void ExtractExecutable_Command_ReturnsExecutablePath(string command, string? expected)
    {
        // Act
        var result = IfeoTaskManagerReplacementService.ExtractExecutable(command);

        // Assert
        result.Should().Be(expected);
    }
}
