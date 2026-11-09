using PickArr.Core.Domain;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class RatingValuesTests
{
    [Theory]
    [InlineData("like", true)]
    [InlineData("LIKE", true)]
    [InlineData("love", true)]
    [InlineData("dislike", true)]
    [InlineData("nope", false)]
    public void IsValid_accepts_love_like_dislike(string value, bool expected) =>
        RatingValues.IsValid(value).ShouldBe(expected);

    [Fact]
    public void Normalize_and_ToApi_round_trip()
    {
        RatingValues.Normalize("love").ShouldBe(RatingValues.Love);
        RatingValues.Normalize("LIKE").ShouldBe(RatingValues.Like);
        RatingValues.Normalize("dislike").ShouldBe(RatingValues.Dislike);
        RatingValues.ToApi(RatingValues.Love).ShouldBe("love");
        RatingValues.ToApi(RatingValues.Like).ShouldBe("like");
        RatingValues.ToApi(RatingValues.Dislike).ShouldBe("dislike");
        RatingValues.ToApiOrNull(null).ShouldBeNull();
    }

    [Fact]
    public void IsPositive_covers_love_and_like()
    {
        RatingValues.IsPositive(RatingValues.Love).ShouldBeTrue();
        RatingValues.IsPositive(RatingValues.Like).ShouldBeTrue();
        RatingValues.IsPositive(RatingValues.Dislike).ShouldBeFalse();
    }
}
