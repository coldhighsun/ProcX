using System.Globalization;
using ProcX.App.Converters;

namespace ProcX.App.Tests.Converters;

public class ByteSizeConverterTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(-5L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(5L * 1024 * 1024, "5.0 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
    [InlineData(2L * 1024 * 1024 * 1024 * 1024 * 1024, "2,048.0 TB")]
    public void Format_Bytes_ReturnsHumanReadableSize(long bytes, string expected)
    {
        // Act
        var result = ByteSizeConverter.Format(bytes, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_UnsupportedValue_ReturnsNull()
    {
        // Act
        var result = new ByteSizeConverter().Convert("x", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeNull();
    }
}
