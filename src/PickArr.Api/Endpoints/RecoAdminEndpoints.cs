using Hangfire;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Recommendations;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Api.Endpoints;

public static class RecoAdminEndpoints
{
    public static IEndpointRouteBuilder MapRecoAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/recommendations");

        group.MapPost("/rebuild", (IBackgroundJobClient jobs) =>
        {
            var hf = jobs.Enqueue<RecoBuildJob>(j => j.ExecuteAsync(true, CancellationToken.None));
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
            PickArrDbContext db,
            IBackgroundJobClient jobs,
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

            var state = await db.RecoStates.FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, ct);
            if (state is not null)
            {
                state.CurrentVersion = 0;
                state.EffectiveN = 0;
                state.RatingCount = includeRatings ? 0 : await db.Ratings.CountAsync(ct);
                state.Stage = RecoLearning.StageCold;
                state.ProfileJson = null;
                state.EmbeddingCoverage = 0;
                state.LastError = null;
                state.LastEvalJson = null;
                state.LastEvalAt = null;
                state.Level3Active = false;
                // Keep ModelStatus/ModelId — files remain on disk.
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            jobs.Enqueue<RecoBuildJob>(j => j.ExecuteAsync(true, CancellationToken.None));
            return Results.Accepted(null, new { includeRatings });
        });

        return endpoints;
    }
}
