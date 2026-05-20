using System;
using System.Linq;
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

    [Fact]
    public void Birth_Assigns_Baby_To_Mothers_Household_And_District()
    {
        var world = new WorldState();
        var mother = new Citizen("Parent", 25, "Worker", Gender.Female) { DistrictId = 7 };
        world.Citizens.Add(mother);

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 1.0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(7));
        dem.UpdateTick(world);

        var baby = world.Citizens.Single(c => c.Age == 0);
        Assert.NotEqual("Baby", baby.Name);
        Assert.Equal("Child", baby.Profession);
        Assert.Equal(LifeStage.Child, baby.LifeStage);
        Assert.Equal(EmploymentStatus.Student, baby.EmploymentStatus);
        Assert.Equal(7, baby.DistrictId);
        Assert.NotEqual(Gender.Other, baby.Gender);
        Assert.Equal(mother.Id, baby.MotherId);
        Assert.Null(baby.FatherId);
        Assert.Equal(mother.HouseholdId, baby.HouseholdId);
        Assert.True(mother.HouseholdId.HasValue);

        var household = world.GetHousehold(mother.HouseholdId.Value);
        Assert.NotNull(household);
        Assert.Contains(mother.Id, household.MemberIds);
        Assert.Contains(baby.Id, household.MemberIds);
    }

    [Fact]
    public void Birth_Assigns_Second_Parent_From_Mothers_Household()
    {
        var world = new WorldState();
        var mother = new Citizen("Maria Green", 29, "Worker", Gender.Female) { DistrictId = 3 };
        var father = new Citizen("Ivan Green", 31, "Worker", Gender.Male) { DistrictId = 3 };
        world.Citizens.Add(mother);
        world.Citizens.Add(father);
        world.CreateHousehold(3, new[] { mother, father });

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 1.0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(11));
        dem.UpdateTick(world);

        var baby = world.Citizens.Single(c => c.Age == 0);
        Assert.Equal(mother.Id, baby.MotherId);
        Assert.Equal(father.Id, baby.FatherId);
        Assert.Equal("Green", baby.FamilyName);
        Assert.EndsWith(" Green", baby.Name);
        Assert.Equal(mother.HouseholdId, baby.HouseholdId);
    }

    [Fact]
    public void Birth_Uses_Mothers_FamilyName_When_No_Second_Parent()
    {
        var world = new WorldState();
        var mother = new Citizen("Elena River", 28, "Worker", Gender.Female) { DistrictId = 4 };
        world.Citizens.Add(mother);

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 1.0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(13));
        dem.UpdateTick(world);

        var baby = world.Citizens.Single(c => c.Age == 0);
        Assert.Equal(mother.Id, baby.MotherId);
        Assert.Null(baby.FatherId);
        Assert.Equal("River", baby.FamilyName);
        Assert.EndsWith(" River", baby.Name);
    }

    [Fact]
    public void Death_Removes_Citizen_From_Household_And_Business()
    {
        var world = new WorldState();
        var business = new Business("Clinic", "clinic", 3);
        var citizen = new Citizen("Sick", 40, "Doctor")
        {
            DistrictId = 1,
            Health = 0f,
            Job = business.Name
        };
        world.Businesses.Add(business);
        world.Citizens.Add(citizen);
        world.CreateHousehold(1, new[] { citizen });
        business.EmployeeIds.Add(citizen.Id);
        business.EmployeeCount = business.EmployeeIds.Count;

        var dem = new DemographySystem(ticksPerYear: 100, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, rng: new Random(1));
        dem.UpdateTick(world);

        Assert.DoesNotContain(citizen, world.Citizens);
        Assert.Empty(world.Households);
        Assert.DoesNotContain(citizen.Id, business.EmployeeIds);
        Assert.Equal(0, business.EmployeeCount);
    }

    [Fact]
    public void Migration_Moves_Household_Members_Together()
    {
        var world = new WorldState();
        world.Districts.Add(new District("North") { Id = 1 });
        world.Districts.Add(new District("South") { Id = 2 });

        var adult = new Citizen("Adult", 30, "Worker", Gender.Male) { DistrictId = 1 };
        var child = new Citizen("Child", 5, "Child", Gender.Female) { DistrictId = 1 };
        world.Citizens.Add(adult);
        world.Citizens.Add(child);
        var household = world.CreateHousehold(1, new[] { adult, child });

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, migrationRatePerPersonPerYear: 1f, rng: new Random(2));
        dem.UpdateTick(world);

        Assert.Equal(household.DistrictId, adult.DistrictId);
        Assert.Equal(household.DistrictId, child.DistrictId);
        Assert.Contains(household.DistrictId, world.Districts.Select(d => (int?)d.Id));
    }

    [Fact]
    public void Migration_Prefers_District_With_Jobs_Safety_And_Available_Housing()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Strained") { Id = 1 });
        world.Districts.Add(new District("Opportunity") { Id = 2 });

        var adult = new Citizen("Adult", 30, "Worker", Gender.Male)
        {
            DistrictId = 1,
            Satisfaction = 35f,
            SafetySatisfaction = 10f
        };
        var child = new Citizen("Child", 5, "Child", Gender.Female)
        {
            DistrictId = 1,
            Satisfaction = 35f,
            SafetySatisfaction = 10f
        };
        world.Citizens.Add(adult);
        world.Citizens.Add(child);

        var household = world.CreateHousehold(1, new[] { adult, child });
        var oldHome = world.AddHousingUnit(1, 1, 2, 30f);
        var newHome = world.AddHousingUnit(2, 2, 3, 15f);
        world.AssignHouseholdToHousingUnit(household, oldHome);
        world.Businesses.Add(new Business("Factory", "factory", 5) { DistrictId = 2 });

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, migrationRatePerPersonPerYear: 1f, rng: new Random(3));
        dem.UpdateTick(world);

        Assert.Equal(2, household.DistrictId);
        Assert.Equal(2, adult.DistrictId);
        Assert.Equal(2, child.DistrictId);
        Assert.Null(oldHome.HouseholdId);
        Assert.Equal(household.Id, newHome.HouseholdId);
        Assert.Equal(2, household.HousingUnitId);
    }

    [Fact]
    public void Migration_Does_Not_Abandon_Housing_When_Target_Has_No_Available_Home()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Home") { Id = 1 });
        world.Districts.Add(new District("Jobs") { Id = 2 });

        var adult = new Citizen("Adult", 30, "Worker", Gender.Male)
        {
            DistrictId = 1,
            Satisfaction = 30f,
            SafetySatisfaction = 10f
        };
        world.Citizens.Add(adult);
        var household = world.CreateHousehold(1, new[] { adult });
        var home = world.AddHousingUnit(1, 1, 1, 20f);
        world.AssignHouseholdToHousingUnit(household, home);
        world.Businesses.Add(new Business("Factory", "factory", 20) { DistrictId = 2 });

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, migrationRatePerPersonPerYear: 1f, rng: new Random(4));
        dem.UpdateTick(world);

        Assert.Equal(1, household.DistrictId);
        Assert.Equal(1, adult.DistrictId);
        Assert.Equal(household.Id, home.HouseholdId);
        Assert.Equal(1, household.HousingUnitId);
    }

    [Fact]
    public void Migration_Lone_Unemployed_Citizen_Prefers_District_With_Open_Jobs()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Quiet") { Id = 1 });
        world.Districts.Add(new District("Industrial") { Id = 2 });

        var citizen = new Citizen("Worker", 30, "Worker", Gender.Female)
        {
            DistrictId = 1,
            Satisfaction = 35f,
            SafetySatisfaction = 20f
        };
        world.Citizens.Add(citizen);
        world.Businesses.Add(new Business("Plant", "factory", 10) { DistrictId = 2 });

        var dem = new DemographySystem(ticksPerYear: 1, birthRatePerPersonPerYear: 0f, baseDeathRatePerPersonPerYear: 0f, migrationRatePerPersonPerYear: 1f, rng: new Random(5));
        dem.UpdateTick(world);

        Assert.Equal(2, citizen.DistrictId);
    }

    [Fact]
    public void ExternalMigration_Adds_Citizen_With_Savings_When_Jobs_And_Housing_Are_Available()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Opportunity") { Id = 1, AverageSafetySatisfaction = 70f });
        world.AddHousingUnit(1, 1, 1, 15f);
        world.Businesses.Add(new Business("Workshop", "workshop", 2) { DistrictId = 1 });

        var dem = new DemographySystem(
            ticksPerYear: 1,
            birthRatePerPersonPerYear: 0f,
            baseDeathRatePerPersonPerYear: 0f,
            migrationRatePerPersonPerYear: 1f,
            rng: new Random(1));

        dem.UpdateTick(world);

        var migrant = Assert.Single(world.Citizens);
        Assert.Equal(1, migrant.DistrictId);
        Assert.True(migrant.Cash > 0f);
        Assert.Equal(migrant.Cash, world.LastExternalInflow, precision: 3);
        Assert.Contains(world.Events, gameEvent => gameEvent.Title.StartsWith("Migration:", StringComparison.Ordinal));
        Assert.True(migrant.HouseholdId.HasValue);
        Assert.True(world.HousingUnits[0].IsOccupied);
    }
}
