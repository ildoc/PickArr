using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PickArr.Api.Endpoints;
using PickArr.Core;
using PickArr.Infrastructure;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var configuredPath = builder.Configuration["PickArr:DataPath"];
    var dataPath = string.IsNullOrWhiteSpace(configuredPath) ? "/config" : configuredPath;
    Directory.CreateDirectory(dataPath);
    Directory.CreateDirectory(Path.Combine(dataPath, "logs"));

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(dataPath, "logs", "pickarr-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7));

    builder.Services.AddPickArrCore();
    builder.Services.AddPickArrInfrastructure(builder.Configuration);
    builder.Services.AddOpenApi();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();

        // Schedules are owned by app state and re-applied after storage init (T12).
        // Initial catalog import runs in CatalogBootstrapHostedService after Hangfire starts.
        await scope.ServiceProvider.GetRequiredService<ScheduleReconciler>().ReconcileAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false,
    });
    app.MapHealthChecks("/health/ready");
    app.MapSettingsEndpoints();
    app.MapSystemEndpoints();
    app.MapCatalogEndpoints();
    app.MapTitlesEndpoints();
    app.MapFavoritesEndpoints();
    app.MapRatingsEndpoints();
    app.MapRecommendationsEndpoints();
    app.MapRecoAdminEndpoints();
    app.MapArrEndpoints();
    app.MapAdminEndpoints();
    app.MapFallbackToFile("index.html");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
