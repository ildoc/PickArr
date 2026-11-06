using PickArr.Core.Recommendations;

namespace PickArr.Core.Abstractions;

public interface IRatingService
{
    Task SetAsync(int titleId, string value, string origin, CancellationToken cancellationToken = default);
    Task ClearAsync(int titleId, string origin, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RatingExportItemDto>> ExportAsync(CancellationToken cancellationToken = default);

    Task<RatingImportResultDto> ImportAsync(
        RatingImportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Batch onboarding votes; null value = skip. One debounced rebuild at the end.</summary>
    Task OnboardingAsync(
        IReadOnlyList<RatingOnboardingItem> items,
        CancellationToken cancellationToken = default);
}

public sealed record RatingOnboardingItem(int TitleId, string? Value);
