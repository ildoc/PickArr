using Pickarr.Core.Profiles;

namespace Pickarr.Core.Abstractions;

public interface IProfileService
{
    Task<IReadOnlyList<ProfileDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProfileDto> CreateAsync(
        int? actorProfileId,
        ProfileCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateAsync(
        int? actorProfileId,
        int profileId,
        ProfileUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int? actorProfileId,
        int profileId,
        CancellationToken cancellationToken = default);
}
