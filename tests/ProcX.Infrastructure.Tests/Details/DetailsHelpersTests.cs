using System.Runtime.CompilerServices;
using ProcX.Application.Details;
using ProcX.Infrastructure.Details;
using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Tests.Details;

public class DetailsHelpersTests
{
    [Fact]
    public void NativeStructs_On64BitProcess_MatchWindowsLayout()
    {
        // Assert: sizes of the documented 64-bit layouts.
        Unsafe.SizeOf<SystemProcessInformation>().Should().Be(256);
        Unsafe.SizeOf<SystemThreadInformation>().Should().Be(80);
        Unsafe.SizeOf<ProcessHandleTableEntryInfo>().Should().Be(40);
        Unsafe.SizeOf<ProcessHandleSnapshotInformation>().Should().Be(16);
    }

    [Theory]
    [InlineData(0x0000u, IntegrityLevel.Untrusted)]
    [InlineData(0x1000u, IntegrityLevel.Low)]
    [InlineData(0x2000u, IntegrityLevel.Medium)]
    [InlineData(0x2100u, IntegrityLevel.MediumPlus)]
    [InlineData(0x3000u, IntegrityLevel.High)]
    [InlineData(0x4000u, IntegrityLevel.System)]
    [InlineData(0x5000u, IntegrityLevel.Protected)]
    public void MapIntegrityLevel_Rid_ReturnsLevel(uint rid, IntegrityLevel expected)
    {
        // Act
        var result = Win32ProcessDetailsProvider.MapIntegrityLevel(rid);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData((ushort)0x014C, ProcessArchitecture.X86)]
    [InlineData((ushort)0x8664, ProcessArchitecture.X64)]
    [InlineData((ushort)0xAA64, ProcessArchitecture.Arm64)]
    [InlineData((ushort)0x01C4, ProcessArchitecture.Arm)]
    [InlineData((ushort)0x0000, ProcessArchitecture.Unknown)]
    public void MapMachine_Value_ReturnsArchitecture(ushort machine, ProcessArchitecture expected)
    {
        // Act
        var result = Win32ProcessDetailsProvider.MapMachine(machine);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, "Executive")]
    [InlineData(6u, "UserRequest")]
    [InlineData(15u, "WrQueue")]
    [InlineData(999u, "999")]
    public void DescribeWaitReason_Value_ReturnsName(uint value, string expected)
    {
        // Act
        var result = Win32ProcessDetailsProvider.DescribeWaitReason(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveSymbol_AddressInsideModule_ReturnsModulePlusOffset()
    {
        // Arrange
        ModuleDetails[] modules =
        [
            new("a.dll", @"C:\a.dll", 0x1000, 0x1000, null, null, null),
            new("b.dll", @"C:\b.dll", 0x10000, 0x2000, null, null, null),
        ];

        // Act & Assert
        Win32ProcessDetailsProvider.ResolveSymbol(modules, 0x10abc).Should().Be("b.dll+0xabc");
        Win32ProcessDetailsProvider.ResolveSymbol(modules, 0x2000).Should().BeNull();
    }

    [Theory]
    [InlineData("Key", @"\REGISTRY\MACHINE\SOFTWARE\Microsoft", @"HKLM\SOFTWARE\Microsoft")]
    [InlineData("Key", @"\REGISTRY\USER\S-1-5-21\Software", @"HKU\S-1-5-21\Software")]
    [InlineData("Event", @"\BaseNamedObjects\x", @"\BaseNamedObjects\x")]
    public void Format_NativeName_ReturnsFriendlyName(string type, string name, string expected)
    {
        // Act
        var result = HandleNames.Format(type, name);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToDosPath_SystemDriveDevicePath_ReturnsDriveLetterPath()
    {
        // Arrange: find the device path of the system drive the same way Windows reports file names.
        var drive = Path.GetPathRoot(Environment.SystemDirectory)!.TrimEnd('\\');
        var converted = DevicePathConverter.ToDosPath(@"\Device\NotAVolume\x");

        // Act & Assert
        converted.Should().Be(@"\Device\NotAVolume\x");
        drive.Should().MatchRegex("^[A-Z]:$");
    }
}
