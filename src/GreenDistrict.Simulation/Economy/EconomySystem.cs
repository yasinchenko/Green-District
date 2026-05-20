using System;
using System.Linq;
using System.Collections.Generic;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Map;

namespace GreenDistrict.Simulation.Economy;

/// <summary>
/// Simple economy system responsible for job assignment and payroll processing.
/// </summary>
public class EconomySystem
{
    public const int MinimumWorkingAge = 18;
    public const int DefaultBankruptcyLossTicks = 3;
    private const float FoodMaintenanceSpend = 0.8f;
    private const float GoodsMaintenanceSpend = 0.35f;
    private const float HealthcareMaintenanceSpend = 0.25f;
    private const float NeedTarget = 88f;
    private const float ExternalDemandShare = 0.45f;
    private const float InvestmentProfitShare = 0.18f;
    private const float LocalGovernmentOperatingShare = 0.65f;
    private const float ImportPriceMultiplier = 1.35f;

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
        ProcessProductionAndSales(world, accessibility: null);
    }

    public void ProcessProductionAndSales(WorldState world, MapAccessibilityReport? accessibility)
    {
        ProcessProduction(world, accessibility);
        ProcessExternalSales(world);
    }

    public void ProcessProduction(WorldState world)
    {
        ProcessProduction(world, accessibility: null);
    }

    public void ProcessProduction(WorldState world, MapAccessibilityReport? accessibility)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            business.ResetTickAccounting();
            var accessible = accessibility?.IsBusinessAccessible(business.Id) ?? true;
            var produced = accessible
                ? business.BaseOutput * business.GetProductionMultiplier() * business.GetStaffingRatio()
                : 0f;

            business.LastProducedUnits = produced;
        }
    }

    public float ProcessExternalSales(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var totalSalesRevenue = 0f;
        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            var remainingUnits = Math.Max(0f, business.LastProducedUnits - business.LastSoldUnits);
            if (remainingUnits <= 0f || business.UnitPrice <= 0f) continue;

            var externalDemandUnits = business.LastProducedUnits *
                                      Math.Max(0f, business.DemandMultiplier) *
                                      business.GetQualityDemandMultiplier() *
                                      ExternalDemandShare;
            var soldUnits = Math.Min(remainingUnits, externalDemandUnits);
            var salesRevenue = soldUnits * Math.Max(0f, business.UnitPrice);
            if (salesRevenue <= 0f) continue;

            business.LastSoldUnits += soldUnits;
            business.LastSalesRevenue += salesRevenue;
            business.LastExternalSalesRevenue += salesRevenue;
            business.RecordRevenue(salesRevenue);
            totalSalesRevenue += salesRevenue;
        }

        world.LastSalesRevenueGenerated = totalSalesRevenue;
        world.LastExternalInflow += totalSalesRevenue;
        return totalSalesRevenue;
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
        world.LastGrossWagesPaid = 0f;
        world.LastNetWagesPaid = 0f;
        world.LastLocalGovernmentSpending = 0f;
        world.LastExternalGovernmentSpending = 0f;
        world.LastExternalInflow = 0f;
        world.LastExternalOutflow = 0f;
        world.LastInternalTransfers = 0f;

        var budgetBefore = world.Budget;
        var incomeTaxCollected = 0f;
        var grossWagesPaid = 0f;
        var netWagesPaid = 0f;

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
                business.RecordExpense(gross);

                // Citizen receives net to their Income
                citizen.Income += net;
                citizen.Cash += net;
                grossWagesPaid += gross;
                netWagesPaid += net;

                // Government gets tax
                world.Budget += tax;
                incomeTaxCollected += tax;
            }
        }

        world.LastIncomeTaxCollected = incomeTaxCollected;
        world.LastGrossWagesPaid = grossWagesPaid;
        world.LastNetWagesPaid = netWagesPaid;
        world.LastInternalTransfers += grossWagesPaid;
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
            var hasTickAccounting = Math.Abs(business.RevenueThisTick) > 0.001f || Math.Abs(business.ExpensesThisTick) > 0.001f;
            var taxableProfit = Math.Max(0f, hasTickAccounting ? business.ProfitThisTick : business.GetProfit());
            if (taxableProfit <= 0f) continue;

            var tax = taxableProfit * rate;
            business.RecordExpense(tax);
            world.Budget += tax;
            collected += tax;
        }

        world.LastBusinessTaxCollected = collected;
        world.LastInternalTransfers += collected;
        world.LastNetBudgetChange += world.Budget - budgetBefore;
        return collected;
    }

    public int ProcessBusinessInvestments(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var upgrades = 0;
        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active))
        {
            var investment = CalculateBusinessInvestment(business);
            if (investment > 0f)
            {
                business.Invest(investment);
            }

            while (business.TryUpgrade())
            {
                upgrades++;
            }
        }

        return upgrades;
    }

    private static float CalculateBusinessInvestment(Business business)
    {
        if (business.BusinessLevel >= Business.MaxBusinessLevel) return 0f;
        if (business.ProfitThisTick <= 0f || business.Cash <= 0f) return 0f;

        var payrollReserve = Math.Max(0f, business.WagePerEmployee) * Math.Max(1, business.EmployeeIds.Count);
        var targetCashReserve = payrollReserve * 2f + Math.Max(250f, business.GetUpgradeCost() * 0.2f);
        var investableCash = Math.Max(0f, business.Cash - targetCashReserve);
        var plannedInvestment = Math.Max(0f, business.ProfitThisTick * InvestmentProfitShare);
        return Math.Min(investableCash, plannedInvestment);
    }

    public float ProcessConsumerPurchases(WorldState world)
    {
        return ProcessConsumerPurchases(world, accessibility: null);
    }

    public float ProcessConsumerPurchases(WorldState world, MapAccessibilityReport? accessibility)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var totalSpending = 0f;
        foreach (var citizen in world.Citizens.Where(c => c.Cash > 0f))
        {
            totalSpending += PurchaseNeed(
                world,
                citizen,
                FoodMaintenanceSpend,
                citizen.FoodSatisfaction,
                value => citizen.FoodSatisfaction = ClampSatisfaction(value),
                spend => spend * 1.4f,
                accessibility,
                "food",
                "farm",
                "shop",
                "trade");

            totalSpending += PurchaseNeed(
                world,
                citizen,
                GoodsMaintenanceSpend,
                citizen.EntertainmentSatisfaction,
                value => citizen.EntertainmentSatisfaction = ClampSatisfaction(value),
                spend => spend * 0.8f,
                accessibility,
                "goods",
                "workshop",
                "shop",
                "trade");

            totalSpending += PurchaseNeed(
                world,
                citizen,
                HealthcareMaintenanceSpend,
                citizen.HealthcareSatisfaction,
                value => citizen.HealthcareSatisfaction = ClampSatisfaction(value),
                spend => spend * 0.9f,
                accessibility,
                "healthcare",
                "clinic",
                "services");

            citizen.RecalculateSatisfaction();
        }

        world.LastConsumerSpending = totalSpending;
        return totalSpending;
    }

    public IReadOnlyList<ConsumerDemandSnapshot> EstimateConsumerDemand(WorldState world, int? districtId = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        return new[]
        {
            EstimateConsumerDemand(world, districtId, "food", FoodMaintenanceSpend, "food", "farm", "shop", "trade"),
            EstimateConsumerDemand(world, districtId, "goods", GoodsMaintenanceSpend, "goods", "workshop", "shop", "trade"),
            EstimateConsumerDemand(world, districtId, "healthcare", HealthcareMaintenanceSpend, "healthcare", "clinic", "services")
        };
    }

    public EconomyDiagnosis Diagnose(WorldState world, int? districtId = null)
    {
        return EconomyDiagnosis.Analyze(world, districtId);
    }

    private static ConsumerDemandSnapshot EstimateConsumerDemand(
        WorldState world,
        int? districtId,
        string category,
        float maintenanceSpend,
        params string[] providerTerms)
    {
        var citizens = world.Citizens
            .Where(citizen => !districtId.HasValue || citizen.DistrictId == districtId)
            .ToList();
        var desiredSpending = citizens.Sum(citizen => CalculateDesiredNeedSpend(citizen, category, maintenanceSpend));
        var availableCash = citizens.Sum(citizen => Math.Max(0f, citizen.Cash));
        var providers = world.Businesses
            .Where(business => business.Status == BusinessStatus.Active)
            .Where(business => !districtId.HasValue || business.DistrictId == districtId)
            .Where(business => MatchesAny(business, providerTerms))
            .ToList();
        var availableSupplyValue = providers.Sum(business =>
            Math.Max(0f, business.LastProducedUnits - business.LastSoldUnits) * Math.Max(0f, business.UnitPrice));
        var averageQuality = providers.Count == 0 ? 0f : providers.Average(provider => provider.GetQualityDemandMultiplier());
        var achievableSpending = Math.Min(desiredSpending, Math.Min(availableCash, availableSupplyValue));
        var unmetDemand = Math.Max(0f, desiredSpending - achievableSpending);

        return new ConsumerDemandSnapshot(
            category,
            citizens.Count,
            desiredSpending,
            availableCash,
            availableSupplyValue,
            achievableSpending,
            unmetDemand,
            averageQuality);
    }

    private static float PurchaseNeed(
        WorldState world,
        Citizen citizen,
        float maintenanceSpend,
        float currentNeed,
        Action<float> setNeed,
        Func<float, float> satisfactionGain,
        MapAccessibilityReport? accessibility,
        params string[] providerTerms)
    {
        var desiredSpend = CalculateDesiredNeedSpend(currentNeed, maintenanceSpend);
        if (desiredSpend <= 0f || citizen.Cash <= 0f) return 0f;

        var provider = FindConsumerProvider(world, citizen.DistrictId, accessibility, providerTerms);
        if (provider == null)
        {
            return PurchaseImportedNeed(world, citizen, desiredSpend, currentNeed, setNeed, satisfactionGain);
        }

        var unitPrice = Math.Max(0.01f, provider.UnitPrice);
        var remainingUnits = Math.Max(0f, provider.LastProducedUnits - provider.LastSoldUnits);
        var availableValue = remainingUnits * unitPrice;
        if (availableValue <= 0f) return 0f;

        var spend = Math.Min(Math.Min(desiredSpend, citizen.Cash), availableValue);
        if (spend <= 0f) return 0f;

        var units = spend / unitPrice;
        citizen.Cash -= spend;
        provider.LastSoldUnits += units;
        provider.LastSalesRevenue += spend;
        provider.LastLocalSalesRevenue += spend;
        provider.RecordRevenue(spend);
        world.LastInternalTransfers += spend;
        setNeed(currentNeed + satisfactionGain(spend));
        return spend;
    }

    private static float PurchaseImportedNeed(
        WorldState world,
        Citizen citizen,
        float desiredLocalValue,
        float currentNeed,
        Action<float> setNeed,
        Func<float, float> satisfactionGain)
    {
        if (desiredLocalValue <= 0f || citizen.Cash <= 0f) return 0f;

        var importSpend = Math.Min(citizen.Cash, desiredLocalValue * ImportPriceMultiplier);
        if (importSpend <= 0f) return 0f;

        citizen.Cash -= importSpend;
        world.LastExternalOutflow += importSpend;
        var effectiveLocalValue = importSpend / ImportPriceMultiplier;
        setNeed(currentNeed + satisfactionGain(effectiveLocalValue));
        return importSpend;
    }

    private static float CalculateDesiredNeedSpend(Citizen citizen, string category, float maintenanceSpend)
    {
        var need = category.ToLowerInvariant() switch
        {
            "food" => citizen.FoodSatisfaction,
            "goods" => citizen.EntertainmentSatisfaction,
            "healthcare" => citizen.HealthcareSatisfaction,
            _ => citizen.Satisfaction
        };

        return CalculateDesiredNeedSpend(need, maintenanceSpend);
    }

    private static float CalculateDesiredNeedSpend(float currentNeed, float maintenanceSpend)
    {
        var needGap = Math.Max(0f, NeedTarget - currentNeed);
        return maintenanceSpend + needGap * 0.035f;
    }

    private static Business? FindConsumerProvider(
        WorldState world,
        int? districtId,
        MapAccessibilityReport? accessibility,
        params string[] terms)
    {
        return world.Businesses
            .Where(business => business.Status == BusinessStatus.Active)
            .Where(business => MatchesAny(business, terms))
            .Where(business => accessibility?.IsBusinessAccessible(business.Id) ?? true)
            .Where(HasRemainingConsumerSupply)
            .OrderByDescending(business => business.DistrictId == districtId)
            .ThenByDescending(business => business.GetQualityDemandMultiplier())
            .ThenBy(business => business.UnitPrice)
            .FirstOrDefault();
    }

    private static bool HasRemainingConsumerSupply(Business business)
    {
        return business.LastProducedUnits > business.LastSoldUnits && business.UnitPrice > 0f;
    }

    private static bool MatchesAny(Business business, IEnumerable<string> terms)
    {
        return terms.Any(term =>
            ContainsTerm(business.Type, term) ||
            ContainsTerm(business.ProductionType, term) ||
            ContainsTerm(business.Name, term));
    }

    private static bool ContainsTerm(string? value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static float ClampSatisfaction(float value) => Math.Clamp(value, 0f, 100f);

    public float ProcessGovernmentExpenses(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var budgetBefore = world.Budget;
        var activeProjects = world.Projects.Count(p => !p.Completed);
        var expenses = Math.Max(0f, world.BaseOperatingExpensePerTick) +
                       Math.Max(0f, world.ProjectOperatingExpensePerTick) * activeProjects;

        world.Budget -= expenses;
        world.LastOperatingExpenses = expenses;
        var localSpending = PayLocalGovernmentSuppliers(world, expenses * LocalGovernmentOperatingShare);
        var externalSpending = expenses - localSpending;
        world.LastLocalGovernmentSpending += localSpending;
        world.LastExternalGovernmentSpending += externalSpending;
        world.LastInternalTransfers += localSpending;
        world.LastExternalOutflow += externalSpending;
        world.LastNetBudgetChange += world.Budget - budgetBefore;
        return expenses;
    }

    public static float PayLocalGovernmentSuppliers(WorldState world, float amount, int? districtId = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        amount = Math.Max(0f, amount);
        if (amount <= 0f) return 0f;

        var suppliers = world.Businesses
            .Where(business => business.Status == BusinessStatus.Active)
            .Where(business => !districtId.HasValue || business.DistrictId == districtId.Value)
            .OrderByDescending(business => business.GetQualityDemandMultiplier())
            .ThenBy(business => business.Id)
            .ToList();

        if (suppliers.Count == 0 && districtId.HasValue)
        {
            suppliers = world.Businesses
                .Where(business => business.Status == BusinessStatus.Active)
                .OrderByDescending(business => business.GetQualityDemandMultiplier())
                .ThenBy(business => business.Id)
                .ToList();
        }

        if (suppliers.Count == 0) return 0f;

        var perSupplier = amount / suppliers.Count;
        foreach (var supplier in suppliers)
        {
            supplier.RecordRevenue(perSupplier);
        }

        return amount;
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

    public int UpdateBusinessViability(
        WorldState world,
        float bankruptcyProfitThreshold = 0f,
        int lossTicksToBankruptcy = DefaultBankruptcyLossTicks,
        MapAccessibilityReport? accessibility = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        lossTicksToBankruptcy = Math.Max(1, lossTicksToBankruptcy);

        var closed = 0;
        foreach (var business in world.Businesses.Where(b => b.Status == BusinessStatus.Active).ToList())
        {
            var pressure = EvaluateBusinessPressure(world, business, bankruptcyProfitThreshold, accessibility);

            if (pressure.IsUnderPressure)
            {
                business.ConsecutiveLossTicks++;
            }
            else
            {
                business.ConsecutiveLossTicks = 0;
            }

            if (business.ConsecutiveLossTicks >= lossTicksToBankruptcy)
            {
                CloseBusiness(world, business, BusinessStatus.Bankrupt, pressure.Reason);
                closed++;
            }
        }

        if (closed > 0)
        {
            world.RefreshMapAccessibility();
        }

        return closed;
    }

    private static BusinessPressure EvaluateBusinessPressure(
        WorldState world,
        Business business,
        float bankruptcyProfitThreshold,
        MapAccessibilityReport? accessibility)
    {
        var hasTickAccounting = Math.Abs(business.RevenueThisTick) > 0.001f || Math.Abs(business.ExpensesThisTick) > 0.001f;
        var payrollReserve = Math.Max(0f, business.WagePerEmployee) * Math.Max(1, business.EmployeeIds.Count);
        var hasCumulativeLoss = business.GetProfit() < bankruptcyProfitThreshold;
        var hasDebtPressure = business.Cash <= -payrollReserve;
        var hasCashflowPressure = hasTickAccounting
            ? hasDebtPressure || (business.ProfitThisTick < bankruptcyProfitThreshold && business.Cash < payrollReserve && hasCumulativeLoss)
            : business.Cash <= bankruptcyProfitThreshold && hasCumulativeLoss;
        if (hasCashflowPressure)
        {
            return new BusinessPressure(true, "cashflow");
        }

        if (accessibility != null && !accessibility.IsBusinessAccessible(business.Id))
        {
            return new BusinessPressure(true, "road access");
        }

        if (business.MaxEmployees > 0 && business.EmployeeIds.Count == 0 && world.Citizens.Any(IsAvailableForWork))
        {
            return new BusinessPressure(true, "staff shortage");
        }

        var hadProductionToSell = business.LastProducedUnits > 0.001f;
        var hadSales = business.LastSoldUnits > 0.001f || business.RevenueThisTick > 0.001f;
        if (hadProductionToSell && !hadSales)
        {
            return new BusinessPressure(true, "demand");
        }

        return new BusinessPressure(false, string.Empty);
    }

    public Business? TryOpenBusiness(
        WorldState world,
        BusinessTypeCatalog catalog,
        string typeId,
        int? districtId = null,
        float minimumBudget = 12000f,
        float maximumUnemploymentRate = 20f,
        float startingCash = 0f)
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
            Cash = Math.Max(0f, startingCash),
            Status = BusinessStatus.Active
        };

        world.Businesses.Add(business);
        if (business.Cash > 0f)
        {
            world.LastExternalInflow += business.Cash;
        }

        world.Events.Add(new GameEvent(
            $"Business opened: {business.Name}",
            $"Business {business.Name} opened to cover {definition.ProductionType} demand.",
            EventType.Economic)
        {
            CreatedAtTick = world.Clock.CurrentTick
        });
        world.RefreshMapAccessibility();
        return business;
    }

    public Business? TryOpenNeededBusiness(
        WorldState world,
        BusinessTypeCatalog catalog,
        int? districtId = null,
        BusinessOpeningRules? rules = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        rules ??= BusinessOpeningRules.Default;
        var candidate = catalog.Types
            .Select(definition => new
            {
                Definition = definition,
                DemandGap = EstimateDemandGap(world, definition, districtId)
            })
            .Where(candidate => candidate.DemandGap >= rules.MinimumDemandGap)
            .OrderByDescending(candidate => candidate.DemandGap)
            .FirstOrDefault(candidate => CanOpenBusiness(world, candidate.Definition, districtId, rules));

        if (candidate == null) return null;

        return TryOpenBusiness(
            world,
            catalog,
            candidate.Definition.Id,
            districtId,
            rules.MinimumBudget,
            rules.MaximumUnemploymentRate,
            rules.StartingCash);
    }

    public float EstimateDemandGap(WorldState world, BusinessTypeDefinition definition, int? districtId = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        var category = DemandCategoryForProductionType(definition.ProductionType);
        var demand = EstimateConsumerDemand(world, districtId)
            .FirstOrDefault(snapshot => string.Equals(snapshot.Category, category, StringComparison.OrdinalIgnoreCase));
        if (demand == null || demand.Population == 0) return 0f;

        var needGap = demand.Population <= 0
            ? 0f
            : Math.Clamp(demand.UnmetDemand / demand.Population * 20f, 0f, 100f);
        var sameTypeBusinesses = CountActiveBusinessesForDefinition(world, definition, districtId);
        var capacityCoverage = sameTypeBusinesses * 12f;
        return Math.Clamp(needGap - capacityCoverage, 0f, 100f);
    }

    private bool CanOpenBusiness(
        WorldState world,
        BusinessTypeDefinition definition,
        int? districtId,
        BusinessOpeningRules rules)
    {
        if (world.Budget < rules.MinimumBudget) return false;
        if (!rules.HasBuildableLocation) return false;
        if (GetUnemploymentRate(world) > rules.MaximumUnemploymentRate) return false;
        if (CountAvailableWorkers(world) < rules.MinimumAvailableWorkers) return false;
        if (CountActiveBusinessesForDefinition(world, definition, districtId) >= rules.MaximumSameTypePerDistrict) return false;

        return true;
    }

    private static int CountAvailableWorkers(WorldState world)
    {
        return world.Citizens.Count(IsAvailableForWork);
    }

    private static int CountActiveBusinessesForDefinition(WorldState world, BusinessTypeDefinition definition, int? districtId)
    {
        return world.Businesses.Count(business =>
            business.Status == BusinessStatus.Active &&
            (!districtId.HasValue || business.DistrictId == districtId) &&
            (string.Equals(business.Type, definition.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(business.ProductionType, definition.ProductionType, StringComparison.OrdinalIgnoreCase)));
    }

    private static string DemandCategoryForProductionType(string productionType)
    {
        return productionType.ToLowerInvariant() switch
        {
            "food" => "food",
            "healthcare" or "services" => "healthcare",
            "goods" or "trade" => "goods",
            _ => "goods"
        };
    }

    public float GetUnemploymentRate(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        var laborForce = world.Citizens.Where(IsEligibleForWork).ToList();
        if (laborForce.Count == 0) return 0f;
        var unemployed = laborForce.Count(c => string.IsNullOrEmpty(c.Job));
        return (unemployed / (float)laborForce.Count) * 100f;
    }

    private static void CloseBusiness(WorldState world, Business business, BusinessStatus status, string reason)
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
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "sustained viability problem" : reason;
        world.Events.Add(new GameEvent(
            $"Business closed: {business.Name}",
            $"Business {business.Name} closed with status {status} due to {reasonText}.",
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

public sealed class BusinessOpeningRules
{
    public static BusinessOpeningRules Default { get; } = new();

    public float MinimumBudget { get; init; } = 12000f;
    public float MaximumUnemploymentRate { get; init; } = 100f;
    public int MinimumAvailableWorkers { get; init; } = 1;
    public float MinimumDemandGap { get; init; } = 18f;
    public int MaximumSameTypePerDistrict { get; init; } = 2;
    public bool HasBuildableLocation { get; init; } = true;
    public float StartingCash { get; init; } = 2500f;
}

public sealed record ConsumerDemandSnapshot(
    string Category,
    int Population,
    float DesiredSpending,
    float AvailableCash,
    float AvailableSupplyValue,
    float AchievableSpending,
    float UnmetDemand,
    float AverageQuality);

internal readonly record struct BusinessPressure(bool IsUnderPressure, string Reason);
