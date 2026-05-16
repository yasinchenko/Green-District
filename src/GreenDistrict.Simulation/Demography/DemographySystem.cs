using System;
using System.Collections.Generic;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Demography;

/// <summary>
/// Handles aging, births, deaths and simple migration.
/// Designed to run annual updates when enough ticks have passed.
/// </summary>
public class DemographySystem
{
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

        // Immediate deaths due to health
        var toRemove = new List<Citizen>();
        foreach (var c in world.Citizens.Where(c => c.Health <= 0f).ToList())
        {
            toRemove.Add(c);
        }
        foreach (var d in toRemove)
        {
            world.Citizens.Remove(d);
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
            world.Citizens.Remove(d);
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
                    var baby = new Citizen("Baby", 0, c.Profession, Gender.Female);
                    births.Add(baby);
                }
            }
        }
        foreach (var b in births)
        {
            world.Citizens.Add(b);
            world.Events.Add(new GameEvent($"Birth", $"A new citizen {b.Name} was born.", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
        }

        // Migration: small chance per person to move districts (if multiple districts exist)
        if (world.Districts.Count > 1)
        {
            foreach (var c in world.Citizens)
            {
                if (_rng.NextDouble() < MigrationRatePerPersonPerYear)
                {
                    var target = world.Districts[_rng.Next(world.Districts.Count)];
                    c.DistrictId = target.Id;
                }
            }
        }
    }
}
