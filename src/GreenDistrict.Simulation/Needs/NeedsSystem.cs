using System;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Map;

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
    public float NoHousingPenaltyPerTick { get; }
    public float OvercrowdingPenaltyPerExtraPersonPerTick { get; }
    public float HighRentBurdenPenaltyPerTick { get; }
    public float StableHousingRecoveryPerTick { get; }

    public NeedsSystem(
        float foodDecayPerTick = 0.001f,
        float housingDecayPerTick = 0.002f,
        float safetyDecayPerTick = 0.0003f,
        float healthcareDecayPerTick = 0.0003f,
        float entertainmentDecayPerTick = 0.001f,
        float noHousingPenaltyPerTick = 0.05f,
        float overcrowdingPenaltyPerExtraPersonPerTick = 0.02f,
        float highRentBurdenPenaltyPerTick = 0.03f,
        float stableHousingRecoveryPerTick = 0.01f)
    {
        FoodDecayPerTick = foodDecayPerTick;
        HousingDecayPerTick = housingDecayPerTick;
        SafetyDecayPerTick = safetyDecayPerTick;
        HealthcareDecayPerTick = healthcareDecayPerTick;
        EntertainmentDecayPerTick = entertainmentDecayPerTick;
        NoHousingPenaltyPerTick = noHousingPenaltyPerTick;
        OvercrowdingPenaltyPerExtraPersonPerTick = overcrowdingPenaltyPerExtraPersonPerTick;
        HighRentBurdenPenaltyPerTick = highRentBurdenPenaltyPerTick;
        StableHousingRecoveryPerTick = stableHousingRecoveryPerTick;
    }

    /// <summary>
    /// Update needs for all citizens in the world by one tick.
    /// </summary>
    public void UpdateTick(WorldState world)
    {
        UpdateTick(world, accessibility: null);
    }

    public void UpdateTick(WorldState world, MapAccessibilityReport? accessibility)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var citizen in world.Citizens)
        {
            // Decay needs
            citizen.FoodSatisfaction = Clamp01(citizen.FoodSatisfaction - FoodDecayPerTick * CoverageDecayMultiplier(accessibility, citizen.DistrictId, MapCoverageKind.Trade));
            citizen.HousingSatisfaction = Clamp01(citizen.HousingSatisfaction + CalculateHousingDelta(world, citizen));
            citizen.SafetySatisfaction = Clamp01(citizen.SafetySatisfaction - SafetyDecayPerTick * CoverageDecayMultiplier(accessibility, citizen.DistrictId, MapCoverageKind.Safety));
            citizen.HealthcareSatisfaction = Clamp01(citizen.HealthcareSatisfaction - HealthcareDecayPerTick * CoverageDecayMultiplier(accessibility, citizen.DistrictId, MapCoverageKind.Healthcare));
            citizen.EntertainmentSatisfaction = Clamp01(citizen.EntertainmentSatisfaction - EntertainmentDecayPerTick * CoverageDecayMultiplier(accessibility, citizen.DistrictId, MapCoverageKind.Recreation));

            // Recalculate aggregated satisfaction
            citizen.RecalculateSatisfaction();

            // Update mood and other derived stats
            citizen.UpdateMood();
        }
    }

    private static float CoverageDecayMultiplier(MapAccessibilityReport? accessibility, int? districtId, MapCoverageKind kind)
    {
        if (accessibility == null || !districtId.HasValue) return 1f;

        var coveragePercent = accessibility.GetDistrictCoveragePercent(districtId, kind);
        return 1f + (100f - Math.Clamp(coveragePercent, 0f, 100f)) / 100f;
    }

    private float CalculateHousingDelta(WorldState world, Citizen citizen)
    {
        var delta = -HousingDecayPerTick;
        var household = citizen.HouseholdId.HasValue
            ? world.GetHousehold(citizen.HouseholdId.Value)
            : null;

        if (household == null || !household.HasHousing)
        {
            return delta - NoHousingPenaltyPerTick;
        }

        var penalty = 0f;
        if (household.IsOvercrowded)
        {
            var extraPeople = Math.Max(0, household.MemberCount - household.HousingCapacity);
            penalty += extraPeople * OvercrowdingPenaltyPerExtraPersonPerTick;
        }

        if (household.RentPerTick > 0f)
        {
            if (household.TotalIncome <= 0f)
            {
                penalty += HighRentBurdenPenaltyPerTick;
            }
            else
            {
                var rentBurden = household.RentPerTick / household.TotalIncome;
                if (rentBurden > 0.35f)
                {
                    penalty += HighRentBurdenPenaltyPerTick * Math.Min(2f, rentBurden / 0.35f);
                }
            }
        }

        if (penalty <= 0f)
        {
            delta += StableHousingRecoveryPerTick;
        }
        else
        {
            delta -= penalty;
        }

        return delta;
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 100f);
}
