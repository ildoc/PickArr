using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pickarr.Infrastructure.Diagnostics;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Pickarr.Infrastructure.Tests;

public sealed class GhcrImageTagClientTests : IAsyncLifetime
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListTagsAsync_returns_tags_after_anonymous_token()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/token").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"token":"test-token"}"""));
        _server
            .Given(Request.Create()
                .WithPath("/v2/ildoc/pickarr/tags/list")
                .WithHeader("Authorization", "Bearer test-token")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"name":"ildoc/pickarr","tags":["edge","1.2.3","1.2.3.4","latest"]}"""));

        var client = CreateClient();
        var tags = await client.ListTagsAsync();

        tags.ShouldBe(["edge", "1.2.3", "1.2.3.4", "latest"]);
    }

    [Fact]
    public async Task ListTagsAsync_fail_soft_when_token_endpoint_errors()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/token").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var client = CreateClient();
        var tags = await client.ListTagsAsync();

        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListTagsAsync_fail_soft_when_tags_endpoint_errors()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/token").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"token":"test-token"}"""));
        _server
            .Given(Request.Create().WithPath("/v2/ildoc/pickarr/tags/list").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var client = CreateClient();
        var tags = await client.ListTagsAsync();

        tags.ShouldBeEmpty();
    }

    private GhcrImageTagClient CreateClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0] + "/") };
        var options = Options.Create(new GhcrOptions
        {
            BaseUrl = _server.Urls[0],
            Repository = "ildoc/pickarr",
        });
        return new GhcrImageTagClient(http, options, NullLogger<GhcrImageTagClient>.Instance);
    }
}
