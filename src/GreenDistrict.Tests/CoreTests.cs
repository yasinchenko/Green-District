using System;
using System.Collections.Generic;
using Xunit;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Tests;

public class SimulationClockTests
{
    [Fact]
    public void Clock_StartsAtZero()
    {
        var clock = new SimulationClock();
        
        Assert.Equal(0, clock.CurrentTick);
        Assert.Equal(0, clock.Hour);
        Assert.Equal(0, clock.Minute);
        Assert.Equal(0, clock.Day);
    }
    
    [Fact]
    public void Clock_IncrementsOnTick()
    {
        var clock = new SimulationClock();
        
        clock.Tick();
        Assert.Equal(1, clock.CurrentTick);
        
        clock.Tick();
        Assert.Equal(2, clock.CurrentTick);
    }
    
    [Fact]
    public void Clock_CalculatesTimeCorrectly()
    {
        var clock = new SimulationClock();
        
        // Advance to 1 hour (60 ticks)
        clock.AdvanceTicks(60);
        Assert.Equal(1, clock.Hour);
        Assert.Equal(0, clock.Minute);
        
        // Advance to 1 hour 30 minutes
        clock.AdvanceTicks(30);
        Assert.Equal(1, clock.Hour);
        Assert.Equal(30, clock.Minute);
        
        // Advance to next day (1440 ticks)
        clock.AdvanceTicks(1380);
        Assert.Equal(1, clock.Day);
        Assert.Equal(0, clock.Hour);
    }
    
    [Fact]
    public void Clock_FormatsTimeString()
    {
        var clock = new SimulationClock();
        
        Assert.Equal("00:00", clock.GetTimeString());
        
        clock.AdvanceTicks(65); // 1h 5m
        Assert.Equal("01:05", clock.GetTimeString());
    }
    
    [Fact]
    public void Clock_TimeScaleCanBeSet()
    {
        var clock = new SimulationClock();
        
        clock.TimeScale = 2.0f;
        Assert.Equal(2.0f, clock.TimeScale);
        
        // Negative values should be clamped
        clock.TimeScale = -1.0f;
        Assert.Equal(0.1f, clock.TimeScale);
    }
    
    [Fact]
    public void Clock_CanReset()
    {
        var clock = new SimulationClock();
        clock.AdvanceTicks(500);
        clock.TimeScale = 2.0f;
        
        clock.Reset();
        
        Assert.Equal(0, clock.CurrentTick);
        Assert.Equal(1.0f, clock.TimeScale);
    }
}

public class UpdateManagerTests
{
    [Fact]
    public void UpdateManager_RegistersHandlers()
    {
        var manager = new UpdateManager();
        int callCount = 0;
        
        manager.Register(UpdatePhase.TimeUpdate, () => callCount++);
        manager.ExecutePhase(UpdatePhase.TimeUpdate);
        
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void UpdateManager_ExecutesMultipleHandlersInRegistrationOrder()
    {
        var manager = new UpdateManager();
        var calls = new List<int>();

        manager.Register(UpdatePhase.TimeUpdate, () => calls.Add(1));
        manager.Register(UpdatePhase.TimeUpdate, () => calls.Add(2));
        manager.Register(UpdatePhase.TimeUpdate, () => calls.Add(3));

        manager.ExecutePhase(UpdatePhase.TimeUpdate);

        Assert.Equal(new[] { 1, 2, 3 }, calls);
        Assert.Equal(new[] { UpdatePhase.TimeUpdate }, manager.GetRegisteredPhases());
    }

    [Fact]
    public void UpdateManager_Unregister_RemovesAllHandlersForPhase()
    {
        var manager = new UpdateManager();
        int callCount = 0;

        manager.Register(UpdatePhase.TimeUpdate, () => callCount++);
        manager.Register(UpdatePhase.TimeUpdate, () => callCount++);

        manager.Unregister(UpdatePhase.TimeUpdate);
        manager.ExecutePhase(UpdatePhase.TimeUpdate);

        Assert.Equal(0, callCount);
        Assert.Empty(manager.GetRegisteredPhases());
    }
    
    [Fact]
    public void UpdateManager_ExecutesFullCycle()
    {
        var manager = new UpdateManager();
        var executedPhases = new List<UpdatePhase>();
        
        // Register handlers for all phases
        for (int i = 0; i <= 11; i++)
        {
            var phase = (UpdatePhase)i;
            manager.Register(phase, () => executedPhases.Add(phase));
        }
        
        manager.ExecuteFullCycle();
        
        // All 12 phases should be executed
        Assert.Equal(12, executedPhases.Count);
        
        // They should be in order
        for (int i = 0; i < executedPhases.Count; i++)
        {
            Assert.Equal((int)executedPhases[i], i);
        }
    }
}

public class WorldStateTests
{
    [Fact]
    public void WorldState_InitializesWithDefaults()
    {
        var world = new WorldState();
        
        Assert.Equal(0, world.Clock.CurrentTick);
        Assert.Equal(10000f, world.Budget);
        Assert.Equal(75f, world.SupportRating);
        Assert.True(world.IsInPower);
        Assert.Empty(world.Citizens);
    }
    
    [Fact]
    public void WorldState_AddsAndRetrievesCitizens()
    {
        var world = new WorldState();
        var citizen = new Citizen("Alice", 30, "Farmer");
        
        world.Citizens.Add(citizen);
        
        Assert.Single(world.Citizens);
        Assert.Equal("Alice", world.GetCitizen(citizen.Id)?.Name);
    }
    
    [Fact]
    public void WorldState_CalculatesPopulation()
    {
        var world = new WorldState();
        
        world.Citizens.Add(new Citizen("Alice", 30, "Farmer"));
        world.Citizens.Add(new Citizen("Bob", 25, "Worker"));
        world.Citizens.Add(new Citizen("Charlie", 40, "Manager"));
        
        Assert.Equal(3, world.GetTotalPopulation());
    }
    
    [Fact]
    public void WorldState_FiltersCitizensByProfession()
    {
        var world = new WorldState();
        
        world.Citizens.Add(new Citizen("Alice", 30, "Farmer"));
        world.Citizens.Add(new Citizen("Bob", 25, "Farmer"));
        world.Citizens.Add(new Citizen("Charlie", 40, "Worker"));
        
        var farmers = world.GetCitizensByProfession("Farmer");
        
        Assert.Equal(2, farmers.Count);
        Assert.All(farmers, c => Assert.Equal("Farmer", c.Profession));
    }
    
    [Fact]
    public void WorldState_FindsUnemployedCitizens()
    {
        var world = new WorldState();
        
        var alice = new Citizen("Alice", 30, "Farmer");
        var bob = new Citizen("Bob", 25, "Worker") { Job = "Farm1" };
        var child = new Citizen("Charlie", 8, "Child");
        
        world.Citizens.Add(alice);
        world.Citizens.Add(bob);
        world.Citizens.Add(child);
        
        var unemployed = world.GetUnemployedCitizens();
        
        Assert.Single(unemployed);
        Assert.Equal("Alice", unemployed[0].Name);
    }
    
    [Fact]
    public void WorldState_CalculatesAverageSatisfaction()
    {
        var world = new WorldState();
        
        world.Citizens.Add(new Citizen("Alice", 30, "Farmer") { Satisfaction = 100f });
        world.Citizens.Add(new Citizen("Bob", 25, "Worker") { Satisfaction = 50f });
        world.Citizens.Add(new Citizen("Charlie", 40, "Manager") { Satisfaction = 75f });
        
        var average = world.GetAverageSatisfaction();
        
        Assert.Equal(75f, average);
    }

    [Fact]
    public void WorldState_CreateHousehold_Tracks_Members_Housing_And_Income()
    {
        var world = new WorldState();
        var adult = new Citizen("Alice", 30, "Worker") { Income = 1000f };
        var child = new Citizen("Bob", 8, "Child") { Income = 0f };
        world.Citizens.Add(adult);
        world.Citizens.Add(child);

        var household = world.CreateHousehold(
            districtId: 2,
            members: new[] { adult, child },
            housingUnitId: 10,
            housingCapacity: 1,
            rentPerTick: 25f);

        Assert.Equal(2, household.MemberCount);
        Assert.Equal(2, household.DistrictId);
        Assert.Equal(10, household.HousingUnitId);
        Assert.Equal(1, household.HousingCapacity);
        Assert.Equal(25f, household.RentPerTick);
        Assert.True(household.HasHousing);
        Assert.True(household.IsOvercrowded);
        Assert.Equal(1000f, household.TotalIncome);
        Assert.Equal(500f, household.PerCapitaIncome);
        Assert.Equal(household.Id, adult.HouseholdId);
        Assert.Equal(household.Id, child.HouseholdId);
    }

    [Fact]
    public void WorldState_RecalculateHouseholds_Removes_Stale_Members_And_Updates_Income()
    {
        var world = new WorldState();
        var adult = new Citizen("Alice", 30, "Worker") { Income = 1000f };
        var secondAdult = new Citizen("Bob", 32, "Worker") { Income = 500f };
        world.Citizens.Add(adult);
        world.Citizens.Add(secondAdult);
        var household = world.CreateHousehold(1, new[] { adult, secondAdult });

        world.Citizens.Remove(secondAdult);
        adult.Income = 1200f;
        world.RecalculateHouseholds();

        Assert.Single(household.MemberIds);
        Assert.Contains(adult.Id, household.MemberIds);
        Assert.Equal(1200f, household.TotalIncome);
        Assert.Equal(1200f, household.PerCapitaIncome);
    }

    [Fact]
    public void WorldState_AssignHouseholdToHousingUnit_Syncs_Household_Unit_And_District()
    {
        var world = new WorldState();
        var adult = new Citizen("Alice", 30, "Worker") { Income = 1000f };
        world.Citizens.Add(adult);
        var household = world.CreateHousehold(null, new[] { adult });
        var housingUnit = world.AddHousingUnit(77, 3, 2, 40f);

        var assigned = world.AssignHouseholdToHousingUnit(household, housingUnit);

        Assert.True(assigned);
        Assert.Equal(household.Id, housingUnit.HouseholdId);
        Assert.Equal(77, household.HousingUnitId);
        Assert.Equal(2, household.HousingCapacity);
        Assert.Equal(40f, household.RentPerTick);
        Assert.Equal(3, household.DistrictId);
        Assert.Equal(3, adult.DistrictId);
    }

    [Fact]
    public void WorldState_ReleaseHouseholdHousing_Clears_Both_Sides()
    {
        var world = new WorldState();
        var adult = new Citizen("Alice", 30, "Worker");
        world.Citizens.Add(adult);
        var household = world.CreateHousehold(1, new[] { adult });
        var housingUnit = world.AddHousingUnit(12, 1, 2, 15f);
        world.AssignHouseholdToHousingUnit(household, housingUnit);

        world.ReleaseHouseholdHousing(household);

        Assert.Null(housingUnit.HouseholdId);
        Assert.Null(household.HousingUnitId);
        Assert.Equal(0, household.HousingCapacity);
        Assert.Equal(0f, household.RentPerTick);
    }

    [Fact]
    public void PoliticalSupportUpdate_Computes_District_And_Global_Support()
    {
        var world = new WorldState();
        var district = new District("North") { Id = 1 };
        world.Districts.Add(district);

        var employed = new Citizen("A", 30, "Worker")
        {
            DistrictId = 1,
            Satisfaction = 80f,
            SafetySatisfaction = 60f,
            HealthcareSatisfaction = 80f,
            EntertainmentSatisfaction = 60f,
            Job = "Farm"
        };
        var second = new Citizen("B", 30, "Worker")
        {
            DistrictId = 1,
            Satisfaction = 80f,
            SafetySatisfaction = 60f,
            HealthcareSatisfaction = 80f,
            EntertainmentSatisfaction = 60f
        };
        world.Citizens.Add(employed);
        world.Citizens.Add(second);

        world.UpdateManager.ExecutePhase(UpdatePhase.DistrictAggregates);
        world.UpdateManager.ExecutePhase(UpdatePhase.PoliticalSupportUpdate);

        Assert.Equal(67.5f, district.SupportRating);
        Assert.Equal(67.5f, world.SupportRating);
    }

    [Fact]
    public void CrisisProgression_Starts_And_Resolves_District_Crisis()
    {
        var world = new WorldState();
        var district = new District("North")
        {
            Id = 1,
            Population = 10,
            SupportRating = 10f,
            AverageSafetySatisfaction = 10f,
            ServiceLevel = 10f,
            EmploymentRate = 10f
        };
        world.Districts.Add(district);

        world.UpdateManager.ExecutePhase(UpdatePhase.CrisisProgression);
        world.UpdateManager.ExecutePhase(UpdatePhase.CrisisProgression);

        Assert.True(district.HasActiveCrisis);
        Assert.True(district.CrisisRisk >= 35f);
        Assert.Single(world.Events, e => e.Type == EventType.Crisis);

        district.SupportRating = 80f;
        district.AverageSafetySatisfaction = 80f;
        district.ServiceLevel = 80f;
        district.EmploymentRate = 80f;
        world.UpdateManager.ExecutePhase(UpdatePhase.CrisisProgression);

        Assert.False(district.HasActiveCrisis);
        Assert.Contains(world.Events, e => e.Type == EventType.Political && e.Title.Contains("resolved"));
    }

    [Fact]
    public void ResolveEventChoice_Applies_Choice_Effects_Once()
    {
        var world = new WorldState();
        world.Districts.Add(new District("North") { Id = 1 });
        var citizen = new Citizen("A", 30, "Worker")
        {
            DistrictId = 1,
            SafetySatisfaction = 30f,
            HealthcareSatisfaction = 40f
        };
        world.Citizens.Add(citizen);

        var gameEvent = new GameEvent("Budget decision", "Choose a response.", EventType.Decision);
        gameEvent.Choices.Add(new EventChoice("spend", "Spend funds")
        {
            DistrictId = 1,
            BudgetEffect = -100f,
            SupportEffect = 3f,
            SafetySatisfactionEffect = 10f,
            HealthcareSatisfactionEffect = 5f
        });
        world.Events.Add(gameEvent);

        var resolved = world.ResolveEventChoice(gameEvent.Id, "spend");
        var resolvedAgain = world.ResolveEventChoice(gameEvent.Id, "spend");

        Assert.True(resolved);
        Assert.False(resolvedAgain);
        Assert.True(gameEvent.IsResolved);
        Assert.Equal("spend", gameEvent.SelectedChoiceId);
        Assert.Equal(9900f, world.Budget);
        Assert.Equal(78f, world.SupportRating);
        Assert.Equal(40f, citizen.SafetySatisfaction);
        Assert.Equal(45f, citizen.HealthcareSatisfaction);
    }

    [Fact]
    public void CrisisEvent_Has_Choices_And_Funded_Response_Resolves_Crisis()
    {
        var world = new WorldState();
        var district = new District("North")
        {
            Id = 1,
            Population = 5,
            SupportRating = 10f,
            AverageSafetySatisfaction = 10f,
            ServiceLevel = 10f,
            EmploymentRate = 10f
        };
        world.Districts.Add(district);
        world.Citizens.Add(new Citizen("A", 30, "Worker")
        {
            DistrictId = 1,
            SafetySatisfaction = 10f,
            HealthcareSatisfaction = 10f
        });

        world.UpdateManager.ExecutePhase(UpdatePhase.CrisisProgression);
        var crisis = Assert.Single(world.Events, e => e.Type == EventType.Crisis);

        Assert.True(crisis.HasChoices);
        Assert.Contains(crisis.Choices, c => c.Id == "fund-response");

        var resolved = world.ResolveEventChoice(crisis.Id, "fund-response");

        Assert.True(resolved);
        Assert.True(crisis.IsResolved);
        Assert.False(district.HasActiveCrisis);
        Assert.Equal(9500f, world.Budget);
        Assert.True(world.SupportRating > 75f);
    }

    [Fact]
    public void ElectionCheck_Updates_Power_From_Support()
    {
        var world = new WorldState
        {
            ElectionIntervalTicks = 2,
            SupportRating = 49f
        };

        world.Clock.AdvanceTicks(2);
        world.UpdateManager.ExecutePhase(UpdatePhase.ElectionCheck);

        Assert.False(world.IsInPower);
        Assert.Equal(1, world.ElectionCount);
        Assert.Equal(2, world.LastElectionTick);
        Assert.Equal(49f, world.LastElectionSupport);
        Assert.Contains(world.Events, e => e.Type == EventType.Election);
    }
}

public class CitizenTests
{
    [Fact]
    public void Citizen_InitializesWithCorrectData()
    {
        var citizen = new Citizen("Alice", 30, "Farmer");
        
        Assert.Equal("Alice", citizen.Name);
        Assert.Equal(30, citizen.Age);
        Assert.Equal("Farmer", citizen.Profession);
        Assert.Equal(50f, citizen.Satisfaction);
        Assert.Equal(LifeStage.Adult, citizen.LifeStage);
        Assert.Equal(EmploymentStatus.Unemployed, citizen.EmploymentStatus);
    }

    [Fact]
    public void Citizen_Derives_LifeStage_From_Age()
    {
        var child = new Citizen("Child", 8, "Child");
        var student = new Citizen("Student", 16, "Student");
        var adult = new Citizen("Adult", 30, "Worker");

        Assert.Equal(LifeStage.Child, child.LifeStage);
        Assert.Equal(EmploymentStatus.Student, child.EmploymentStatus);
        Assert.Equal(LifeStage.Student, student.LifeStage);
        Assert.Equal(EmploymentStatus.Student, student.EmploymentStatus);
        Assert.Equal(LifeStage.Adult, adult.LifeStage);
        Assert.Equal(EmploymentStatus.Unemployed, adult.EmploymentStatus);
    }

    [Fact]
    public void Citizen_Job_Updates_EmploymentStatus()
    {
        var citizen = new Citizen("Alice", 30, "Farmer");

        citizen.Job = "Farm";
        Assert.Equal(EmploymentStatus.Employed, citizen.EmploymentStatus);

        citizen.Job = null;
        Assert.Equal(EmploymentStatus.Unemployed, citizen.EmploymentStatus);
    }

    [Fact]
    public void Citizen_Retire_Updates_Statuses_And_Clears_Job()
    {
        var citizen = new Citizen("Alice", 65, "Worker") { Job = "Factory" };

        citizen.Retire();

        Assert.True(citizen.IsRetired);
        Assert.Null(citizen.Job);
        Assert.Equal(LifeStage.Retired, citizen.LifeStage);
        Assert.Equal(EmploymentStatus.Retired, citizen.EmploymentStatus);
    }
    
    [Fact]
    public void Citizen_RecalculatesSatisfactionFromNeeds()
    {
        var citizen = new Citizen("Alice", 30, "Farmer");
        
        citizen.FoodSatisfaction = 100f;
        citizen.HousingSatisfaction = 100f;
        citizen.SafetySatisfaction = 100f;
        citizen.HealthcareSatisfaction = 100f;
        citizen.EntertainmentSatisfaction = 100f;
        
        citizen.RecalculateSatisfaction();
        
        Assert.Equal(100f, citizen.Satisfaction);
    }
    
    [Fact]
    public void Citizen_UpdatesMood()
    {
        var citizen = new Citizen("Alice", 30, "Farmer");
        citizen.Satisfaction = 80f;
        
        citizen.UpdateMood();
        
        // Mood should shift towards satisfaction
        Assert.True(citizen.Mood > 50f && citizen.Mood < 80f);
    }
    
    [Fact]
    public void Citizen_HealthDecaysWithoutFood()
    {
        var citizen = new Citizen("Alice", 30, "Farmer") { Health = 100f };
        citizen.FoodSatisfaction = 20f; // Poor food satisfaction
        
        citizen.UpdateMood();
        
        Assert.True(citizen.Health < 100f);
    }
}
