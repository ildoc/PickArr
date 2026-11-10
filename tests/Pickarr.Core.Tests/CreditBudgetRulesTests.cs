using Pickarr.Core.Catalog;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class CreditBudgetRulesTests
{
    [Fact]
    public void ComputeReserve_ceils_percent_of_quota()
    {
        CreditBudgetRules.ComputeReserve(2500, 10).ShouldBe(250);
        CreditBudgetRules.ComputeReserve(100, 10).ShouldBe(10);
        CreditBudgetRules.ComputeReserve(15, 10).ShouldBe(2);
    }

    [Fact]
    public void Scheduled_job_cannot_spend_reserve()
    {
        // quota 100, used 80, reserve 10% = 10 → spendable scheduled = 10
        CreditBudgetRules.TryReserve(
            used: 80,
            quota: 100,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 0,
            cost: 10,
            isManual: false).ShouldBeTrue();

        CreditBudgetRules.TryReserve(
            used: 80,
            quota: 100,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 0,
            cost: 11,
            isManual: false).ShouldBeFalse();
    }

    [Fact]
    public void Manual_can_use_reserve()
    {
        // same numbers: spendable manual = 20
        CreditBudgetRules.TryReserve(
            used: 80,
            quota: 100,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 0,
            cost: 20,
            isManual: true).ShouldBeTrue();

        CreditBudgetRules.TryReserve(
            used: 80,
            quota: 100,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 0,
            cost: 21,
            isManual: true).ShouldBeFalse();
    }

    [Fact]
    public void MaxCreditsPerRun_caps_even_when_monthly_allows()
    {
        CreditBudgetRules.TryReserve(
            used: 0,
            quota: 2500,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 79,
            cost: 2,
            isManual: false).ShouldBeFalse();

        CreditBudgetRules.TryReserve(
            used: 0,
            quota: 2500,
            reservePercent: 10,
            maxCreditsPerRun: 80,
            runCreditsUsed: 79,
            cost: 1,
            isManual: false).ShouldBeTrue();
    }

    [Fact]
    public void Zero_cost_always_allowed()
    {
        CreditBudgetRules.TryReserve(2500, 2500, 10, 80, 80, cost: 0, isManual: false).ShouldBeTrue();
    }
}
