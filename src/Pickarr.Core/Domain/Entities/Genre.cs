namespace Pickarr.Core.Domain.Entities;

public sealed class Genre
{
    public int Id { get; set; }
    public ICollection<GenreLocalization> Localizations { get; set; } = new List<GenreLocalization>();
    public ICollection<TitleGenre> TitleGenres { get; set; } = new List<TitleGenre>();
}
