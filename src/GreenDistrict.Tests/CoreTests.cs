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
        
        world.Citizens.Add(alice);
        world.Citizens.Add(bob);
        
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
