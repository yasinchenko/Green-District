using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Needs;
using System;

namespace GreenDistrict.Tests;

public class NeedsTests
{
    [Fact]
    public void Needs_Decrease_After_UpdateTick()
    {
        var world = new WorldState();
        var citizen = new Citizen("Test", 30, "Worker")
        {
            FoodSatisfaction = 100f,
            HousingSatisfaction = 100f,
            SafetySatisfaction = 100f,
            HealthcareSatisfaction = 100f,
            EntertainmentSatisfaction = 100f
        };
        world.Citizens.Add(citizen);

        var needs = new NeedsSystem(foodDecayPerTick: 5f);
        needs.UpdateTick(world);

        Assert.Equal(95f, citizen.FoodSatisfaction);
        var expectedHousing = 100f - 0.002f;
        Assert.InRange(citizen.HousingSatisfaction, expectedHousing - 0.0005f, expectedHousing + 0.0005f);
    }

    [Fact]
    public void Satisfaction_Recalculates_Correctly()
    {
        var world = new WorldState();
        var citizen = new Citizen("Test", 30, "Worker")
        {
            FoodSatisfaction = 80f,
            HousingSatisfaction = 60f,
            SafetySatisfaction = 40f,
            HealthcareSatisfaction = 20f,
            EntertainmentSatisfaction = 0f
        };
        world.Citizens.Add(citizen);

        var needs = new NeedsSystem(foodDecayPerTick: 0f, housingDecayPerTick: 0f, safetyDecayPerTick: 0f, healthcareDecayPerTick: 0f, entertainmentDecayPerTick: 0f);
        needs.UpdateTick(world);

        // Average of (80,60,40,20,0) = 40
        Assert.Equal(40f, citizen.Satisfaction);
    }

    [Fact]
    public void Low_Food_Causes_Health_Decay()
    {
        var world = new WorldState();
        var citizen = new Citizen("Test", 30, "Worker") { Health = 100f };
        citizen.FoodSatisfaction = 20f; // poor food
        citizen.HousingSatisfaction = 100f;
        citizen.SafetySatisfaction = 100f;
        citizen.HealthcareSatisfaction = 100f;
        citizen.EntertainmentSatisfaction = 100f;

        world.Citizens.Add(citizen);

        var needs = new NeedsSystem(foodDecayPerTick: 0f);
        needs.UpdateTick(world);

        // UpdateMood reduces health by 1 when FoodSatisfaction < 30
        Assert.True(citizen.Health < 100f);
    }
}
