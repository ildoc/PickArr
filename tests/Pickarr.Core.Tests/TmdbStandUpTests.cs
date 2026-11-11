using Pickarr.Core.Domain;
using Pickarr.Core.Metadata;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class TmdbStandUpTests
{
    [Theory]
    [InlineData("stand-up comedy", true)]
    [InlineData("Stand Up Comedy", true)]
    [InlineData("standup comedy", true)]
    [InlineData("stand–up comedy", true)]
    [InlineData("standup", false)]
    [InlineData("superhero", false)]
    [InlineData("drama", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Detects_stand_up_keyword_names(string? name, bool expected)
    {
        TmdbStandUp.IsStandUpKeyword(name).ShouldBe(expected);
    }

    [Fact]
    public void ResolveCategory_uses_keyword_names_not_media_type_alone()
    {
        TmdbStandUp.ResolveCategory(MediaTypes.Movie, ["superhero", "based on comic"])
            .ShouldBe(TitleCategories.Movie);
        TmdbStandUp.ResolveCategory(MediaTypes.Movie, ["stand-up comedy"])
            .ShouldBe(TitleCategories.StandUp);
        TmdbStandUp.ResolveCategory(MediaTypes.Series, Array.Empty<string?>())
            .ShouldBe(TitleCategories.Series);
    }

    [Fact]
    public void LanguageFallback_triggers_on_empty_overview_or_title()
    {
        LanguageFallback.NeedsEnglishFallback(null, "x").ShouldBeTrue();
        LanguageFallback.NeedsEnglishFallback("x", "").ShouldBeTrue();
        LanguageFallback.NeedsEnglishFallback("x", "y").ShouldBeFalse();
    }

    [Fact]
    public void TextNormalizer_strips_diacritics()
    {
        TextNormalizer.NormalizeName("Caffè").ShouldBe("caffe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TextNormalizer_empty_inputs_become_empty_string(string? value)
    {
        TextNormalizer.NormalizeName(value).ShouldBe(string.Empty);
    }
}
