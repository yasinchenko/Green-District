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
        world.ConfigureEconomicTickInterval(30);
        world.ConfigureDemography(seed: 42, ticksPerYear: 2, birthRatePerPersonPerYear: 0.3f, baseDeathRatePerPersonPerYear: 0.04f, migrationRatePerPersonPerYear: 0.01f);
        world.Clock.AdvanceTicks(42);
        world.Clock.TimeScale = 2f;

        var district = new District("North") { Id = 7, SupportRating = 58f, HasActiveCrisis = true };
        world.Districts.Add(district);

        var citizen = new Citizen("Alice North", 34, "Worker", Gender.Female)
        {
            DistrictId = 7,
            Cash = 500f,
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
            Cash = 700f,
            Revenue = 1000f,
            Expenses = 300f,
            RevenueThisTick = 120f,
            ExpensesThisTick = 40f,
            TotalRevenue = 1500f,
            TotalExpenses = 500f,
            LastLocalSalesRevenue = 15f,
            LastExternalSalesRevenue = 25f,
            BusinessLevel = 3,
            ProductQuality = 1.4f,
            InvestmentReserve = 275f,
            LastInvestment = 35f,
            ProductionType = "goods"
        };
        business.EmployeeIds.Add(citizen.Id);
        business.EmployeeCount = business.EmployeeIds.Count;
        citizen.Job = business.Name;
        world.Businesses.Add(business);

        var project = GovernmentProject.CreateTyped(ProjectType.Clinic, 7);
        world.Government.StartProject(world, project);
        world.LastConsumerSpending = 42f;
        world.LastExternalInflow = 123f;
        world.LastExternalOutflow = project.Cost;
        world.LastInternalTransfers = 456f;
        world.LastLocalGovernmentSpending = 111f;
        world.LastExternalGovernmentSpending = 222f;

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
        Assert.Equal(30, loaded.EconomicTickInterval);
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
        Assert.Equal(500f, loadedCitizen.Cash);
        Assert.Equal(household.Id, loadedCitizen.HouseholdId);
        Assert.Contains(loadedCitizen.Id, loadedHousehold.MemberIds);
        Assert.Equal(loadedHousehold.Id, loadedHousing.HouseholdId);
        Assert.Contains(loadedCitizen.Id, loadedBusiness.EmployeeIds);
        Assert.Equal(700f + project.LocalCostPaid, loadedBusiness.Cash);
        Assert.Equal(120f + project.LocalCostPaid, loadedBusiness.RevenueThisTick);
        Assert.Equal(40f, loadedBusiness.ExpensesThisTick);
        Assert.Equal(1500f + project.LocalCostPaid, loadedBusiness.TotalRevenue);
        Assert.Equal(500f, loadedBusiness.TotalExpenses);
        Assert.Equal(15f, loadedBusiness.LastLocalSalesRevenue);
        Assert.Equal(25f, loadedBusiness.LastExternalSalesRevenue);
        Assert.Equal(3, loadedBusiness.BusinessLevel);
        Assert.Equal(1.4f, loadedBusiness.ProductQuality);
        Assert.Equal(275f, loadedBusiness.InvestmentReserve);
        Assert.Equal(35f, loadedBusiness.LastInvestment);
        Assert.Equal(42f, loaded.LastConsumerSpending);
        Assert.Equal(123f, loaded.LastExternalInflow);
        Assert.Equal(project.Cost, loaded.LastExternalOutflow);
        Assert.Equal(456f, loaded.LastInternalTransfers);
        Assert.Equal(111f, loaded.LastLocalGovernmentSpending);
        Assert.Equal(222f, loaded.LastExternalGovernmentSpending);
        Assert.Equal(ProjectType.Clinic, loadedProject.Type);
        Assert.Equal(7, loadedProject.DistrictId);
        Assert.Equal(project.LocalCostPaid, loadedProject.LocalCostPaid);
        Assert.Equal(project.ExternalCostPaid, loadedProject.ExternalCostPaid);
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
