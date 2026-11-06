using System.Diagnostics.CodeAnalysis;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.Logging;
using PickArr.Core.Recommendations;

namespace PickArr.Infrastructure.Metadata.Reco;

/// <summary>Thread-safe ONNX sentence embedder (mean pool + L2).</summary>
public sealed class RecoOnnxEmbedder : IDisposable
{
    private readonly object _gate = new();
    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private int _threads = 2;

    public bool IsReady => _session is not null && _tokenizer is not null;
    public string? ModelId { get; private set; }
    public string? LastError { get; private set; }

    public bool TryLoad(string modelDirectory, string modelId, int threads, ILogger? logger = null)
    {
        lock (_gate)
        {
            DisposeSession_NoLock();
            try
            {
                var onnxPath = Path.Combine(modelDirectory, RecoModelCatalog.DefaultOnnxFileName);
                var vocabPath = Path.Combine(modelDirectory, RecoModelCatalog.VocabFileName);
                if (!File.Exists(onnxPath) || !File.Exists(vocabPath))
                {
                    LastError = "model.onnx or vocab.txt missing";
                    return false;
                }

                _threads = Math.Clamp(threads, 1, 8);
                var options = new SessionOptions
                {
                    IntraOpNumThreads = _threads,
                    InterOpNumThreads = 1,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                };
                _session = new InferenceSession(onnxPath, options);
                _tokenizer = BertTokenizer.Create(vocabPath);
                ModelId = modelId;
                LastError = null;
                logger?.LogInformation("Loaded reco ONNX model {ModelId} from {Path}", modelId, modelDirectory);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                logger?.LogWarning(ex, "Failed to load reco ONNX model from {Path}", modelDirectory);
                DisposeSession_NoLock();
                return false;
            }
        }
    }

    public float[] Embed(string text)
    {
        lock (_gate)
        {
            if (_session is null || _tokenizer is null)
            {
                throw new InvalidOperationException("ONNX embedder is not loaded.");
            }

            return EmbedCore(text, _session, _tokenizer);
        }
    }

    public IReadOnlyList<float[]> EmbedBatch(IReadOnlyList<string> texts)
    {
        lock (_gate)
        {
            if (_session is null || _tokenizer is null)
            {
                throw new InvalidOperationException("ONNX embedder is not loaded.");
            }

            var result = new float[texts.Count][];
            for (var i = 0; i < texts.Count; i++)
            {
                result[i] = EmbedCore(texts[i], _session, _tokenizer);
            }

            return result;
        }
    }

    /// <summary>Self-test: same phrase twice ≥ 0.99; golden tip of phrase0 if provided.</summary>
    public bool RunSelfTest([NotNullWhen(false)] out string? error, float[]? goldenPhrase0 = null)
    {
        try
        {
            var p0 = RecoModelCatalog.SelfTestPhrases[0];
            var a = Embed(p0);
            var b = Embed(p0);
            var self = RecoKnn.Dot(a, b);
            if (self < 0.99f)
            {
                error = $"Self cosine {self:F4} < 0.99";
                return false;
            }

            if (goldenPhrase0 is { Length: > 0 })
            {
                var g = RecoKnn.Dot(a, goldenPhrase0);
                if (g < 0.99f)
                {
                    error = $"Golden cosine {g:F4} < 0.99";
                    return false;
                }
            }

            var other = Embed(RecoModelCatalog.SelfTestPhrases[1]);
            if (RecoKnn.Dot(a, other) > 0.95f)
            {
                error = "Distinct phrases unexpectedly almost identical";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static float[] EmbedCore(string text, InferenceSession session, BertTokenizer tokenizer)
    {
        var ids = tokenizer.EncodeToIds(text, RecoModelCatalog.MaxTokens - 2, out string? _, out _).ToList();
        var withSpecial = tokenizer.BuildInputsWithSpecialTokens(ids).ToList();
        if (withSpecial.Count > RecoModelCatalog.MaxTokens)
        {
            withSpecial = withSpecial.Take(RecoModelCatalog.MaxTokens).ToList();
        }

        var n = withSpecial.Count;
        var inputIds = new DenseTensor<long>([1, n]);
        var attention = new DenseTensor<long>([1, n]);
        var tokenType = new DenseTensor<long>([1, n]);
        for (var i = 0; i < n; i++)
        {
            inputIds[0, i] = withSpecial[i];
            attention[0, i] = 1;
            tokenType[0, i] = 0;
        }

        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenType),
        ]);

        var hidden = results.First().AsEnumerable<float>().ToArray();
        var dim = RecoModelCatalog.EmbeddingDimensions;
        if (hidden.Length < n * dim)
        {
            throw new InvalidOperationException($"Unexpected ONNX output length {hidden.Length} for seq {n}.");
        }

        var pooled = new float[dim];
        for (var t = 0; t < n; t++)
        {
            var offset = t * dim;
            for (var d = 0; d < dim; d++)
            {
                pooled[d] += hidden[offset + d];
            }
        }

        for (var d = 0; d < dim; d++)
        {
            pooled[d] /= n;
        }

        RecoKnn.L2Normalize(pooled);
        return pooled;
    }

    private void DisposeSession_NoLock()
    {
        _session?.Dispose();
        _session = null;
        _tokenizer = null;
        ModelId = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeSession_NoLock();
        }
    }
}
