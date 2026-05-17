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

        var needs = new NeedsSystem(foodDecayPerTick: 5f, noHousingPenaltyPerTick: 0f, stableHousingRecoveryPerTick: 0f);
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

        var needs = new NeedsSystem(foodDecayPerTick: 0f, housingDecayPerTick: 0f, safetyDecayPerTick: 0f, healthcareDecayPerTick: 0f, entertainmentDecayPerTick: 0f, noHousingPenaltyPerTick: 0f);
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

        var needs = new NeedsSystem(foodDecayPerTick: 0f, noHousingPenaltyPerTick: 0f, stableHousingRecoveryPerTick: 0f);
        needs.UpdateTick(world);

        // UpdateMood applies minute-scale health decay when food is poor.
        Assert.True(citizen.Health < 100f);
        Assert.True(citizen.Health > 99f);
    }

    [Fact]
    public void HousingSatisfaction_Drops_Faster_Without_Housing()
    {
        var world = new WorldState();
        var citizen = new Citizen("No Home", 30, "Worker")
        {
            HousingSatisfaction = 80f
        };
        world.Citizens.Add(citizen);

        var needs = new NeedsSystem(
            foodDecayPerTick: 0f,
            housingDecayPerTick: 1f,
            safetyDecayPerTick: 0f,
            healthcareDecayPerTick: 0f,
            entertainmentDecayPerTick: 0f,
            noHousingPenaltyPerTick: 4f,
            stableHousingRecoveryPerTick: 0f);

        needs.UpdateTick(world);

        Assert.Equal(75f, citizen.HousingSatisfaction);
    }

    [Fact]
    public void HousingSatisfaction_Reflects_Overcrowding_And_Rent_Burden()
    {
        var world = new WorldState();
        var adult = new Citizen("Adult", 30, "Worker") { Income = 10f, HousingSatisfaction = 80f };
        var child = new Citizen("Child", 8, "Child") { HousingSatisfaction = 80f };
        world.Citizens.Add(adult);
        world.Citizens.Add(child);
        var household = world.CreateHousehold(1, new[] { adult, child }, housingUnitId: 10, housingCapacity: 1, rentPerTick: 10f);

        var needs = new NeedsSystem(
            foodDecayPerTick: 0f,
            housingDecayPerTick: 1f,
            safetyDecayPerTick: 0f,
            healthcareDecayPerTick: 0f,
            entertainmentDecayPerTick: 0f,
            overcrowdingPenaltyPerExtraPersonPerTick: 3f,
            highRentBurdenPenaltyPerTick: 2f,
            stableHousingRecoveryPerTick: 0f);

        needs.UpdateTick(world);

        Assert.True(household.IsOvercrowded);
        Assert.Equal(72f, adult.HousingSatisfaction);
        Assert.Equal(72f, child.HousingSatisfaction);
    }

    [Fact]
    public void HousingSatisfaction_Recovers_Slightly_With_Stable_Housing()
    {
        var world = new WorldState();
        var adult = new Citizen("Stable", 30, "Worker") { Income = 100f, HousingSatisfaction = 60f };
        world.Citizens.Add(adult);
        world.CreateHousehold(1, new[] { adult }, housingUnitId: 1, housingCapacity: 2, rentPerTick: 10f);

        var needs = new NeedsSystem(
            foodDecayPerTick: 0f,
            housingDecayPerTick: 1f,
            safetyDecayPerTick: 0f,
            healthcareDecayPerTick: 0f,
            entertainmentDecayPerTick: 0f,
            stableHousingRecoveryPerTick: 3f);

        needs.UpdateTick(world);

        Assert.Equal(62f, adult.HousingSatisfaction);
    }
}
