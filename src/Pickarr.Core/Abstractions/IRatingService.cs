using Pickarr.Core.Recommendations;

namespace Pickarr.Core.Abstractions;

public interface IRatingService
{
    Task SetAsync(int profileId, int titleId, string value, string origin, CancellationToken cancellationToken = default);
    Task ClearAsync(int profileId, int titleId, string origin, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RatingExportItemDto>> ExportAsync(int profileId, CancellationToken cancellationToken = default);

    Task<RatingImportResultDto> ImportAsync(
        int profileId,
        RatingImportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Batch onboarding votes; null value = skip. One debounced rebuild at the end.</summary>
    Task OnboardingAsync(
        int profileId,
        IReadOnlyList<RatingOnboardingItem> items,
        CancellationToken cancellationToken = default);
}

public sealed record RatingOnboardingItem(int TitleId, string? Value);
