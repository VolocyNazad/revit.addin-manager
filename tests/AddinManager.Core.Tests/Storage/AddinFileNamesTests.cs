using AddinManager.Core.Storage;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Общее правило имени <c>.addin</c>: одна нормализация для реального store и тестовых двойников.
/// </summary>
public sealed class AddinFileNamesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyName_StaysEmpty(string? fileName)
    {
        Assert.Equal(string.Empty, AddinFileNames.Normalize(fileName));
    }

    [Theory]
    [InlineData("A", "A.addin")]
    [InlineData(" A.addin ", "A.addin")]
    [InlineData("A.ADDIN", "A.ADDIN")]
    [InlineData("some file.addin", "some file.addin")]
    public void Normalize_AddsOrKeepsExtension(string fileName, string expected)
    {
        Assert.Equal(expected, AddinFileNames.Normalize(fileName));
    }

    [Fact]
    public void Normalize_DoesNotAppendExtensionTwice()
    {
        Assert.Equal("A.addin", AddinFileNames.Normalize("A.addin"));
    }
}
