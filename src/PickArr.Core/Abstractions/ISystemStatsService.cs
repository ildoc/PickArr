using Pickarr.Core.Diagnostics;

namespace Pickarr.Core.Abstractions;

public interface ISystemStatsService
{
    Task<SystemStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
