namespace Pickarr.Core.Domain.Entities;

public sealed class Favorite
{
    public int TitleId { get; set; }
    public int ProfileId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Title? Title { get; set; }
    public Profile? Profile { get; set; }
}
