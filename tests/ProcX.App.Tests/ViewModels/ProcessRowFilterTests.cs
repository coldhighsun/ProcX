using ProcX.App.ViewModels;
using ProcX.Application.Processes;

namespace ProcX.App.Tests.ViewModels;

public class ProcessRowFilterTests
{
    private static ProcessRowViewModel CreateRow()
    {
        var row = new ProcessRowViewModel(Rows.Metrics(1234, "Explorer.EXE"));
        row.ApplyStaticInfo(new ProcessStaticInfo(@"C:\Windows\explorer.exe", "alice", "Windows Explorer", false), null);
        return row;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_BlankSearch_ReturnsTrue(string? search)
    {
        // Act
        var result = ProcessRowFilter.Matches(CreateRow(), search);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("WINDOWS EXP")]
    [InlineData("ALI")]
    [InlineData(@"c:\windows")]
    [InlineData("1234")]
    [InlineData(" 1234 ")]
    public void Matches_MatchingField_ReturnsTrue(string search)
    {
        // Act
        var result = ProcessRowFilter.Matches(CreateRow(), search);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("123")]
    public void Matches_NoMatchingField_ReturnsFalse(string search)
    {
        // Act
        var result = ProcessRowFilter.Matches(CreateRow(), search);

        // Assert
        result.Should().BeFalse();
    }
}
