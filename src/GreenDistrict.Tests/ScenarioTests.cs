using System.IO;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;
using Xunit;

namespace GreenDistrict.Tests;

public class ScenarioTests
{
    [Fact]
    public void WorldState_Initialize_Loads_Default_Scenario()
    {
        var world = new WorldState();

        world.Initialize();

        Assert.Equal(10000f, world.Budget);
        Assert.Equal(75f, world.SupportRating);
        Assert.Equal(2, world.Districts.Count);
        Assert.Equal(2, world.Businesses.Count);
        Assert.Equal(4, world.Citizens.Count);
        Assert.Equal(2, world.Households.Count);
        Assert.Equal(3, world.HousingUnits.Count);
        Assert.Contains(world.Businesses, b => b.Name == "Central Farm" && b.BaseOutput > 0f && b.UnitPrice > 0f);
        Assert.Contains(world.Citizens, c => c.Name == "Maria Green" && c.EmploymentStatus == EmploymentStatus.Employed);
        Assert.Contains(world.Households, h => h.MemberCount == 3 && h.HousingCapacity == 4);
        Assert.Contains(world.HousingUnits, h => h.Id == 1 && h.IsOccupied);
    }

    [Fact]
    public void WorldState_InitializeFromJson_Loads_Citizens_Businesses_And_Households()
    {
        var world = new WorldState();

        world.InitializeFromJson("""
            {
              "budget": 15000,
              "supportRating": 62,
              "districts": [
                { "id": 10, "name": "Old Town" }
              ],
              "businesses": [
                { "id": 20, "name": "Bakery", "type": "shop", "maxEmployees": 1, "districtId": 10, "wagePerEmployee": 450, "productionType": "trade", "baseOutput": 80, "unitPrice": 4, "demandMultiplier": 0.75, "revenue": 2000 }
              ],
              "housingUnits": [
                { "id": 99, "districtId": 10, "capacity": 2, "rentPerTick": 15 }
              ],
              "citizens": [
                { "name": "Nina Stone", "age": 34, "profession": "Trader", "gender": "Female", "districtId": 10, "job": "Bakery", "income": 100 },
                { "name": "Pavel Stone", "age": 11, "profession": "Child", "gender": "Male", "districtId": 10, "job": "Bakery" }
              ],
              "households": [
                { "districtId": 10, "housingUnitId": 99, "housingCapacity": 2, "rentPerTick": 15, "memberNames": [ "Nina Stone", "Pavel Stone" ] }
              ]
            }
            """);

        var bakery = Assert.Single(world.Businesses);
        var adult = world.GetCitizenByName("Nina Stone");
        var child = world.GetCitizenByName("Pavel Stone");
        var household = Assert.Single(world.Households);

        Assert.NotNull(adult);
        Assert.NotNull(child);
        Assert.Equal(15000f, world.Budget);
        Assert.Equal(62f, world.SupportRating);
        Assert.Equal("Bakery", adult.Job);
        Assert.Null(child.Job);
        Assert.Equal("trade", bakery.ProductionType);
        Assert.Equal(80f, bakery.BaseOutput);
        Assert.Equal(4f, bakery.UnitPrice);
        Assert.Single(bakery.EmployeeIds);
        Assert.Contains(adult.Id, bakery.EmployeeIds);
        Assert.Single(world.HousingUnits);
        Assert.Equal(household.Id, world.HousingUnits[0].HouseholdId);
        Assert.Equal(99, household.HousingUnitId);
        Assert.Equal(100f, household.TotalIncome);
    }

    [Fact]
    public void Data_DefaultScenario_File_Is_Loadable()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "data", "scenarios", "default_scenario.json");

        var scenario = WorldScenarioLoader.LoadJsonFile(path);
        var world = new WorldState();
        world.Initialize(scenario);

        Assert.NotEmpty(world.Districts);
        Assert.NotEmpty(world.Businesses);
        Assert.NotEmpty(world.Citizens);
        Assert.NotEmpty(world.Households);
        Assert.NotEmpty(world.HousingUnits);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Codex_plan.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
