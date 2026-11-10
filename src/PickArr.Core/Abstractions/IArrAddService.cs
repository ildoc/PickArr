using Pickarr.Core.Arr;
using Pickarr.Core.Catalog;

namespace Pickarr.Core.Abstractions;

public interface IArrAddService
{
    Task<ArrDetailDto> AddAsync(
        int titleId,
        SonarrAddOptions options,
        CancellationToken cancellationToken = default,
        int? profileId = null);

    Task<ArrDetailDto> AddAsync(
        int titleId,
        RadarrAddOptions options,
        CancellationToken cancellationToken = default,
        int? profileId = null);
}

public interface IArrDeleteService
{
    Task<ArrDetailDto> DeleteAsync(
        int titleId,
        bool deleteFiles,
        bool addImportExclusion,
        CancellationToken cancellationToken = default,
        int? profileId = null);
}
