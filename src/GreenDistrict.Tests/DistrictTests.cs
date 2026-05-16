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

        var c1 = new Citizen("A", 30, "Worker")
        {
            DistrictId = 1,
            Satisfaction = 80f,
            HousingSatisfaction = 90f,
            SafetySatisfaction = 80f,
            HealthcareSatisfaction = 60f,
            EntertainmentSatisfaction = 40f
        };
        var c2 = new Citizen("B", 25, "Worker")
        {
            DistrictId = 1,
            Satisfaction = 60f,
            HousingSatisfaction = 70f,
            SafetySatisfaction = 60f,
            HealthcareSatisfaction = 80f,
            EntertainmentSatisfaction = 60f
        };
        var c3 = new Citizen("C", 40, "Worker")
        {
            DistrictId = 2,
            Satisfaction = 50f,
            HousingSatisfaction = 40f,
            SafetySatisfaction = 30f,
            HealthcareSatisfaction = 50f,
            EntertainmentSatisfaction = 30f
        };
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);
        world.Citizens.Add(c3);

        var b1 = new Business("Farm 1", "farm", 10) { DistrictId = 1, Revenue = 1000f, Expenses = 200f };
        var b2 = new Business("Shop 1", "shop", 5) { DistrictId = 2, Revenue = 200f, Expenses = 150f };
        world.Businesses.Add(b1);
        world.Businesses.Add(b2);
        b1.EmployeeIds.Add(c1.Id);
        b1.EmployeeCount = b1.EmployeeIds.Count;
        c1.Job = b1.Name;

        var household = world.CreateHousehold(1, new[] { c1, c2 });
        var northHome = world.AddHousingUnit(1, 1, 3, 20f);
        world.AddHousingUnit(2, 1, 2, 15f);
        world.AddHousingUnit(3, 2, 1, 12f);
        world.AssignHouseholdToHousingUnit(household, northHome);

        world.Projects.Add(new GovernmentProject("Clinic", 100f, 10) { DistrictId = 1, Completed = true });
        world.Projects.Add(new GovernmentProject("Park", 100f, 10) { DistrictId = 1, Completed = false });
        world.Projects.Add(new GovernmentProject("Road", 100f, 10) { DistrictId = 2, Completed = false });

        var system = new DistrictSystem();
        system.UpdateDistrictAggregates(world);

        Assert.Equal(2, d1.Population);
        Assert.Equal(1, d2.Population);
        Assert.Equal(70f, d1.AverageSatisfaction);
        Assert.Equal(50f, d2.AverageSatisfaction);
        Assert.Equal(80f, d1.AverageHousingSatisfaction);
        Assert.Equal(40f, d2.AverageHousingSatisfaction);
        Assert.Equal(70f, d1.AverageSafetySatisfaction);
        Assert.Equal(30f, d2.AverageSafetySatisfaction);
        Assert.Equal(70f, d1.AverageHealthcareSatisfaction);
        Assert.Equal(50f, d1.AverageEntertainmentSatisfaction);
        Assert.Equal(65f, d1.ServiceLevel);
        Assert.Equal(40f, d2.ServiceLevel);
        Assert.Equal(5, d1.HousingCapacity);
        Assert.Equal(1, d1.OccupiedHousing);
        Assert.Equal(1, d1.AvailableHousing);
        Assert.Equal(1, d2.HousingCapacity);
        Assert.Equal(0, d2.OccupiedHousing);
        Assert.Equal(1, d2.AvailableHousing);
        Assert.Equal(10, d1.TotalJobs);
        Assert.Equal(9, d1.OpenJobs);
        Assert.Equal(50f, d1.EmploymentRate);
        Assert.Equal(5, d2.TotalJobs);
        Assert.Equal(5, d2.OpenJobs);
        Assert.Equal(0f, d2.EmploymentRate);
        Assert.Equal(1, d1.ActiveProjects);
        Assert.Equal(1, d1.CompletedProjects);
        Assert.Equal(1, d2.ActiveProjects);
        Assert.Equal(0, d2.CompletedProjects);

        // b1 profit = 800 -> avgProfit=800 -> economic = (800/1000)*100 +50 = 130 -> clamped to 100
        Assert.Equal(100f, d1.EconomicLevel);
        // b2 profit = 50 -> economic = (50/1000)*100 +50 = 55
        Assert.InRange(d2.EconomicLevel, 54.9f, 55.1f);
    }
}
