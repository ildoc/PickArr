using Microsoft.Extensions.Logging.Abstractions;
using PickArr.Core.Recommendations;
using PickArr.Infrastructure.Metadata.Reco;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class RecoOnnxEmbedderTests
{
    [Fact]
    public void Self_test_passes_when_model_files_present()
    {
        var dir = ResolveModelDir();
        if (dir is null)
        {
            return; // conditional skip — no model in environment
        }

        using var embedder = new RecoOnnxEmbedder();
        embedder.TryLoad(dir, RecoModelCatalog.DefaultModelId, threads: 1, NullLogger.Instance)
            .ShouldBeTrue(embedder.LastError);
        embedder.RunSelfTest(out var error).ShouldBeTrue(error);
        var a = embedder.Embed(RecoModelCatalog.SelfTestPhrases[0]);
        a.Length.ShouldBe(RecoModelCatalog.EmbeddingDimensions);
        Math.Sqrt(a.Sum(v => v * v)).ShouldBe(1.0, 1e-3);
    }

    private static string? ResolveModelDir()
    {
        var configured = Environment.GetEnvironmentVariable("PickArr__Models__Path");
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(Path.Combine(configured, RecoModelCatalog.DefaultModelId));
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode", "pickarr-models", RecoModelCatalog.DefaultModelId));

        foreach (var dir in candidates)
        {
            if (File.Exists(Path.Combine(dir, RecoModelCatalog.DefaultOnnxFileName))
                && File.Exists(Path.Combine(dir, RecoModelCatalog.VocabFileName)))
            {
                return dir;
            }
        }

        return null;
    }
}
