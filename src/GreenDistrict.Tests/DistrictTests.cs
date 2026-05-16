using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.World;

namespace GreenDistrict.Tests;

public class DistrictSystemTests
{
    [Fact]
    public void District_Aggregates_Correctly()
    {
        var world = new WorldState();
        var d1 = new District("North") { Id = 1 };
        var d2 = new District("South") { Id = 2 };
        world.Districts.Add(d1);
        world.Districts.Add(d2);

        var c1 = new Citizen("A", 30, "Worker") { DistrictId = 1, Satisfaction = 80f };
        var c2 = new Citizen("B", 25, "Worker") { DistrictId = 1, Satisfaction = 60f };
        var c3 = new Citizen("C", 40, "Worker") { DistrictId = 2, Satisfaction = 50f };
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);
        world.Citizens.Add(c3);

        var b1 = new Business("Farm 1", "farm", 10) { DistrictId = 1, Revenue = 1000f, Expenses = 200f };
        var b2 = new Business("Shop 1", "shop", 5) { DistrictId = 2, Revenue = 200f, Expenses = 150f };
        world.Businesses.Add(b1);
        world.Businesses.Add(b2);

        var system = new DistrictSystem();
        system.UpdateDistrictAggregates(world);

        Assert.Equal(2, d1.Population);
        Assert.Equal(1, d2.Population);
        Assert.Equal(70f, d1.AverageSatisfaction);
        Assert.Equal(50f, d2.AverageSatisfaction);

        // b1 profit = 800 -> avgProfit=800 -> economic = (800/1000)*100 +50 = 130 -> clamped to 100
        Assert.Equal(100f, d1.EconomicLevel);
        // b2 profit = 50 -> economic = (50/1000)*100 +50 = 55
        Assert.InRange(d2.EconomicLevel, 54.9f, 55.1f);
    }
}
