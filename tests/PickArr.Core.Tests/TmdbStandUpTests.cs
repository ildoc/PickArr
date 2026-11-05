using PickArr.Core.Metadata;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class TmdbStandUpTests
{
    [Theory]
    [InlineData(9715, "anything", true)]
    [InlineData(1, "stand-up comedy", true)]
    [InlineData(1, "Stand Up Comedy", true)]
    [InlineData(1, "standup", true)]
    [InlineData(1, "drama", false)]
    public void Detects_stand_up_keywords(int id, string name, bool expected)
    {
        TmdbStandUp.IsStandUpKeyword(id, name).ShouldBe(expected);
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
}
