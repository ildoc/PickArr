using PickArr.Core.Admin;

namespace PickArr.Core.Abstractions;

public interface IAdminJobService
{
    Task<IReadOnlyList<AdminJobDto>> ListJobsAsync(CancellationToken cancellationToken = default);

    Task<TriggerJobResult> TriggerAsync(
        string jobKey,
        TriggerJobRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedAdminRunsDto> ListRunsAsync(
        string? kind,
        string? status,
        int page,
        int pageSize,
        string? jobKey = null,
        CancellationToken cancellationToken = default);

    Task<AdminRunSummaryDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IAdminQuotaService
{
    Task<AdminQuotaDto> GetWatchmodeQuotaAsync(CancellationToken cancellationToken = default);
}
