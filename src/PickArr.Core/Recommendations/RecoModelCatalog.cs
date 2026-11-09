namespace Pickarr.Core.Recommendations;

/// <summary>Known local embedding models (D-REC-11 / SP-R2).</summary>
public static class RecoModelCatalog
{
    public const string DefaultModelId = "all-MiniLM-L6-v2@q8";
    public const int EmbeddingDimensions = 384;
    public const int MaxTokens = 256;

    /// <summary>SHA-256 of Xenova all-MiniLM-L6-v2 onnx/model_quantized.onnx (SP-R2).</summary>
    public const string DefaultOnnxSha256 =
        "AFDB6F1A0E45B715D0BB9B11772F032C399BABD23BFC31FED1C170AFC848BDB1";

    public const string DefaultOnnxFileName = "model.onnx";
    public const string VocabFileName = "vocab.txt";
    public const string ManifestFileName = "model.json";

    public static string DefaultOnnxUrl { get; } =
        "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx";

    public static string DefaultVocabUrl { get; } =
        "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    public static readonly string[] SelfTestPhrases =
    [
        "A science fiction thriller about time travel.",
        "A romantic comedy set in Paris.",
        "A documentary about deep ocean creatures.",
    ];
}

public static class RecoModelStatuses
{
    public const string Absent = "Absent";
    public const string Downloading = "Downloading";
    public const string Ready = "Ready";
    public const string Error = "Error";
}
