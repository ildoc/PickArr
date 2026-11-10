using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Recommendations;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Api.Endpoints;

public static class RecoAdminEndpoints
{
    public static IEndpointRouteBuilder MapRecoAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/recommendations");

        group.MapPost("/rebuild", (IBackgroundJobClient jobs) =>
        {
            var hf = jobs.Enqueue<RecoBuildJob>(j => j.ExecuteAsync(null, true, CancellationToken.None));
            return Results.Accepted(null, new { hangfireJobId = hf });
        });

        group.MapPost("/evaluate", (IBackgroundJobClient jobs) =>
        {
            var hf = jobs.Enqueue<RecoEvalJob>(j => j.ExecuteAsync(CancellationToken.None));
            return Results.Accepted(null, new { hangfireJobId = hf });
        });

        group.MapGet("/evaluations/latest", async (IRecoEvalService eval, CancellationToken ct) =>
        {
            var report = await eval.GetLatestAsync(ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        group.MapPost("/embeddings/rebuild", async (
            bool? all,
            IBackgroundJobClient jobs,
            CancellationToken ct) =>
        {
            var hf = jobs.Enqueue<RecoEmbeddingJob>(
                j => j.ExecuteAsync(all == true, true, CancellationToken.None));
            return Results.Accepted(null, new { hangfireJobId = hf });
        });

        group.MapPost("/model/download", async (
            IBackgroundJobClient jobs,
            CancellationToken ct) =>
        {
            var hf = jobs.Enqueue<RecoModelDownloadJob>(
                j => j.ExecuteAsync(false, CancellationToken.None));
            return Results.Accepted(null, new { hangfireJobId = hf });
        });

        group.MapPost("/model/detect", async (
            IBackgroundJobClient jobs,
            CancellationToken ct) =>
        {
            var hf = jobs.Enqueue<RecoModelDownloadJob>(
                j => j.ExecuteAsync(true, CancellationToken.None));
            return Results.Accepted(null, new { hangfireJobId = hf });
        });

        group.MapPost("/reset", async (
            bool includeRatings,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Recommendations.ExecuteDeleteAsync(ct);
            await db.TitleEmbeddings.ExecuteDeleteAsync(ct);
            await db.RecoEventLogs.ExecuteDeleteAsync(ct);
            if (includeRatings)
            {
                await db.RatingEvents.ExecuteDeleteAsync(ct);
                await db.Ratings.ExecuteDeleteAsync(ct);
            }

            var adminId = await db.Profiles.AsNoTracking()
                .Where(profile => profile.IsAdmin)
                .Select(profile => (int?)profile.Id)
                .FirstOrDefaultAsync(ct);
            var states = await db.RecoStates.ToListAsync(ct);
            foreach (var state in states)
            {
                var ratingCount = includeRatings
                    ? 0
                    : await db.Ratings.CountAsync(rating => rating.ProfileId == state.ProfileId, ct);
                state.CurrentVersion = 0;
                state.EffectiveN = 0;
                state.RatingCount = ratingCount;
                state.Stage = RecoLearning.StageCold;
                state.ProfileJson = null;
                state.LastError = null;
                if (state.ProfileId == adminId)
                {
                    state.EmbeddingCoverage = 0;
                    state.LastEvalJson = null;
                    state.LastEvalAt = null;
                    state.Level3Active = false;
                    // Keep ModelStatus/ModelId — files remain on disk.
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Accepted(null, new { includeRatings });
        });

        return endpoints;
    }
}
