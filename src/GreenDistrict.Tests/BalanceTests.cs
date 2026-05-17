using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;
using Xunit;

namespace GreenDistrict.Tests;

public class BalanceTests
{
    [Fact]
    public void DefaultScenario_MinuteScaleRun_DoesNotImmediatelyCollapsePopulation()
    {
        var world = CreateDefaultWorld();
        var initialPopulation = world.GetTotalPopulation();

        SimulationRunner.Run(world, ticksToRun: 20 * 60);

        Assert.Equal(initialPopulation, world.GetTotalPopulation());
        Assert.DoesNotContain(world.Events, e => e.Title.StartsWith("Death:", StringComparison.Ordinal));
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
}
