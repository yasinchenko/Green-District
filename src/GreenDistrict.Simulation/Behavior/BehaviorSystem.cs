using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;

namespace GreenDistrict.Simulation.Behavior;

/// <summary>
/// Handles citizen behavior: retirement, voluntary quits, basic job-change logic.
/// </summary>
public class BehaviorSystem
{
    public int RetirementAge { get; }
    public float QuitSatisfactionThreshold { get; }

    public BehaviorSystem(int retirementAge = 65, float quitSatisfactionThreshold = 15f)
    {
        RetirementAge = retirementAge;
        QuitSatisfactionThreshold = quitSatisfactionThreshold;
    }

    public void UpdateTick(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var c in world.Citizens.ToList())
        {
            // Retirement
            if (!c.IsRetired && c.Age >= RetirementAge)
            {
                EconomySystem.ReleaseCitizenFromJob(world, c);
                c.Retire();
                c.Profession = "Retired";
                world.Events.Add(new GameEvent($"Retirement: {c.Name}", $"{c.Name} retired at age {c.Age}.", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
                continue;
            }

            // Voluntary quit due to low satisfaction
            if (!string.IsNullOrEmpty(c.Job) && c.Satisfaction < QuitSatisfactionThreshold)
            {
                EconomySystem.ReleaseCitizenFromJob(world, c);
                world.Events.Add(new GameEvent($"Quit: {c.Name}", $"{c.Name} quit their job due to low satisfaction.", EventType.Social) { CreatedAtTick = world.Clock.CurrentTick });
            }
        }
    }
}
