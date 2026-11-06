namespace PickArr.Core.Domain.Entities;

public sealed class Keyword
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<TitleKeyword> TitleKeywords { get; set; } = new List<TitleKeyword>();
}
