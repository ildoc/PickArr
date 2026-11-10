namespace Pickarr.Core.Domain.Entities;

public sealed class Person
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<TitleCredit> TitleCredits { get; set; } = new List<TitleCredit>();
}
