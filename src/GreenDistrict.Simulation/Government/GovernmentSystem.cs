using System;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Government;

/// <summary>
/// Manages government projects, budget spending and simple fiscal policies.
/// </summary>
public class GovernmentSystem
{
    /// <summary>
    /// Start a project if there is enough budget. Deducts cost immediately.
    /// </summary>
    public bool StartProject(WorldState world, GovernmentProject project)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (project == null) throw new ArgumentNullException(nameof(project));

        if (world.Budget < project.Cost) return false;

        world.Budget -= project.Cost;
        project.StartTick = world.Clock.CurrentTick;
        project.RemainingTicks = project.DurationTicks;
        project.Completed = false;
        world.Projects.Add(project);
        return true;
    }

    /// <summary>
    /// Advance projects by one tick and apply benefits on completion.
    /// </summary>
    public void TickProjects(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var project in world.Projects.Where(p => !p.Completed).ToList())
        {
            project.RemainingTicks = Math.Max(0, project.RemainingTicks - 1);
            if (project.RemainingTicks <= 0)
            {
                project.Completed = true;
                world.Budget += project.Benefit;
                ApplyProjectEffects(world, project);
                world.DistrictsSystem.UpdateDistrictAggregates(world);

                var ev = new GameEvent($"Project {project.Name} completed", $"Project {project.Name} completed and delivered benefit {project.Benefit}", EventType.Economic)
                {
                    CreatedAtTick = world.Clock.CurrentTick
                };
                world.Events.Add(ev);
            }
        }
    }

    /// <summary>
    /// Cancel a project and refund a portion of cost (50%). Returns refunded amount.
    /// </summary>
    public float CancelProject(WorldState world, int projectId)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        var project = world.Projects.FirstOrDefault(p => p.Id == projectId && !p.Completed);
        if (project == null) return 0f;

        var refund = project.Cost * 0.5f;
        world.Budget += refund;
        world.Projects.Remove(project);

        var ev = new GameEvent($"Project {project.Name} cancelled", $"Project {project.Name} was cancelled and refunded {refund}", EventType.Political)
        {
            CreatedAtTick = world.Clock.CurrentTick
        };
        world.Events.Add(ev);

        return refund;
    }

    private static void ApplyProjectEffects(WorldState world, GovernmentProject project)
    {
        var affectedCitizens = project.DistrictId.HasValue
            ? world.Citizens.Where(c => c.DistrictId == project.DistrictId.Value)
            : world.Citizens;

        foreach (var citizen in affectedCitizens)
        {
            citizen.FoodSatisfaction = ClampSatisfaction(citizen.FoodSatisfaction + project.FoodSatisfactionEffect);
            citizen.HousingSatisfaction = ClampSatisfaction(citizen.HousingSatisfaction + project.HousingSatisfactionEffect);
            citizen.SafetySatisfaction = ClampSatisfaction(citizen.SafetySatisfaction + project.SafetySatisfactionEffect);
            citizen.HealthcareSatisfaction = ClampSatisfaction(citizen.HealthcareSatisfaction + project.HealthcareSatisfactionEffect);
            citizen.EntertainmentSatisfaction = ClampSatisfaction(citizen.EntertainmentSatisfaction + project.EntertainmentSatisfactionEffect);
            citizen.RecalculateSatisfaction();
        }

        CreateHousingUnits(world, project);
        world.SupportRating = Math.Clamp(world.SupportRating + project.SupportEffect, 0f, 100f);
    }

    private static void CreateHousingUnits(WorldState world, GovernmentProject project)
    {
        if (!project.DistrictId.HasValue) return;
        if (project.HousingUnitsToCreate <= 0 || project.HousingUnitCapacity <= 0) return;

        var nextId = world.HousingUnits.Count == 0
            ? 1
            : world.HousingUnits.Max(h => h.Id) + 1;

        for (var i = 0; i < project.HousingUnitsToCreate; i++)
        {
            world.AddHousingUnit(
                nextId + i,
                project.DistrictId.Value,
                project.HousingUnitCapacity,
                project.HousingUnitRentPerTick);
        }
    }

    private static float ClampSatisfaction(float value)
    {
        return Math.Clamp(value, 0f, 100f);
    }
}
