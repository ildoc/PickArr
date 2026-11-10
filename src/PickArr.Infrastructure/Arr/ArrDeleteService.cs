using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Arr;

public sealed class ArrDeleteService(
    PickarrDbContext db,
    IArrClientFactory clientFactory,
    IArrLibrarySync librarySync,
    ITitleQueryService titles,
    IClock clock) : IArrDeleteService
{
    public async Task<ArrDetailDto> DeleteAsync(
        int titleId,
        bool deleteFiles,
        bool addImportExclusion,
        CancellationToken cancellationToken = default,
        int? profileId = null)
    {
        var title = await db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken)
                    ?? throw ArrOperationException.TitleNotFound(titleId);

        var kind = title.MediaType == MediaTypes.Series ? ArrKind.Sonarr : ArrKind.Radarr;
        var kindKey = kind.ToStorage();
        var cached = await db.ArrLibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TitleId == titleId && a.Kind == kindKey, cancellationToken)
                     ?? throw ArrOperationException.NotInLibrary();

        var client = await clientFactory.TryCreateAsync(kind, cancellationToken)
                     ?? throw ArrOperationException.NotConfigured();

        var options = new { deleteFiles, addImportExclusion, path = cached.Path, sizeOnDisk = cached.SizeOnDisk };

        try
        {
            await client.DeleteAsync(cached.ArrId, deleteFiles, addImportExclusion, cancellationToken);
            await librarySync.RemoveItemAsync(kind, cached.ArrId, cancellationToken);
            await WriteAuditAsync(
                kind,
                cached.ArrId,
                titleId,
                options,
                ArrAuditOutcomes.Success,
                error: null,
                profileId,
                cancellationToken);

            var detail = await titles.GetTitleAsync(titleId, sourceId: null, cancellationToken);
            return detail?.Arr
                   ?? throw new InvalidOperationException("Title detail missing after Arr delete.");
        }
        catch (ArrOperationException)
        {
            throw;
        }
        catch (ArrClientException ex)
        {
            await WriteAuditAsync(
                kind,
                cached.ArrId,
                titleId,
                options,
                ArrAuditOutcomes.Failed,
                ex.Message,
                profileId,
                cancellationToken);
            throw ArrAddService.MapClientException(ex);
        }
        catch (HttpRequestException ex)
        {
            await WriteAuditAsync(
                kind,
                cached.ArrId,
                titleId,
                options,
                ArrAuditOutcomes.Failed,
                ex.Message,
                profileId,
                cancellationToken);
            throw ArrOperationException.Unreachable(ex.Message);
        }
    }

    private async Task WriteAuditAsync(
        ArrKind kind,
        int arrId,
        int titleId,
        object options,
        string outcome,
        string? error,
        int? profileId,
        CancellationToken cancellationToken)
    {
        db.ArrAuditLogs.Add(new ArrAuditLog
        {
            ProfileId = profileId,
            At = clock.UtcNow,
            Action = ArrAuditActions.Delete,
            Kind = kind.ToStorage(),
            ArrId = arrId,
            TitleId = titleId,
            Options = JsonSerializer.Serialize(options, ArrJson.Options),
            Outcome = outcome,
            Error = error,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
