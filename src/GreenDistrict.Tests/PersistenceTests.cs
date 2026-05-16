using System.IO;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Persistence;
using Xunit;

namespace GreenDistrict.Tests;

public class PersistenceTests
{
    [Fact]
    public void WorldStateSerializer_RoundTrips_Core_State_And_Relationships()
    {
        var world = new WorldState
        {
            Budget = 12345f,
            SupportRating = 61f,
            IncomeTaxRate = 0.12f,
            BusinessTaxRate = 0.08f,
            ElectionIntervalTicks = 12
        };
        world.ConfigureDemography(seed: 42, ticksPerYear: 2, birthRatePerPersonPerYear: 0.3f, baseDeathRatePerPersonPerYear: 0.04f, migrationRatePerPersonPerYear: 0.01f);
        world.Clock.AdvanceTicks(42);
        world.Clock.TimeScale = 2f;

        var district = new District("North") { Id = 7, SupportRating = 58f, HasActiveCrisis = true };
        world.Districts.Add(district);

        var citizen = new Citizen("Alice North", 34, "Worker", Gender.Female)
        {
            DistrictId = 7,
            Income = 900f,
            FoodSatisfaction = 80f,
            HousingSatisfaction = 70f,
            SafetySatisfaction = 60f,
            HealthcareSatisfaction = 50f,
            EntertainmentSatisfaction = 40f
        };
        citizen.RecalculateSatisfaction();
        world.Citizens.Add(citizen);

        var housing = world.AddHousingUnit(100, 7, 2, 25f);
        var household = world.CreateHousehold(7, new[] { citizen });
        world.AssignHouseholdToHousingUnit(household, housing);

        var business = new Business("Factory", "factory", 3)
        {
            Id = 20,
            DistrictId = 7,
            Revenue = 1000f,
            Expenses = 300f,
            ProductionType = "goods"
        };
        business.EmployeeIds.Add(citizen.Id);
        business.EmployeeCount = business.EmployeeIds.Count;
        citizen.Job = business.Name;
        world.Businesses.Add(business);

        var project = GovernmentProject.CreateTyped(ProjectType.Clinic, 7);
        world.Government.StartProject(world, project);

        var gameEvent = new GameEvent("Crisis", "Choose response.", EventType.Decision);
        gameEvent.Choices.Add(new EventChoice("fund", "Fund response") { BudgetEffect = -100f, DistrictId = 7 });
        world.Events.Add(gameEvent);

        world.DistrictsSystem.UpdateDistrictAggregates(world);

        var json = WorldStateSerializer.SaveJson(world);
        var loaded = WorldStateSerializer.LoadJson(json);

        Assert.Equal(42, loaded.Clock.CurrentTick);
        Assert.Equal(2f, loaded.Clock.TimeScale);
        Assert.Equal(12345f - project.Cost, loaded.Budget);
        Assert.Equal(61f, loaded.SupportRating);
        Assert.Equal(0.12f, loaded.IncomeTaxRate);
        Assert.Equal(12, loaded.ElectionIntervalTicks);
        Assert.Equal(42, loaded.SimulationSeed);
        Assert.Equal(2, loaded.DemographyTicksPerYear);
        Assert.Equal(0.3f, loaded.BirthRatePerPersonPerYear);

        var loadedCitizen = Assert.Single(loaded.Citizens);
        var loadedHousehold = Assert.Single(loaded.Households);
        var loadedHousing = Assert.Single(loaded.HousingUnits);
        var loadedBusiness = Assert.Single(loaded.Businesses);
        var loadedProject = Assert.Single(loaded.Projects);
        var loadedEvent = Assert.Single(loaded.Events);

        Assert.Equal(citizen.Id, loadedCitizen.Id);
        Assert.Equal(household.Id, loadedCitizen.HouseholdId);
        Assert.Contains(loadedCitizen.Id, loadedHousehold.MemberIds);
        Assert.Equal(loadedHousehold.Id, loadedHousing.HouseholdId);
        Assert.Contains(loadedCitizen.Id, loadedBusiness.EmployeeIds);
        Assert.Equal(ProjectType.Clinic, loadedProject.Type);
        Assert.Equal(7, loadedProject.DistrictId);
        Assert.False(loadedProject.Completed);
        Assert.True(loadedEvent.HasChoices);
        Assert.Equal("fund", loadedEvent.Choices[0].Id);
    }

    [Fact]
    public void WorldStateSerializer_Can_Save_And_Load_File()
    {
        var world = new WorldState { Budget = 7777f };
        var path = Path.Combine(Path.GetTempPath(), $"green-district-save-{System.Guid.NewGuid():N}.json");

        try
        {
            WorldStateSerializer.SaveJsonFile(world, path);
            var loaded = WorldStateSerializer.LoadJsonFile(path);

            Assert.Equal(7777f, loaded.Budget);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
