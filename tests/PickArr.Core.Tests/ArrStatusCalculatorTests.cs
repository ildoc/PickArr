using PickArr.Core.Arr;
using PickArr.Core.Domain;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class ArrStatusCalculatorTests
{
    [Theory]
    [InlineData(false, true, ArrState.Downloading)]
    [InlineData(true, false, ArrState.Downloaded)]
    [InlineData(false, false, ArrState.Missing)]
    public void ForMovie_follows_BR09(bool hasFile, bool inQueue, ArrState expected)
    {
        ArrStatusCalculator.ForMovie(hasFile, inQueue).ShouldBe(expected);
    }

    [Fact]
    public void ForSeries_partial_when_some_files()
    {
        var seasons = new List<(ArrSeasonSnapshot, ArrState)>
        {
            (new ArrSeasonSnapshot(1, true, 8, 8, 8, 1), ArrState.Downloaded),
            (new ArrSeasonSnapshot(2, true, 3, 8, 8, 1), ArrState.Partial),
        };

        ArrStatusCalculator.ForSeries(seasons, anyInQueue: false).ShouldBe(ArrState.Partial);
    }

    [Fact]
    public void ForSeason_missing_when_episode_count_zero()
    {
        var season = new ArrSeasonSnapshot(1, true, 0, 0, 10, 0);
        ArrStatusCalculator.ForSeason(season, inQueue: false).ShouldBe(ArrState.Missing);
    }
}
