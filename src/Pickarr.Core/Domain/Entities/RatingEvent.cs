namespace Pickarr.Core.Domain.Entities;

public sealed class RatingEvent
{
    public int Id { get; set; }
    public int TitleId { get; set; }
    public int ProfileId { get; set; }
    public string? Value { get; set; }
    public DateTime At { get; set; }
    public required string Origin { get; set; }

    public Title? Title { get; set; }
    public Profile? Profile { get; set; }
}
