using System;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.World;

/// <summary>
/// Responsible for district-level aggregates and statistics.
/// </summary>
public class DistrictSystem
{
    /// <summary>
    /// Recomputes population, average satisfaction and economic level for each district.
    /// </summary>
    public void UpdateDistrictAggregates(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var district in world.Districts)
        {
            var citizens = world.Citizens.Where(c => c.DistrictId == district.Id).ToList();
            district.Population = citizens.Count;
            district.AverageSatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.Satisfaction);

            // Economic level estimate: average business profit normalized to 0-100
            var businesses = world.Businesses.Where(b => b.DistrictId == district.Id).ToList();
            if (businesses.Count == 0)
            {
                district.EconomicLevel = 50f; // neutral default
            }
            else
            {
                var avgProfit = businesses.Average(b => b.GetProfit());
                // Normalize using a simple heuristic: map profit 0..1000 -> 0..100
                var economic = (avgProfit / 1000f) * 100f + 50f; // baseline 50
                district.EconomicLevel = Math.Clamp(economic, 0f, 100f);
            }
        }
    }
}
