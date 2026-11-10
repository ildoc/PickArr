namespace Pickarr.Core.Domain.Entities;

public sealed class Rating
{
    public int TitleId { get; set; }
    public int ProfileId { get; set; }
    public required string Value { get; set; }
    public DateTime RatedAt { get; set; }

    public Title? Title { get; set; }
    public Profile? Profile { get; set; }
}
