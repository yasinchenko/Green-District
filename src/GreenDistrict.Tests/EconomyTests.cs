using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;

namespace GreenDistrict.Tests;

public class EconomyTests
{
    [Fact]
    public void AssignJobs_Fills_Vacancies()
    {
        var world = new WorldState();
        var business = new Business("Shop", "shop", 2) { Id = 1 };
        world.Businesses.Add(business);

        var c1 = new Citizen("A", 30, "Worker");
        var c2 = new Citizen("B", 25, "Worker");
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);

        var eco = new EconomySystem();
        var assigned = eco.AssignJobs(world);

        Assert.Equal(2, assigned);
        Assert.Equal(2, business.EmployeeIds.Count);
        Assert.Equal("Shop", c1.Job);
    }

    [Fact]
    public void ProcessPayroll_Pays_Wages_And_Taxes()
    {
        var world = new WorldState();
        var business = new Business("Farm", "farm", 1) { Id = 1, WagePerEmployee = 500f, Revenue = 1000f };
        world.Businesses.Add(business);

        var c1 = new Citizen("A", 30, "Farmer");
        world.Citizens.Add(c1);

        // assign manually
        business.EmployeeIds.Add(c1.Id);
        c1.Job = business.Name;

        var eco = new EconomySystem(taxRate: 0.1f);
        eco.ProcessPayroll(world);

        // gross 500, tax 50, net 450
        Assert.Equal(450f, c1.Income);
        Assert.Equal(500f, business.Revenue); // 1000-500 (business paid gross wages)
        // Default starting budget is 10000, tax 50 added -> 10050
        Assert.Equal(10050f, world.Budget);
    }

    [Fact]
    public void GetUnemploymentRate_Computes_Correctly()
    {
        var world = new WorldState();
        var c1 = new Citizen("A", 30, "Worker") { Job = "Shop" };
        var c2 = new Citizen("B", 25, "Worker");
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);

        var eco = new EconomySystem();
        var rate = eco.GetUnemploymentRate(world);

        Assert.Equal(50f, rate);
    }
}
