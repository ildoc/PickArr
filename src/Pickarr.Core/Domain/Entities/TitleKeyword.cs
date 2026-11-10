namespace Pickarr.Core.Domain.Entities;

public sealed class TitleKeyword
{
    public int TitleId { get; set; }
    public int KeywordId { get; set; }

    public Title? Title { get; set; }
    public Keyword? Keyword { get; set; }
}
