using PickArr.Infrastructure.Metadata.Tmdb;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class TmdbAuthHandlerTests
{
    [Fact]
    public void Api_key_is_sent_as_a_query_parameter()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "tv/1396?language=en-US");
        TmdbAuthHandler.ApplyCredential(request, "0123456789abcdef0123456789abcdef");

        request.Headers.Authorization.ShouldBeNull();
        request.RequestUri!.OriginalString.ShouldContain("api_key=0123456789abcdef0123456789abcdef");
        request.RequestUri.OriginalString.ShouldContain("language=en-US");
    }

    [Fact]
    public void Read_access_token_is_sent_as_a_bearer_token()
    {
        const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signature";
        using var request = new HttpRequestMessage(HttpMethod.Get, "tv/1396");
        TmdbAuthHandler.ApplyCredential(request, token);

        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(token);
        request.RequestUri!.OriginalString.ShouldNotContain("api_key");
    }
}
