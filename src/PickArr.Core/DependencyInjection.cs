using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Settings;

namespace PickArr.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddPickArrCore(this IServiceCollection services)
    {
        services.AddSingleton<WatchmodeQuotaObservation>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ICreditBudget, CreditBudgetService>();
        return services;
    }
}
