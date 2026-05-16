using System;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Needs;

/// <summary>
/// Simple needs system that decays citizen needs over time and updates satisfaction.
/// Designed to be deterministic and testable: decay rates are provided via constructor.
/// </summary>
public class NeedsSystem
{
    // Decay per tick (1 tick = 1 game minute)
    public float FoodDecayPerTick { get; }
    public float HousingDecayPerTick { get; }
    public float SafetyDecayPerTick { get; }
    public float HealthcareDecayPerTick { get; }
    public float EntertainmentDecayPerTick { get; }

    public NeedsSystem(
        float foodDecayPerTick = 0.01f,
        float housingDecayPerTick = 0.002f,
        float safetyDecayPerTick = 0.001f,
        float healthcareDecayPerTick = 0.001f,
        float entertainmentDecayPerTick = 0.005f)
    {
        FoodDecayPerTick = foodDecayPerTick;
        HousingDecayPerTick = housingDecayPerTick;
        SafetyDecayPerTick = safetyDecayPerTick;
        HealthcareDecayPerTick = healthcareDecayPerTick;
        EntertainmentDecayPerTick = entertainmentDecayPerTick;
    }

    /// <summary>
    /// Update needs for all citizens in the world by one tick.
    /// </summary>
    public void UpdateTick(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var citizen in world.Citizens)
        {
            // Decay needs
            citizen.FoodSatisfaction = Clamp01(citizen.FoodSatisfaction - FoodDecayPerTick);
            citizen.HousingSatisfaction = Clamp01(citizen.HousingSatisfaction - HousingDecayPerTick);
            citizen.SafetySatisfaction = Clamp01(citizen.SafetySatisfaction - SafetyDecayPerTick);
            citizen.HealthcareSatisfaction = Clamp01(citizen.HealthcareSatisfaction - HealthcareDecayPerTick);
            citizen.EntertainmentSatisfaction = Clamp01(citizen.EntertainmentSatisfaction - EntertainmentDecayPerTick);

            // Recalculate aggregated satisfaction
            citizen.RecalculateSatisfaction();

            // Update mood and other derived stats
            citizen.UpdateMood();
        }
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 100f);
}
