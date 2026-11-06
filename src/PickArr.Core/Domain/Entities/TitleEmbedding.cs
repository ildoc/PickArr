namespace PickArr.Core.Domain.Entities;

public sealed class TitleEmbedding
{
    public int TitleId { get; set; }
    public required string ModelId { get; set; }
    public required byte[] Vector { get; set; }
    public required string TextHash { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Title? Title { get; set; }
}
