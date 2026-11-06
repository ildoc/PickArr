using PickArr.Core.Domain;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class RatingValuesTests
{
    [Theory]
    [InlineData("like", true)]
    [InlineData("LIKE", true)]
    [InlineData("dislike", true)]
    [InlineData("nope", false)]
    public void IsValid_accepts_like_dislike(string value, bool expected) =>
        RatingValues.IsValid(value).ShouldBe(expected);

    [Fact]
    public void ToApi_maps_domain_to_lowercase()
    {
        RatingValues.ToApi(RatingValues.Like).ShouldBe("like");
        RatingValues.ToApi(RatingValues.Dislike).ShouldBe("dislike");
        RatingValues.ToApiOrNull(null).ShouldBeNull();
    }
}
