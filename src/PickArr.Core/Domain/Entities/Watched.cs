namespace Pickarr.Core.Domain.Entities;

public sealed class Watched
{
    public int TitleId { get; set; }
    public DateTime WatchedAt { get; set; }

    public Title? Title { get; set; }
}
