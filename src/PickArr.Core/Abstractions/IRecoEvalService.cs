using PickArr.Core.Recommendations;

namespace PickArr.Core.Abstractions;

public interface IRecoEvalService
{
    /// <summary>Leave-one-out offline evaluation; persists report and updates Level3Active gate.</summary>
    Task<RecoEvalReportDto> EvaluateAsync(CancellationToken cancellationToken = default);

    Task<RecoEvalReportDto?> GetLatestAsync(CancellationToken cancellationToken = default);
}
