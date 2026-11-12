using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Settings;

namespace Pickarr.Api.Endpoints;

public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/setup");

        group.MapGet("/", async (SetupService setup, CancellationToken ct) =>
            Results.Ok(await setup.GetStatusAsync(ct)));

        group.MapPost("/complete", async (SetupService setup, CancellationToken ct) =>
        {
            var (status, error) = await setup.CompleteAsync(ct);
            if (error is not null)
            {
                return Results.BadRequest(new { detail = error });
            }

            return Results.Ok(status);
        });

        return endpoints;
    }
}
