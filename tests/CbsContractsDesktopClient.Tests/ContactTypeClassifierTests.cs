using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContactTypeClassifierTests
{
    [Theory]
    [InlineData("ivan@example.com", "Email", "mailto:ivan@example.com")]
    [InlineData("+7 999 123 45 67", "Phone", "tel:+7 999 123 45 67")]
    [InlineData("example.ru", "SiteUrl", "http://example.ru/")]
    [InlineData("@employee_support", "Telegram", "tg://resolve/?domain=employee_support")]
    public void TryClassify_RecognizesSupportedContactTypes(string value, string expectedType, string expectedUri)
    {
        var classified = ContactTypeClassifier.TryClassify(value, out var match);

        Assert.True(classified);
        Assert.Equal(expectedType, match.Type);
        Assert.Equal(expectedUri, ContactTypeClassifier.TryCreateLaunchUri(value, match)?.ToString());
    }

    [Fact]
    public void TryClassify_RejectsUnknownContactType()
    {
        var classified = ContactTypeClassifier.TryClassify("not a supported contact", out var match);

        Assert.False(classified);
        Assert.Equal(string.Empty, match.Type);
    }
}
