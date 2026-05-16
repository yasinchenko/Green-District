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
    /// Recomputes population, housing, average satisfaction and economic level for each district.
    /// </summary>
    public void UpdateDistrictAggregates(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var district in world.Districts)
        {
            var citizens = world.Citizens.Where(c => c.DistrictId == district.Id).ToList();
            district.Population = citizens.Count;
            district.AverageSatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.Satisfaction);
            district.AverageHousingSatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.HousingSatisfaction);
            district.AverageSafetySatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.SafetySatisfaction);
            district.AverageHealthcareSatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.HealthcareSatisfaction);
            district.AverageEntertainmentSatisfaction = citizens.Count == 0 ? 0f : citizens.Average(c => c.EntertainmentSatisfaction);

            var housingUnits = world.HousingUnits.Where(h => h.DistrictId == district.Id).ToList();
            district.HousingCapacity = housingUnits.Sum(h => h.Capacity);
            district.OccupiedHousing = housingUnits.Count(h => h.IsOccupied);
            district.AvailableHousing = housingUnits.Count(h => !h.IsOccupied);

            // Economic level estimate: average business profit normalized to 0-100
            var businesses = world.Businesses.Where(b => b.DistrictId == district.Id).ToList();
            var activeBusinesses = businesses.Where(b => b.Status == BusinessStatus.Active).ToList();
            district.TotalJobs = activeBusinesses.Sum(b => Math.Max(0, b.MaxEmployees));
            var filledJobs = activeBusinesses.Sum(b => b.EmployeeIds.Count);
            district.OpenJobs = Math.Max(0, district.TotalJobs - filledJobs);

            var workforce = citizens.Count(c => c.LifeStage == LifeStage.Adult && !c.IsRetired);
            var employed = citizens.Count(c => c.EmploymentStatus == EmploymentStatus.Employed);
            district.EmploymentRate = workforce == 0 ? 0f : Math.Clamp(employed / (float)workforce * 100f, 0f, 100f);

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

            var districtProjects = world.Projects.Where(p => p.DistrictId == district.Id).ToList();
            district.ActiveProjects = districtProjects.Count(p => !p.Completed);
            district.CompletedProjects = districtProjects.Count(p => p.Completed);

            var serviceNeeds = citizens.Count == 0
                ? 50f
                : (district.AverageHealthcareSatisfaction + district.AverageEntertainmentSatisfaction) / 2f;
            district.ServiceLevel = Math.Clamp(serviceNeeds + district.CompletedProjects * 5f, 0f, 100f);
        }
    }
}
