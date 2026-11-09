namespace Pickarr.Core.Domain.Entities;

public sealed class TitleCredit
{
    public int TitleId { get; set; }
    public int PersonId { get; set; }
    public required string Role { get; set; }
    public int Order { get; set; }

    public Title? Title { get; set; }
    public Person? Person { get; set; }
}
