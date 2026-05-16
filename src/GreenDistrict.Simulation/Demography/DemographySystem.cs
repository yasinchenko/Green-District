using System;
using System.Collections.Generic;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;

namespace GreenDistrict.Simulation.Demography;

/// <summary>
/// Handles aging, births, deaths and district migration.
/// Designed to run annual updates when enough ticks have passed.
/// </summary>
public class DemographySystem
{
    private static readonly string[] FemaleNames = ["Anna", "Maria", "Elena", "Sofia", "Nina"];
    private static readonly string[] MaleNames = ["Alex", "Ivan", "Mikhail", "Nikolai", "Sergey"];

    public int TicksPerYear { get; }
    public float BirthRatePerPersonPerYear { get; }
    public float BaseDeathRatePerPersonPerYear { get; }
    public float MigrationRatePerPersonPerYear { get; }

    private readonly Random _rng;

    public DemographySystem(int ticksPerYear = 1440 * 365, float birthRatePerPersonPerYear = 0.02f, float baseDeathRatePerPersonPerYear = 0.01f, float migrationRatePerPersonPerYear = 0.005f, Random? rng = null)
    {
        TicksPerYear = Math.Max(1, ticksPerYear);
        BirthRatePerPersonPerYear = Math.Clamp(birthRatePerPersonPerYear, 0f, 1f);
        BaseDeathRatePerPersonPerYear = Math.Clamp(baseDeathRatePerPersonPerYear, 0f, 1f);
        MigrationRatePerPersonPerYear = Math.Clamp(migrationRatePerPersonPerYear, 0f, 1f);
        _rng = rng ?? new Random(0);
    }

    /// <summary>
    /// Called every tick; performs annual updates when enough ticks passed.
    /// Also performs immediate deaths if Health <= 0.
    /// </summary>
    public void UpdateTick(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var d in world.Citizens.Where(c => c.Health <= 0f).ToList())
        {
            RemoveCitizen(world, d);
            world.Events.Add(new GameEvent($"Death: {d.Name}", $"Citizen {d.Name} died (health).", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
        }

        // Annual updates
        if (world.Clock.CurrentTick % TicksPerYear != 0) return;

        // Aging
        foreach (var c in world.Citizens)
        {
            c.Age += 1;
        }

        // Deaths by age & health
        var deaths = new List<Citizen>();
        foreach (var c in world.Citizens)
        {
            // death prob influenced by age and health
            var ageFactor = Math.Max(0f, (c.Age - 60) / 40f); // ramp up after 60
            var healthFactor = (100f - c.Health) / 100f; // 0..1
            var prob = BaseDeathRatePerPersonPerYear * (1f + ageFactor) * (1f + healthFactor);
            if (_rng.NextDouble() < prob)
                deaths.Add(c);
        }
        foreach (var d in deaths)
        {
            RemoveCitizen(world, d);
            world.Events.Add(new GameEvent($"Death: {d.Name}", $"Citizen {d.Name} died of natural causes.", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
        }

        // Births: consider gender (female) and age-dependent fertility
        var births = new List<Citizen>();
        foreach (var c in world.Citizens)
        {
            if (c.Gender == Gender.Female && c.Age >= 18 && c.Age <= 45)
            {
                // fertility multiplier by age: peak 25-34
                float mult = 1f;
                if (c.Age >= 25 && c.Age <= 34) mult = 1.5f;
                else if (c.Age >= 35 && c.Age <= 40) mult = 0.7f;
                var prob = BirthRatePerPersonPerYear * mult;
                if (_rng.NextDouble() < prob)
                {
                    var baby = CreateBabyForMother(world, c);
                    births.Add(baby);
                }
            }
        }
        foreach (var b in births)
        {
            world.Citizens.Add(b);
            world.Events.Add(new GameEvent($"Birth", $"A new citizen {b.Name} was born.", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
        }

        // Migration: households and lone citizens prefer districts with jobs, housing, safety and satisfaction.
        if (world.Districts.Count > 1)
        {
            foreach (var household in world.Households.ToList())
            {
                if (_rng.NextDouble() < CalculateHouseholdMigrationChance(world, household))
                {
                    TryMoveHouseholdToBestDistrict(world, household);
                }
            }

            foreach (var c in world.Citizens.Where(c => !c.HouseholdId.HasValue).ToList())
            {
                if (_rng.NextDouble() < CalculateCitizenMigrationChance(c))
                {
                    TryMoveCitizenToBestDistrict(world, c);
                }
            }
        }
    }

    private static void RemoveCitizen(WorldState world, Citizen citizen)
    {
        EconomySystem.ReleaseCitizenFromJob(world, citizen);
        world.RemoveCitizenFromHousehold(citizen);
        world.Citizens.Remove(citizen);
    }

    private Citizen CreateBabyForMother(WorldState world, Citizen mother)
    {
        var gender = _rng.NextDouble() < 0.5 ? Gender.Male : Gender.Female;
        var father = FindSecondParent(world, mother);
        var familyName = ResolveFamilyName(mother, father);
        var firstName = GenerateFirstName(gender);
        var fullName = string.IsNullOrWhiteSpace(familyName) ? firstName : $"{firstName} {familyName}";

        var baby = new Citizen(fullName, 0, "Child", gender)
        {
            DistrictId = mother.DistrictId,
            FamilyName = familyName,
            MotherId = mother.Id,
            FatherId = father?.Id,
            FoodSatisfaction = mother.FoodSatisfaction,
            HousingSatisfaction = mother.HousingSatisfaction,
            SafetySatisfaction = mother.SafetySatisfaction,
            HealthcareSatisfaction = mother.HealthcareSatisfaction,
            EntertainmentSatisfaction = mother.EntertainmentSatisfaction
        };

        Household? household = null;
        if (mother.HouseholdId.HasValue)
        {
            household = world.GetHousehold(mother.HouseholdId.Value);
        }

        household ??= world.CreateHousehold(mother.DistrictId, new[] { mother });

        world.AddCitizenToHousehold(baby, household);

        return baby;
    }

    private Citizen? FindSecondParent(WorldState world, Citizen mother)
    {
        if (!mother.HouseholdId.HasValue) return null;

        var household = world.GetHousehold(mother.HouseholdId.Value);
        if (household == null) return null;

        return household.MemberIds
            .Select(world.GetCitizen)
            .Where(c => c != null)
            .Cast<Citizen>()
            .FirstOrDefault(c =>
                c.Id != mother.Id &&
                c.Gender == Gender.Male &&
                c.LifeStage == LifeStage.Adult &&
                !c.IsRetired);
    }

    private string GenerateFirstName(Gender gender)
    {
        var names = gender == Gender.Male ? MaleNames : FemaleNames;
        return names[_rng.Next(names.Length)];
    }

    private static string ResolveFamilyName(Citizen mother, Citizen? father)
    {
        if (!string.IsNullOrWhiteSpace(father?.FamilyName))
        {
            return father.FamilyName;
        }

        if (!string.IsNullOrWhiteSpace(mother.FamilyName))
        {
            return mother.FamilyName;
        }

        return string.Empty;
    }

    private float CalculateHouseholdMigrationChance(WorldState world, Household household)
    {
        var members = household.MemberIds
            .Select(world.GetCitizen)
            .Where(c => c != null)
            .Cast<Citizen>()
            .ToList();

        var averageSatisfaction = members.Count == 0 ? 50f : members.Average(c => c.Satisfaction);
        var stressMultiplier = averageSatisfaction < 45f ? 3f : averageSatisfaction < 60f ? 2f : 1f;

        if (!household.HasHousing || household.IsOvercrowded)
        {
            stressMultiplier += 1f;
        }

        return Math.Clamp(MigrationRatePerPersonPerYear * stressMultiplier, 0f, 1f);
    }

    private float CalculateCitizenMigrationChance(Citizen citizen)
    {
        var stressMultiplier = citizen.Satisfaction < 45f ? 3f : citizen.Satisfaction < 60f ? 2f : 1f;
        return Math.Clamp(MigrationRatePerPersonPerYear * stressMultiplier, 0f, 1f);
    }

    private static void TryMoveHouseholdToBestDistrict(WorldState world, Household household)
    {
        var currentDistrictId = household.DistrictId;
        var currentScore = ScoreDistrictForHousehold(world, household, currentDistrictId);
        var target = world.Districts
            .Select(d => new
            {
                District = d,
                Score = ScoreDistrictForHousehold(world, household, d.Id)
            })
            .Where(option => option.District.Id != currentDistrictId)
            .OrderByDescending(option => option.Score)
            .FirstOrDefault();

        if (target == null || target.Score <= currentScore + 5f) return;

        var targetHousing = FindBestAvailableHousing(world, target.District.Id, household.MemberCount);
        if (targetHousing == null && household.HasHousing) return;

        MoveHousehold(world, household, target.District.Id, targetHousing);
    }

    private static void TryMoveCitizenToBestDistrict(WorldState world, Citizen citizen)
    {
        var currentScore = ScoreDistrictForCitizen(world, citizen, citizen.DistrictId);
        var target = world.Districts
            .Select(d => new
            {
                District = d,
                Score = ScoreDistrictForCitizen(world, citizen, d.Id)
            })
            .Where(option => option.District.Id != citizen.DistrictId)
            .OrderByDescending(option => option.Score)
            .FirstOrDefault();

        if (target == null || target.Score <= currentScore + 5f) return;

        citizen.DistrictId = target.District.Id;
    }

    private static float ScoreDistrictForHousehold(WorldState world, Household household, int? districtId)
    {
        if (!districtId.HasValue) return 0f;

        var score = ScoreDistrictBase(world, districtId.Value);
        var suitableHousing = FindBestAvailableHousing(world, districtId.Value, household.MemberCount);

        if (household.DistrictId == districtId && household.HasHousing)
        {
            score += household.IsOvercrowded ? 10f : 30f;
        }
        else if (suitableHousing != null)
        {
            score += 30f;
        }
        else
        {
            score -= household.HasHousing ? 35f : 15f;
        }

        return score;
    }

    private static float ScoreDistrictForCitizen(WorldState world, Citizen citizen, int? districtId)
    {
        if (!districtId.HasValue) return 0f;

        var score = ScoreDistrictBase(world, districtId.Value);
        if (EconomySystem.IsEligibleForWork(citizen) && string.IsNullOrEmpty(citizen.Job))
        {
            score += CountOpenJobs(world, districtId.Value) * 2f;
        }

        return score;
    }

    private static float ScoreDistrictBase(WorldState world, int districtId)
    {
        var citizens = world.Citizens.Where(c => c.DistrictId == districtId).ToList();
        var averageSatisfaction = citizens.Count == 0 ? 50f : citizens.Average(c => c.Satisfaction);
        var averageSafety = citizens.Count == 0 ? 50f : citizens.Average(c => c.SafetySatisfaction);
        var openJobs = CountOpenJobs(world, districtId);
        var availableHousing = world.HousingUnits.Count(h => h.DistrictId == districtId && !h.IsOccupied);

        return averageSatisfaction * 0.30f
            + averageSafety * 0.30f
            + Math.Min(30f, openJobs * 6f)
            + Math.Min(20f, availableHousing * 8f);
    }

    private static int CountOpenJobs(WorldState world, int districtId)
    {
        return world.Businesses
            .Where(b => b.DistrictId == districtId && b.Status == BusinessStatus.Active)
            .Sum(b => Math.Max(0, b.MaxEmployees - b.EmployeeIds.Count));
    }

    private static HousingUnit? FindBestAvailableHousing(WorldState world, int districtId, int householdSize)
    {
        return world.HousingUnits
            .Where(h => h.DistrictId == districtId && !h.IsOccupied && h.Capacity >= householdSize)
            .OrderBy(h => h.RentPerTick)
            .ThenBy(h => h.Capacity)
            .FirstOrDefault();
    }

    private static void MoveHousehold(WorldState world, Household household, int districtId, HousingUnit? targetHousing)
    {
        if (targetHousing != null)
        {
            world.AssignHouseholdToHousingUnit(household, targetHousing);
        }
        else if (household.HousingUnitId.HasValue)
        {
            world.ReleaseHouseholdHousing(household);
        }

        household.DistrictId = districtId;
        foreach (var memberId in household.MemberIds.ToList())
        {
            var member = world.GetCitizen(memberId);
            if (member == null)
            {
                household.MemberIds.Remove(memberId);
                continue;
            }

            member.DistrictId = districtId;
        }
    }
}
