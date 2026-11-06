using System.Security.Cryptography;
using System.Text;

namespace PickArr.Core.Recommendations;

/// <summary>Embedding input text and SHA-256 TextHash (BR-REC-09, §13.2).</summary>
public static class RecoEmbeddingText
{
    public const int MaxKeywords = 15;

    public static string Build(
        string name,
        int? year,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> keywords,
        string? tagline,
        string? overview)
    {
        var sb = new StringBuilder();
        sb.Append(name.Trim());
        if (year is { } y)
        {
            sb.Append(" (").Append(y).Append(')');
        }

        sb.Append('.');

        if (genres.Count > 0)
        {
            sb.Append(' ').Append(string.Join(", ", genres.Where(g => !string.IsNullOrWhiteSpace(g))));
            sb.Append('.');
        }

        var kw = keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Take(MaxKeywords).ToList();
        if (kw.Count > 0)
        {
            sb.Append(" Keywords: ").Append(string.Join(", ", kw)).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(tagline))
        {
            sb.Append(' ').Append(tagline.Trim());
            if (!tagline.TrimEnd().EndsWith('.'))
            {
                sb.Append('.');
            }
        }

        if (!string.IsNullOrWhiteSpace(overview))
        {
            sb.Append(' ').Append(overview.Trim());
        }

        return sb.ToString().Trim();
    }

    public static string Sha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
