using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PickArr.Infrastructure.Jobs;

/// <summary>
/// Queues the initial catalog import after the host (and Hangfire servers) have started.
/// Running this from <c>Program</c> before <c>RunAsync</c> enqueues work that workers may
/// not pick up reliably when settings arrive only from the environment at first boot.
/// </summary>
public sealed class CatalogBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<CatalogBootstrapHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await WaitForStartedAsync(stoppingToken))
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CatalogBootstrap>()
                .StartAsync(force: false, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Initial catalog import failed to start after host startup");
        }
    }

    private async Task<bool> WaitForStartedAsync(CancellationToken stoppingToken)
    {
        if (lifetime.ApplicationStarted.IsCancellationRequested)
        {
            return true;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var startedRegistration = lifetime.ApplicationStarted.Register(static state =>
            ((TaskCompletionSource)state!).TrySetResult(), started);
        await using var stoppingRegistration = stoppingToken.Register(static state =>
            ((TaskCompletionSource)state!).TrySetCanceled(), started);

        try
        {
            await started.Task;
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
