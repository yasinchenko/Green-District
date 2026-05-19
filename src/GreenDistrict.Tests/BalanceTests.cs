using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;
using Xunit;

namespace GreenDistrict.Tests;

public class BalanceTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void DefaultScenario_Start_Has_Consistent_Economic_State()
    {
        var world = CreateDefaultWorld();
        var trackedMoney = GetTrackedMoney(world);

        Assert.Equal(50, world.GetTotalPopulation());
        Assert.NotEmpty(world.Businesses);
        Assert.All(world.Businesses, business => Assert.Equal(BusinessStatus.Active, business.Status));
        Assert.True(trackedMoney > 0f);
        Assert.True(float.IsFinite(trackedMoney));
        Assert.True(world.Budget > 0f);
    }

    [Fact]
    public void DefaultScenario_MinuteScaleRun_DoesNotImmediatelyCollapsePopulation()
    {
        var world = CreateDefaultWorld();
        var initialPopulation = world.GetTotalPopulation();

        SimulationRunner.Run(world, ticksToRun: 20 * 60);

        Assert.Equal(initialPopulation, world.GetTotalPopulation());
        Assert.DoesNotContain(world.Events, e => e.Title.StartsWith("Death:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public void DefaultScenario_First_Periods_Keep_Economy_Alive(int days)
    {
        var world = CreateDefaultWorld();
        var initialPopulation = world.GetTotalPopulation();
        var initialBusinesses = world.Businesses.Count;

        SimulationRunner.Run(world, ticksToRun: days * TicksPerDay);

        Assert.True(world.GetTotalPopulation() > 0);
        Assert.True(world.GetTotalPopulation() <= initialPopulation * 2);
        Assert.Equal(initialBusinesses, world.Businesses.Count);
        Assert.True(world.Businesses.Count(business => business.Status == BusinessStatus.Active) >= 1);
        Assert.True(float.IsFinite(world.Budget));
        Assert.InRange(world.Budget, -100_000f, 5_000_000f);
        Assert.InRange(world.GetAverageSatisfaction(), 25f, 100f);
    }

    [Fact]
    public void DefaultScenario_MoneyGrowth_Is_Explained_By_External_Flows()
    {
        var world = CreateDefaultWorld();
        var initialTrackedMoney = GetTrackedMoney(world);
        var externalInflow = 0f;
        var externalOutflow = 0f;

        SimulationRunner.Run(
            world,
            ticksToRun: 30 * TicksPerDay,
            perTick: tickWorld =>
            {
                externalInflow += tickWorld.LastExternalInflow;
                externalOutflow += tickWorld.LastExternalOutflow;
            });

        var finalTrackedMoney = GetTrackedMoney(world);
        var moneyGrowth = finalTrackedMoney - initialTrackedMoney;

        Assert.True(float.IsFinite(finalTrackedMoney));
        Assert.True(externalInflow >= 0f);
        Assert.True(externalOutflow >= 0f);
        Assert.True(moneyGrowth <= externalInflow + 0.01f);
        Assert.True(finalTrackedMoney >= initialTrackedMoney - externalOutflow - 0.01f);
        Assert.True(world.LastConsumerSpending >= 0f);
    }

    [Fact]
    public void DefaultScenario_HeadlessRuns_Stay_Within_Balance_Bounds()
    {
        var initialWorld = CreateBalancedDefaultWorld();
        var initialPopulation = initialWorld.GetTotalPopulation();
        var initialBusinesses = initialWorld.Businesses.Count;

        var runs = SimulationRunner.RunYearSeries(
            CreateBalancedDefaultWorld,
            new[] { 1, 10, 50 },
            ticksPerYear: 52);

        Assert.Equal(new[] { 1, 10, 50 }, runs.Select(r => r.YearsRun).ToArray());

        foreach (var run in runs)
        {
            Assert.True(run.Population > 0, $"Population collapsed after {run.YearsRun} years.");
            Assert.True(run.Population <= initialPopulation * 5, $"Population exploded after {run.YearsRun} years.");

            Assert.True(float.IsFinite(run.Budget), $"Budget became non-finite after {run.YearsRun} years.");
            Assert.InRange(run.Budget, -100_000f, 5_000_000f);

            Assert.Equal(initialBusinesses, run.Businesses);
            Assert.Equal(initialBusinesses, run.ActiveBusinesses);
            Assert.Empty(run.ClosedBusinessNames);
            Assert.All(run.BusinessSummaries, b => Assert.Equal("Active", b.Status));

            Assert.True(float.IsFinite(run.AverageSatisfaction), $"Satisfaction became non-finite after {run.YearsRun} years.");
            Assert.InRange(run.AverageSatisfaction, 30f, 100f);
            Assert.True(float.IsFinite(run.UnemploymentRate), $"Unemployment became non-finite after {run.YearsRun} years.");
            Assert.InRange(run.UnemploymentRate, 0f, 100f);
        }
    }

    private static WorldState CreateBalancedDefaultWorld()
    {
        var scenario = WorldScenarioLoader.CreateDefault();
        scenario.Seed = 42;
        scenario.EconomicTickInterval = 1;
        scenario.DemographyTicksPerYear = 52;

        var world = new WorldState(scenario.Seed);
        world.Initialize(scenario);
        return world;
    }

    private static WorldState CreateDefaultWorld()
    {
        var scenario = WorldScenarioLoader.CreateDefault();
        scenario.Seed = 42;

        var world = new WorldState(scenario.Seed);
        world.Initialize(scenario);
        return world;
    }

    private static float GetTrackedMoney(WorldState world)
    {
        return world.Budget +
               world.Citizens.Sum(citizen => citizen.Cash) +
               world.Businesses.Sum(business => business.Cash + business.InvestmentReserve);
    }
}
