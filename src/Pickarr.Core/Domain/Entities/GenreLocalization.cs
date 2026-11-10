namespace Pickarr.Core.Domain.Entities;

public sealed class GenreLocalization
{
    public int GenreId { get; set; }
    public required string Language { get; set; }
    public required string Name { get; set; }

    public Genre? Genre { get; set; }
}
