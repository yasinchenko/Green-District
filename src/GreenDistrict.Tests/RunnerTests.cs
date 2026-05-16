using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;
using Xunit;

namespace GreenDistrict.Tests;

public class RunnerTests
{
    [Fact]
    public void RunYears_Advances_World_And_Returns_Summary()
    {
        var world = new WorldState();
        var scenario = WorldScenarioLoader.CreateDefault();
        scenario.DemographyTicksPerYear = 2;
        world.Initialize(scenario);

        var summary = SimulationRunner.RunYears(world, yearsToRun: 2, ticksPerYear: 2);

        Assert.Equal(4, world.Clock.CurrentTick);
        Assert.Equal(2, summary.YearsRun);
        Assert.Equal(4, summary.TicksRun);
        Assert.Equal(world.Clock.CurrentTick, summary.FinalTick);
        Assert.Equal(world.GetTotalPopulation(), summary.Population);
        Assert.Equal(world.Budget, summary.Budget);
        Assert.True(summary.AverageSatisfaction >= 0f);
        Assert.True(summary.UnemploymentRate >= 0f);
    }

    [Fact]
    public void RunYearSeries_Uses_Fresh_World_For_Each_Duration()
    {
        var runs = SimulationRunner.RunYearSeries(
            () =>
            {
                var world = new WorldState();
                var scenario = WorldScenarioLoader.CreateDefault();
                scenario.DemographyTicksPerYear = 1;
                world.Initialize(scenario);
                return world;
            },
            new[] { 1, 3 },
            ticksPerYear: 1);

        Assert.Equal(new[] { 1, 3 }, runs.Select(r => r.YearsRun).ToArray());
        Assert.Equal(new long[] { 1, 3 }, runs.Select(r => r.FinalTick).ToArray());
    }

    [Fact]
    public void RunYears_Rejects_Invalid_Arguments()
    {
        var world = new WorldState();

        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationRunner.RunYears(world, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationRunner.RunYears(world, 1, 0));
    }
}
