using Xunit;

namespace Flinku.Tests
{
    public class CustomDomainTests
    {
        [Fact]
        public void TryParseBaseUrl_AcceptsValidHttpsSubdomain()
        {
            Assert.True(FlinkuUrlHelpers.TryParseBaseUrl(
                "https://myapp.flku.dev",
                out var subdomain,
                out var apiBaseUrl,
                out var baseUrlHost,
                out var error));
            Assert.Equal("myapp", subdomain);
            Assert.Equal("https://flku.dev", apiBaseUrl);
            Assert.Equal("myapp.flku.dev", baseUrlHost);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("https://flku.dev")]
        [InlineData("http://evil.com")]
        [InlineData("https://myapp.flku.dev/path")]
        [InlineData("not-a-url")]
        [InlineData("")]
        public void TryParseBaseUrl_RejectsInvalid(string baseUrl)
        {
            Assert.False(FlinkuUrlHelpers.TryParseBaseUrl(
                baseUrl, out _, out _, out _, out var error));
            Assert.Equal(FlinkuUrlHelpers.InvalidBaseUrlMessage, error);
        }

        [Fact]
        public void TryParseCustomDomain_NormalizesHost()
        {
            Assert.True(FlinkuUrlHelpers.TryParseCustomDomain(
                "Links.Example.COM.", out var host, out var error));
            Assert.Equal("links.example.com", host);
            Assert.Null(error);
        }

        [Fact]
        public void TryParseCustomDomain_OptionalNullOrEmpty()
        {
            Assert.True(FlinkuUrlHelpers.TryParseCustomDomain(null, out var host, out _));
            Assert.Null(host);
            Assert.True(FlinkuUrlHelpers.TryParseCustomDomain("  ", out host, out _));
            Assert.Null(host);
        }

        [Theory]
        [InlineData("https://links.example.com", "protocol")]
        [InlineData("links.example.com/path", "path")]
        [InlineData("links.example.com:443", "port")]
        [InlineData("links example.com", "whitespace")]
        public void TryParseCustomDomain_RejectsInvalid(string input, string expectedFragment)
        {
            Assert.False(FlinkuUrlHelpers.TryParseCustomDomain(input, out _, out var error));
            Assert.Contains(expectedFragment, error);
        }

        [Fact]
        public void BuildPublicShortUrl_UsesCustomDomainWhenSet()
        {
            Assert.Equal(
                "https://links.example.com/abc",
                FlinkuUrlHelpers.BuildPublicShortUrl("myapp", "abc", "links.example.com"));
        }

        [Fact]
        public void BuildPublicShortUrl_FallsBackToSubdomainHost()
        {
            Assert.Equal(
                "https://myapp.flku.dev/abc",
                FlinkuUrlHelpers.BuildPublicShortUrl("myapp", "abc"));
        }

        [Fact]
        public void IsAcceptedClipboardHost_AcceptsFlkuBaseAndCustom()
        {
            const string baseUrlHost = "myapp.flku.dev";
            Assert.True(FlinkuUrlHelpers.IsAcceptedClipboardHost("campaign.flku.dev", baseUrlHost));
            Assert.True(FlinkuUrlHelpers.IsAcceptedClipboardHost("myapp.flku.dev", baseUrlHost));
            Assert.True(FlinkuUrlHelpers.IsAcceptedClipboardHost(
                "links.example.com", baseUrlHost, "links.example.com"));
            Assert.False(FlinkuUrlHelpers.IsAcceptedClipboardHost(
                "evil.example.com", baseUrlHost, "links.example.com"));
        }

        [Fact]
        public void IsClipboardUrlAccepted_ParsesHostFromUrl()
        {
            Assert.True(FlinkuUrlHelpers.IsClipboardUrlAccepted(
                "https://links.example.com/promo",
                "myapp.flku.dev",
                "links.example.com"));
            Assert.False(FlinkuUrlHelpers.IsClipboardUrlAccepted(
                "https://evil.example.com/promo",
                "myapp.flku.dev",
                "links.example.com"));
        }
    }
}
