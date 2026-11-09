using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pickarr.Infrastructure.Jobs;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class RecoRebuildSchedulerTests
{
    [Fact]
    public void ScheduleDebouncedRebuild_deletes_previous_and_schedules_30s()
    {
        var client = Substitute.For<IBackgroundJobClient>();
        client.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns("job-1", "job-2");

        var scheduler = new RecoRebuildScheduler(client, NullLogger<RecoRebuildScheduler>.Instance);
        scheduler.ScheduleDebouncedRebuild();
        scheduler.ScheduleDebouncedRebuild();

        // First create + delete of first + second create
        client.Received(2).Create(Arg.Any<Job>(), Arg.Any<IState>());
        client.Received(1).ChangeState("job-1", Arg.Any<IState>(), Arg.Any<string>());
    }

    [Fact]
    public void Debounce_delay_is_30_seconds()
    {
        RecoRebuildScheduler.Debounce.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
