using EMS.API.Services;

namespace EMS.API.Tests;

public class DomainNormalizerTests
{
    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("  Example.COM  ", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://example.com/path/page?q=1", "example.com")]
    [InlineData("https://example.com:8443/login", "example.com")]
    [InlineData("www.example.com", "www.example.com")]
    [InlineData("https://user:pw@example.com/x", "example.com")]
    [InlineData("example.com.", "example.com")]
    [InlineData("sub.domain.example.co.uk", "sub.domain.example.co.uk")]
    public void Normalize_ValidInputs_ReturnsBareHost(string input, string expected)
    {
        Assert.Equal(expected, DomainNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not a domain")]
    [InlineData("localhost")]        // no TLD
    [InlineData("example")]          // no dot
    [InlineData("http://")]          // empty host
    [InlineData(".com")]             // no label before TLD
    public void Normalize_InvalidInputs_ReturnsNull(string? input)
    {
        Assert.Null(DomainNormalizer.Normalize(input));
    }
}
