using System.Text;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class SymbolIconFontTests
{
    [Fact]
    public void SourceFiles_UseThemeSymbolFontInsteadOfHardcodedFluentFont()
    {
        var sourceRoot = TestProjectPaths.FromRepositoryRoot("src");
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));

        var source = string.Join(
            Environment.NewLine,
            sourceFiles.Select(path => File.ReadAllText(path, Encoding.UTF8)));

        Assert.DoesNotContain("Segoe Fluent Icons", source, StringComparison.Ordinal);
        Assert.Contains("SymbolThemeFontFamily", source, StringComparison.Ordinal);
    }
}
