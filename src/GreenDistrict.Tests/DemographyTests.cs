using System;
using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Demography;

public class DemographyTests
{
    [Fact]
    public void Aging_Happens_After_Year_Ticks()
    {
        var world = new WorldState();
        world.Citizens.Add(new Citizen("Alice", 20, "Worker"));
        var dem = new DemographySystem(ticksPerYear: 10, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(123));
        for (int i = 0; i < 10; i++) world.Clock.Tick();
        dem.UpdateTick(world);
        Assert.True(world.Citizens[0].Age >= 21);
    }

    [Fact]
    public void Births_Occur_When_Rate_High()
    {
        var world = new WorldState();
        world.Citizens.Add(new Citizen("Parent", 25, "Worker") { Gender = Gender.Female });
        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 1.0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(42));
        dem.UpdateTick(world);
        // With birth rate 1.0 per person per year and ticksPerYear=1, expect at least one new citizen
        Assert.True(world.Citizens.Count >= 2);
    }
}
