using System.Reflection;
using Pickarr.Core.Abstractions;

namespace Pickarr.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/system/status", () =>
        {
            return Results.Ok(new
            {
                version = ProductVersion(),
                utcNow = DateTime.UtcNow,
            });
        });

        endpoints.MapGet("/api/v1/system/image-updates", async (
            IImageUpdateService updates,
            CancellationToken ct) =>
        {
            var dto = await updates.CheckAsync(ProductVersion(), ct);
            return Results.Ok(dto);
        });

        endpoints.MapGet("/api/v1/system/stats", async (ISystemStatsService stats, CancellationToken ct) =>
        {
            var dto = await stats.GetStatsAsync(ct);
            return Results.Ok(dto);
        });

        endpoints.MapGet("/api/v1/system/logs", async (ILogFileService logs, CancellationToken ct) =>
            Results.Ok(await logs.ListAsync(ct)));

        endpoints.MapGet("/api/v1/system/logs/{fileName}", async (
            string fileName,
            ILogFileService logs,
            CancellationToken ct) =>
        {
            var opened = await logs.OpenReadAsync(fileName, ct);
            if (opened is null)
            {
                return Results.NotFound();
            }

            var (stream, contentType, downloadName) = opened.Value;
            return Results.File(stream, contentType, downloadName);
        });

        endpoints.MapDelete("/api/v1/system/logs", async (ILogFileService logs, CancellationToken ct) =>
            Results.Ok(await logs.ClearAllAsync(ct)));

        return endpoints;
    }

    /// <summary>
    /// Product version from the assembly. A release is <c>X.Y.Z</c>; a later build is
    /// <c>X.Y.Z.N</c>. A source-revision suffix added by the SDK is omitted.
    /// </summary>
    internal static string ProductVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informational[..plus] : informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}
