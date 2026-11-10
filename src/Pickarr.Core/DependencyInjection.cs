using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Settings;

namespace Pickarr.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddPickarrCore(this IServiceCollection services)
    {
        services.AddSingleton<WatchmodeQuotaObservation>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ICreditBudget, CreditBudgetService>();
        return services;
    }
}
