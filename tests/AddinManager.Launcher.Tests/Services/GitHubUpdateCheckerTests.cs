using System.Net;
using System.Net.Http;
using System.Text;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Проверка обновлений через GitHub: читает <c>releases/latest</c>, сравнивает <c>tag_name</c>
/// с версией сборки, отдаёт ссылку на скачивание. Сеть подменена <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class GitHubUpdateCheckerTests
{
    [Fact]
    public async Task CheckAsync_NewerTag_ReportsUpdate()
    {
        var checker = NewChecker(Status(HttpStatusCode.OK, """{"tag_name":"v1.2.3","html_url":"https://example.com/releases/tag/v1.2.3"}"""));

        var result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.True(result.HasUpdate);
        Assert.Equal("1.2.3", result.LatestVersion);
        Assert.Equal("https://example.com/releases/tag/v1.2.3", result.DownloadUrl);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_OlderTag_NoUpdate()
    {
        var checker = NewChecker(Status(HttpStatusCode.OK, """{"tag_name":"v0.1.0","html_url":"https://example.com"}"""));

        var result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HasUpdate);
        Assert.Equal("0.1.0", result.LatestVersion);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_NonSuccessStatus_ReturnsError()
    {
        var checker = NewChecker(Status(HttpStatusCode.InternalServerError, ""));

        var result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HasUpdate);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CheckAsync_NotFound_NoUpdateWithoutError()
    {
        var checker = NewChecker(Status(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"));

        var result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HasUpdate);
        Assert.Null(result.Error);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_MalformedJson_ReturnsError()
    {
        var checker = NewChecker(Status(HttpStatusCode.OK, "not json"));

        var result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HasUpdate);
        Assert.NotNull(result.Error);
    }

    private static GitHubUpdateChecker NewChecker(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        return new GitHubUpdateChecker(new HttpClient(handler), NullLogger<GitHubUpdateChecker>.Instance);
    }

    private static HttpResponseMessage Status(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
