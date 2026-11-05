using PickArr.Core.Arr;
using PickArr.Core.Catalog;

namespace PickArr.Core.Abstractions;

public interface IArrAddService
{
    Task<ArrDetailDto> AddAsync(int titleId, SonarrAddOptions options, CancellationToken cancellationToken = default);
    Task<ArrDetailDto> AddAsync(int titleId, RadarrAddOptions options, CancellationToken cancellationToken = default);
}

public interface IArrDeleteService
{
    Task<ArrDetailDto> DeleteAsync(
        int titleId,
        bool deleteFiles,
        bool addImportExclusion,
        CancellationToken cancellationToken = default);
}
