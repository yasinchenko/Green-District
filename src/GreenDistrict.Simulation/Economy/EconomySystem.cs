using System;
using System.Linq;
using System.Collections.Generic;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Economy;

/// <summary>
/// Simple economy system responsible for job assignment and payroll processing.
/// </summary>
public class EconomySystem
{
    private readonly float _taxRate;

    public EconomySystem(float taxRate = 0.15f)
    {
        _taxRate = Math.Clamp(taxRate, 0f, 1f);
    }

    /// <summary>
    /// Assign unemployed citizens to businesses with vacancies.
    /// Assigns by simple first-come rule; returns number of assignments.
    /// </summary>
    public int AssignJobs(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var assigned = 0;
        var unemployed = world.Citizens.Where(c => string.IsNullOrEmpty(c.Job)).ToList();
        var openBusinesses = world.Businesses.Where(b => b.EmployeeIds.Count < b.MaxEmployees).ToList();

        foreach (var citizen in unemployed)
        {
            var business = openBusinesses.FirstOrDefault(b => b.EmployeeIds.Count < b.MaxEmployees);
            if (business == null) break;

            business.EmployeeIds.Add(citizen.Id);
            business.EmployeeCount = business.EmployeeIds.Count;
            citizen.Job = business.Name;
            assigned++;

            if (business.EmployeeIds.Count >= business.MaxEmployees)
            {
                openBusinesses.Remove(business);
            }
        }

        return assigned;
    }

    /// <summary>
    /// Process payroll for all businesses: pay wages to employees, apply tax to wages and add to world.Budget.
    /// Business pays gross wage from its Revenue; citizens receive net wage (after tax).
    /// </summary>
    public void ProcessPayroll(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var business in world.Businesses)
        {
            if (business.EmployeeIds.Count == 0) continue;

            foreach (var empId in business.EmployeeIds.ToList())
            {
                var citizen = world.GetCitizen(empId);
                if (citizen == null) continue; // stale reference

                var gross = business.WagePerEmployee;
                var tax = gross * _taxRate;
                var net = gross - tax;

                // Business pays gross
                business.Revenue -= gross;
                business.Expenses += gross;

                // Citizen receives net to their Income
                citizen.Income += net;

                // Government gets tax
                world.Budget += tax;
            }
        }
    }

    public float GetUnemploymentRate(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        var population = world.GetTotalPopulation();
        if (population == 0) return 0f;
        var unemployed = world.Citizens.Count(c => string.IsNullOrEmpty(c.Job));
        return (unemployed / (float)population) * 100f;
    }
}
