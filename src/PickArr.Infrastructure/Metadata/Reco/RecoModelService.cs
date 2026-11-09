using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Metadata.Reco;

public sealed class RecoModelService(
    PickarrDbContext db,
    ISettingsStore settings,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    RecoOnnxEmbedder embedder,
    ILogger<RecoModelService> logger) : IRecoModelService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool IsReady => embedder.IsReady;

    public async Task<RecoModelInfoDto> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.RecoStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, cancellationToken);
        var dir = ResolveModelDirectory(await GetModelIdAsync(cancellationToken));
        long? size = null;
        var onnx = Path.Combine(dir, RecoModelCatalog.DefaultOnnxFileName);
        if (File.Exists(onnx))
        {
            size = new FileInfo(onnx).Length;
        }

        return new RecoModelInfoDto(
            state?.ModelStatus ?? RecoModelStatuses.Absent,
            state?.ModelId,
            size,
            dir,
            state?.LastError ?? embedder.LastError);
    }

    public async Task DetectAsync(CancellationToken cancellationToken = default)
    {
        var modelId = await GetModelIdAsync(cancellationToken);
        var dir = ResolveModelDirectory(modelId);

        if (!File.Exists(Path.Combine(dir, RecoModelCatalog.DefaultOnnxFileName))
            || !File.Exists(Path.Combine(dir, RecoModelCatalog.VocabFileName)))
        {
            await SetStatusAsync(RecoModelStatuses.Absent, modelId, "Model files not found", cancellationToken);
            return;
        }

        if (!await VerifyHashAsync(dir, cancellationToken))
        {
            await SetStatusAsync(RecoModelStatuses.Error, modelId, "ONNX SHA-256 mismatch", cancellationToken);
            return;
        }

        var threads = await GetIntSettingAsync(SettingsKeys.RecoEmbeddingsThreads, 2, cancellationToken);
        if (!embedder.TryLoad(dir, modelId, threads, logger))
        {
            await SetStatusAsync(
                RecoModelStatuses.Error,
                modelId,
                embedder.LastError ?? "Failed to load model",
                cancellationToken);
            return;
        }

        if (!embedder.RunSelfTest(out var selfErr))
        {
            await SetStatusAsync(
                RecoModelStatuses.Error,
                modelId,
                selfErr ?? "Self-test failed",
                cancellationToken);
            return;
        }

        await WriteManifestAsync(dir, modelId, cancellationToken);
        await SetStatusAsync(RecoModelStatuses.Ready, modelId, error: null, cancellationToken);
    }

    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        var modelId = await GetModelIdAsync(cancellationToken);
        var dir = ResolveModelDirectory(modelId);
        Directory.CreateDirectory(dir);
        await SetStatusAsync(RecoModelStatuses.Downloading, modelId, error: null, cancellationToken);

        try
        {
            var client = httpClientFactory.CreateClient("reco-model");
            var onnxPath = Path.Combine(dir, RecoModelCatalog.DefaultOnnxFileName);
            var vocabPath = Path.Combine(dir, RecoModelCatalog.VocabFileName);
            var tmpOnnx = onnxPath + ".partial";
            var tmpVocab = vocabPath + ".partial";

            await DownloadFileAsync(client, RecoModelCatalog.DefaultOnnxUrl, tmpOnnx, cancellationToken);
            await DownloadFileAsync(client, RecoModelCatalog.DefaultVocabUrl, tmpVocab, cancellationToken);

            if (!await VerifyHashFileAsync(tmpOnnx, RecoModelCatalog.DefaultOnnxSha256, cancellationToken))
            {
                await SetStatusAsync(RecoModelStatuses.Error, modelId, "Downloaded ONNX SHA-256 mismatch", cancellationToken);
                return;
            }

            File.Copy(tmpOnnx, onnxPath, overwrite: true);
            File.Copy(tmpVocab, vocabPath, overwrite: true);
            TryDelete(tmpOnnx);
            TryDelete(tmpVocab);

            await DetectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reco model download failed");
            await SetStatusAsync(RecoModelStatuses.Error, modelId, ex.Message, cancellationToken);
            throw;
        }
    }

    public float[]? TryEmbed(string text)
    {
        if (!embedder.IsReady)
        {
            return null;
        }

        try
        {
            return embedder.Embed(text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embed failed");
            return null;
        }
    }

    public IReadOnlyList<float[]>? TryEmbedBatch(IReadOnlyList<string> texts)
    {
        if (!embedder.IsReady)
        {
            return null;
        }

        try
        {
            return embedder.EmbedBatch(texts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embed batch failed");
            return null;
        }
    }

    public string ResolveModelDirectory(string modelId)
    {
        var configured = configuration["Pickarr:Models:Path"];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(ResolveDataPath(), "models")
            : configured;
        return Path.Combine(root, modelId);
    }

    private string ResolveDataPath()
    {
        var configured = configuration["Pickarr:DataPath"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pickarr")
            : configured;
    }

    private async Task<string> GetModelIdAsync(CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoEmbeddingsModelId, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return RecoModelCatalog.DefaultModelId;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? RecoModelCatalog.DefaultModelId;
        }
        catch (JsonException)
        {
            return raw.Trim('"');
        }
    }

    private async Task SetStatusAsync(string status, string modelId, string? error, CancellationToken ct)
    {
        var state = await db.RecoStates.FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, ct);
        if (state is null)
        {
            state = new RecoState { Id = RecoState.SingletonId };
            db.RecoStates.Add(state);
        }

        state.ModelStatus = status;
        state.ModelId = modelId;
        state.LastError = error;
        await db.SaveChangesAsync(ct);
    }

    private static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string path,
        CancellationToken ct)
    {
        await using var remote = await client.GetStreamAsync(url, ct);
        await using var local = File.Create(path);
        await remote.CopyToAsync(local, ct);
    }

    private static async Task<bool> VerifyHashFileAsync(string path, string expectedHex, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        var hex = Convert.ToHexString(hash);
        return string.Equals(hex, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> VerifyHashAsync(string dir, CancellationToken ct) =>
        await VerifyHashFileAsync(
            Path.Combine(dir, RecoModelCatalog.DefaultOnnxFileName),
            RecoModelCatalog.DefaultOnnxSha256,
            ct);

    private async Task WriteManifestAsync(string dir, string modelId, CancellationToken ct)
    {
        var manifest = new
        {
            modelId,
            dimensions = RecoModelCatalog.EmbeddingDimensions,
            maxTokens = RecoModelCatalog.MaxTokens,
            pooling = "mean",
            onnxSha256 = RecoModelCatalog.DefaultOnnxSha256,
        };
        var path = Path.Combine(dir, RecoModelCatalog.ManifestFileName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions), ct);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw);
        }
        catch (JsonException)
        {
            return int.TryParse(raw.Trim('"'), out var n) ? n : fallback;
        }
    }
}
