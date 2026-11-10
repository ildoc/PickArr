using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Metadata.Reco;

public sealed class RecoEmbeddingService(
    PickarrDbContext db,
    IRecoModelService models,
    ISettingsStore settings,
    IClock clock,
    ILogger<RecoEmbeddingService> logger) : IRecoEmbeddingService
{
    public async Task<int> EmbedPendingAsync(bool rebuildAll = false, CancellationToken cancellationToken = default)
    {
        if (!await GetBoolSettingAsync(SettingsKeys.RecoEmbeddingsEnabled, true, cancellationToken))
        {
            logger.LogInformation("Embeddings disabled; skipping");
            return 0;
        }

        if (!models.IsReady)
        {
            await models.DetectAsync(cancellationToken);
            if (!models.IsReady)
            {
                logger.LogInformation("Model not ready; skipping embeddings");
                return 0;
            }
        }

        var modelId = (await models.GetInfoAsync(cancellationToken)).ModelId
                      ?? RecoModelCatalog.DefaultModelId;
        var batchSize = await GetIntSettingAsync(SettingsKeys.RecoEmbeddingsBatchSize, 16, cancellationToken);
        batchSize = Math.Clamp(batchSize, 1, 64);
        var throttle = await GetBoolSettingAsync(SettingsKeys.RecoEmbeddingsThrottle, true, cancellationToken);

        var enabledSourceIds = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled && s.Provider != null && s.Provider.IsEnabled)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (enabledSourceIds.Count == 0)
        {
            return 0;
        }

        var poolIds = await db.Availabilities.AsNoTracking()
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existing = await db.TitleEmbeddings.AsNoTracking()
            .Where(e => e.ModelId == modelId && poolIds.Contains(e.TitleId))
            .Select(e => new { e.TitleId, e.TextHash })
            .ToListAsync(cancellationToken);
        var hashByTitle = existing.ToDictionary(e => e.TitleId, e => e.TextHash);

        var pendingIds = new List<int>();
        foreach (var id in poolIds)
        {
            if (rebuildAll || !hashByTitle.ContainsKey(id))
            {
                pendingIds.Add(id);
            }
        }

        // Also re-hash check for non-rebuild: load texts for titles with embedding and compare.
        if (!rebuildAll)
        {
            var maybeStale = hashByTitle.Keys.ToList();
            foreach (var chunk in maybeStale.Chunk(200))
            {
                var texts = await BuildTextsAsync(chunk.ToList(), cancellationToken);
                foreach (var (titleId, text) in texts)
                {
                    var hash = RecoEmbeddingText.Sha256Hex(text);
                    if (!string.Equals(hash, hashByTitle[titleId], StringComparison.OrdinalIgnoreCase)
                        && !pendingIds.Contains(titleId))
                    {
                        pendingIds.Add(titleId);
                    }
                }
            }
        }

        var processed = 0;
        foreach (var batch in pendingIds.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ids = batch.ToList();
            var texts = await BuildTextsAsync(ids, cancellationToken);
            if (texts.Count == 0)
            {
                continue;
            }

            var orderedIds = texts.Keys.ToList();
            var orderedTexts = orderedIds.Select(id => texts[id]).ToList();
            var vectors = models.TryEmbedBatch(orderedTexts);
            if (vectors is null)
            {
                break;
            }

            var now = clock.UtcNow;
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var titleId = orderedIds[i];
                var vector = vectors[i];
                var hash = RecoEmbeddingText.Sha256Hex(orderedTexts[i]);
                var bytes = FloatsToBytes(vector);

                var row = await db.TitleEmbeddings.FindAsync([titleId, modelId], cancellationToken);
                if (row is null)
                {
                    db.TitleEmbeddings.Add(new TitleEmbedding
                    {
                        TitleId = titleId,
                        ModelId = modelId,
                        Vector = bytes,
                        TextHash = hash,
                        UpdatedAt = now,
                    });
                }
                else
                {
                    row.Vector = bytes;
                    row.TextHash = hash;
                    row.UpdatedAt = now;
                }

                processed++;
            }

            await db.SaveChangesAsync(cancellationToken);

            if (throttle)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        await UpdateCoverageAsync(modelId, poolIds.Count, cancellationToken);
        return processed;
    }

    private async Task UpdateCoverageAsync(string modelId, int poolCount, CancellationToken ct)
    {
        var embedded = poolCount == 0
            ? 0
            : await db.TitleEmbeddings.CountAsync(
                e => e.ModelId == modelId
                     && db.Availabilities.Any(a => a.TitleId == e.TitleId),
                ct);

        // Recount against pool more accurately.
        var enabledSourceIds = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled && s.Provider != null && s.Provider.IsEnabled)
            .Select(s => s.Id)
            .ToListAsync(ct);
        var poolIds = await db.Availabilities.AsNoTracking()
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .ToListAsync(ct);
        var poolSet = poolIds.ToHashSet();
        embedded = await db.TitleEmbeddings.AsNoTracking()
            .Where(e => e.ModelId == modelId && poolSet.Contains(e.TitleId))
            .CountAsync(ct);

        var coverage = poolIds.Count == 0 ? 0 : (double)embedded / poolIds.Count;
        var state = await RecoProfileStates.GetOrCreateAdminAsync(db, ct);
        if (state is null)
        {
            return;
        }

        state.EmbeddingCoverage = coverage;
        state.ModelId ??= modelId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, string>> BuildTextsAsync(
        IReadOnlyList<int> titleIds,
        CancellationToken ct)
    {
        var titles = await db.Titles.AsNoTracking()
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre)!.ThenInclude(g => g!.Localizations)
            .Include(t => t.TitleKeywords).ThenInclude(tk => tk.Keyword)
            .Where(t => titleIds.Contains(t.Id))
            .ToListAsync(ct);

        var result = new Dictionary<int, string>(titles.Count);
        foreach (var title in titles)
        {
            var loc = title.Localizations.FirstOrDefault(l =>
                          string.Equals(l.Language, LanguageFallback.English, StringComparison.OrdinalIgnoreCase))
                      ?? title.Localizations.FirstOrDefault();
            var name = loc?.Name ?? title.OriginalTitle ?? $"Title {title.Id}";
            var genres = title.TitleGenres
                .Select(tg => tg.Genre?.Localizations
                    .FirstOrDefault(l => l.Language == LanguageFallback.English)?.Name
                    ?? tg.Genre?.Localizations.FirstOrDefault()?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList();
            var keywords = title.TitleKeywords
                .Select(tk => tk.Keyword?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList();
            result[title.Id] = RecoEmbeddingText.Build(
                name,
                title.ReleaseYear,
                genres,
                keywords,
                loc?.Tagline,
                loc?.Overview);
        }

        return result;
    }

    public static byte[] FloatsToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] BytesToFloats(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        try { return JsonSerializer.Deserialize<bool>(raw); }
        catch (JsonException) { return fallback; }
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        try { return JsonSerializer.Deserialize<int>(raw); }
        catch (JsonException) { return int.TryParse(raw.Trim('"'), out var n) ? n : fallback; }
    }
}
