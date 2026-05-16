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
    public const int MinimumWorkingAge = 18;
    public const int DefaultBankruptcyLossTicks = 3;

    private readonly float _taxRate;

    public EconomySystem(float taxRate = 0.15f)
    {
        _taxRate = Math.Clamp(taxRate, 0f, 1f);
    }

    public void ApplyBusinessTypes(WorldState world, BusinessTypeCatalog catalog)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        foreach (var business in world.Businesses)
        {
            if (business.Status != BusinessStatus.Active) continue;
            if (!catalog.TryGet(business.Type, out var definition)) continue;

            business.ProductionType = definition.ProductionType;
            business.BaseOutput = Math.Max(0f, definition.BaseOutput);
            business.UnitPrice = Math.Max(0f, definition.UnitPrice);
            business.DemandMultiplier = Math.Max(0f, definition.DemandMultiplier);
            if (business.MaxEmployees <= 0)
            {
                business.MaxEmployees = definition.MaxEmployees;
            }
        }
    }

    public void ProcessProductionAndSales(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            var produced = business.BaseOutput * business.GetStaffingRatio();
            var sold = produced * Math.Max(0f, business.DemandMultiplier);
            var salesRevenue = sold * Math.Max(0f, business.UnitPrice);

            business.LastProducedUnits = produced;
            business.LastSoldUnits = sold;
            business.LastSalesRevenue = salesRevenue;
            business.Revenue += salesRevenue;
        }
    }

    /// <summary>
    /// Assign unemployed citizens to businesses with vacancies.
    /// Assigns by simple first-come rule; returns number of assignments.
    /// </summary>
    public int AssignJobs(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var assigned = 0;
        var unemployed = world.Citizens.Where(IsAvailableForWork).ToList();
        var openBusinesses = world.Businesses
            .Where(b => b.Status == BusinessStatus.Active && b.EmployeeIds.Count < b.MaxEmployees)
            .ToList();

        foreach (var citizen in unemployed)
        {
            var business = openBusinesses.FirstOrDefault(b => b.EmployeeIds.Count < b.MaxEmployees);
            if (business == null) break;

            if (HireCitizen(citizen, business))
            {
                assigned++;
            }

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
        ProcessPayroll(world, professionCatalog: null);
    }

    public void ProcessPayroll(WorldState world, ProfessionCatalog? professionCatalog)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        world.LastIncomeTaxCollected = 0f;
        world.LastBusinessTaxCollected = 0f;
        world.LastOperatingExpenses = 0f;
        world.LastNetBudgetChange = 0f;

        var budgetBefore = world.Budget;
        var incomeTaxCollected = 0f;

        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            if (business.EmployeeIds.Count == 0) continue;

            foreach (var empId in business.EmployeeIds.ToList())
            {
                var citizen = world.GetCitizen(empId);
                if (citizen == null || citizen.Job != business.Name || !IsEligibleForWork(citizen))
                {
                    business.EmployeeIds.Remove(empId);
                    business.EmployeeCount = business.EmployeeIds.Count;
                    if (citizen != null && citizen.Job == business.Name)
                    {
                        citizen.Job = null;
                    }
                    continue;
                }

                var gross = GetGrossWage(citizen, business, professionCatalog);
                var tax = gross * Math.Clamp(world.IncomeTaxRate, 0f, 1f);
                var net = gross - tax;

                // Business pays gross
                business.Revenue -= gross;
                business.Expenses += gross;

                // Citizen receives net to their Income
                citizen.Income += net;

                // Government gets tax
                world.Budget += tax;
                incomeTaxCollected += tax;
            }
        }

        world.LastIncomeTaxCollected = incomeTaxCollected;
        world.LastNetBudgetChange += world.Budget - budgetBefore;
    }

    public float ProcessBusinessTaxes(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var budgetBefore = world.Budget;
        var rate = Math.Clamp(world.BusinessTaxRate, 0f, 1f);
        var collected = 0f;

        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            var taxableProfit = Math.Max(0f, business.GetProfit());
            if (taxableProfit <= 0f) continue;

            var tax = taxableProfit * rate;
            business.Revenue -= tax;
            business.Expenses += tax;
            world.Budget += tax;
            collected += tax;
        }

        world.LastBusinessTaxCollected = collected;
        world.LastNetBudgetChange += world.Budget - budgetBefore;
        return collected;
    }

    public float ProcessGovernmentExpenses(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var budgetBefore = world.Budget;
        var activeProjects = world.Projects.Count(p => !p.Completed);
        var expenses = Math.Max(0f, world.BaseOperatingExpensePerTick) +
                       Math.Max(0f, world.ProjectOperatingExpensePerTick) * activeProjects;

        world.Budget -= expenses;
        world.LastOperatingExpenses = expenses;
        world.LastNetBudgetChange += world.Budget - budgetBefore;
        return expenses;
    }

    public static float GetGrossWage(Citizen citizen, Business business, ProfessionCatalog? professionCatalog = null)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));
        if (business == null) throw new ArgumentNullException(nameof(business));

        var fallback = Math.Max(0f, business.WagePerEmployee);
        return professionCatalog?.GetBaseWageOrDefault(citizen.Profession, fallback) ?? fallback;
    }

    public static bool IsEligibleForWork(Citizen citizen)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));

        return citizen.Age >= MinimumWorkingAge
            && citizen.Health > 0f
            && citizen.LifeStage == LifeStage.Adult
            && citizen.EmploymentStatus != EmploymentStatus.Retired;
    }

    public static bool IsAvailableForWork(Citizen citizen)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));

        return IsEligibleForWork(citizen)
            && citizen.EmploymentStatus == EmploymentStatus.Unemployed
            && string.IsNullOrEmpty(citizen.Job);
    }

    public static bool HireCitizen(Citizen citizen, Business business)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));
        if (business == null) throw new ArgumentNullException(nameof(business));
        if (business.Status != BusinessStatus.Active) return false;
        if (!IsAvailableForWork(citizen)) return false;
        if (business.EmployeeIds.Count >= business.MaxEmployees) return false;
        if (business.EmployeeIds.Contains(citizen.Id)) return false;

        business.EmployeeIds.Add(citizen.Id);
        business.EmployeeCount = business.EmployeeIds.Count;
        citizen.Job = business.Name;
        return true;
    }

    public static void ReleaseCitizenFromJob(WorldState world, Citizen citizen)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));

        if (!string.IsNullOrEmpty(citizen.Job))
        {
            var business = world.Businesses.FirstOrDefault(b => b.Name == citizen.Job);
            if (business != null)
            {
                business.EmployeeIds.Remove(citizen.Id);
                business.EmployeeCount = business.EmployeeIds.Count;
            }
        }

        citizen.Job = null;
    }

    public int UpdateBusinessViability(WorldState world, float bankruptcyProfitThreshold = 0f, int lossTicksToBankruptcy = DefaultBankruptcyLossTicks)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        lossTicksToBankruptcy = Math.Max(1, lossTicksToBankruptcy);

        var closed = 0;
        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active).ToList())
        {
            if (business.GetProfit() < bankruptcyProfitThreshold)
            {
                business.ConsecutiveLossTicks++;
            }
            else
            {
                business.ConsecutiveLossTicks = 0;
            }

            if (business.ConsecutiveLossTicks >= lossTicksToBankruptcy)
            {
                CloseBusiness(world, business, BusinessStatus.Bankrupt);
                closed++;
            }
        }

        return closed;
    }

    public Business? TryOpenBusiness(
        WorldState world,
        BusinessTypeCatalog catalog,
        string typeId,
        int? districtId = null,
        float minimumBudget = 12000f,
        float maximumUnemploymentRate = 20f)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (!catalog.TryGet(typeId, out var definition)) return null;
        if (world.Budget < minimumBudget) return null;
        if (GetUnemploymentRate(world) > maximumUnemploymentRate) return null;

        var id = world.Businesses.Count == 0 ? 1 : world.Businesses.Max(b => b.Id) + 1;
        var name = GenerateBusinessName(definition.Name, id);
        var business = new Business(name, definition.Id, definition.MaxEmployees)
        {
            Id = id,
            DistrictId = districtId,
            ProductionType = definition.ProductionType,
            BaseOutput = definition.BaseOutput,
            UnitPrice = definition.UnitPrice,
            DemandMultiplier = definition.DemandMultiplier,
            Status = BusinessStatus.Active
        };

        world.Businesses.Add(business);
        return business;
    }

    public float GetUnemploymentRate(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        var laborForce = world.Citizens.Where(IsEligibleForWork).ToList();
        if (laborForce.Count == 0) return 0f;
        var unemployed = laborForce.Count(c => string.IsNullOrEmpty(c.Job));
        return (unemployed / (float)laborForce.Count) * 100f;
    }

    private static void CloseBusiness(WorldState world, Business business, BusinessStatus status)
    {
        foreach (var empId in business.EmployeeIds.ToList())
        {
            var citizen = world.GetCitizen(empId);
            if (citizen != null && citizen.Job == business.Name)
            {
                citizen.Job = null;
            }
        }

        business.EmployeeIds.Clear();
        business.EmployeeCount = 0;
        business.Status = status;
        business.ClosedAtTick = world.Clock.CurrentTick;
        world.Events.Add(new GameEvent(
            $"Business closed: {business.Name}",
            $"Business {business.Name} closed with status {status}.",
            EventType.Economic)
        {
            CreatedAtTick = world.Clock.CurrentTick
        });
    }

    private static string GenerateBusinessName(string typeName, int id)
    {
        var baseName = string.IsNullOrWhiteSpace(typeName) ? "Business" : typeName;
        return $"{baseName} {id}";
    }
}
