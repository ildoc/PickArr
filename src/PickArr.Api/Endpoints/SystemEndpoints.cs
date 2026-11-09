using System.Reflection;

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
