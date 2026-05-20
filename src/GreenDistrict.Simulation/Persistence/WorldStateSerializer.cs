using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Persistence;

public static class WorldStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static string SaveJson(WorldState world)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        return JsonSerializer.Serialize(ToSave(world), Options);
    }

    public static void SaveJsonFile(WorldState world, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path cannot be empty.", nameof(path));
        File.WriteAllText(path, SaveJson(world));
    }

    public static WorldState LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save JSON cannot be empty.", nameof(json));

        var save = JsonSerializer.Deserialize<WorldSave>(json, Options);
        if (save == null) throw new InvalidOperationException("Save JSON is invalid.");

        return FromSave(save);
    }

    public static WorldState LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path cannot be empty.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    private static WorldSave ToSave(WorldState world)
    {
        return new WorldSave
        {
            CurrentTick = world.Clock.CurrentTick,
            TimeScale = world.Clock.TimeScale,
            Budget = world.Budget,
            SupportRating = world.SupportRating,
            IsInPower = world.IsInPower,
            IncomeTaxRate = world.IncomeTaxRate,
            BusinessTaxRate = world.BusinessTaxRate,
            BaseOperatingExpensePerTick = world.BaseOperatingExpensePerTick,
            ProjectOperatingExpensePerTick = world.ProjectOperatingExpensePerTick,
            ElectionIntervalTicks = world.ElectionIntervalTicks,
            SimulationSeed = world.SimulationSeed,
            EconomicTickInterval = world.EconomicTickInterval,
            DemographyTicksPerYear = world.DemographyTicksPerYear,
            BirthRatePerPersonPerYear = world.BirthRatePerPersonPerYear,
            BaseDeathRatePerPersonPerYear = world.BaseDeathRatePerPersonPerYear,
            MigrationRatePerPersonPerYear = world.MigrationRatePerPersonPerYear,
            LastUnemploymentRate = world.LastUnemploymentRate,
            LastIncomeTaxCollected = world.LastIncomeTaxCollected,
            LastBusinessTaxCollected = world.LastBusinessTaxCollected,
            LastOperatingExpenses = world.LastOperatingExpenses,
            LastNetBudgetChange = world.LastNetBudgetChange,
            LastSalesRevenueGenerated = world.LastSalesRevenueGenerated,
            LastGrossWagesPaid = world.LastGrossWagesPaid,
            LastNetWagesPaid = world.LastNetWagesPaid,
            LastProjectSpending = world.LastProjectSpending,
            LastProjectBenefits = world.LastProjectBenefits,
            LastProjectRefunds = world.LastProjectRefunds,
            LastLocalGovernmentSpending = world.LastLocalGovernmentSpending,
            LastExternalGovernmentSpending = world.LastExternalGovernmentSpending,
            LastConsumerSpending = world.LastConsumerSpending,
            LastExternalInflow = world.LastExternalInflow,
            LastExternalOutflow = world.LastExternalOutflow,
            LastInternalTransfers = world.LastInternalTransfers,
            LastElectionTick = world.LastElectionTick,
            LastElectionSupport = world.LastElectionSupport,
            ElectionCount = world.ElectionCount,
            Citizens = world.Citizens.Select(ToSave).ToList(),
            Households = world.Households.Select(ToSave).ToList(),
            HousingUnits = world.HousingUnits.Select(ToSave).ToList(),
            Districts = world.Districts.Select(ToSave).ToList(),
            Businesses = world.Businesses.Select(ToSave).ToList(),
            Projects = world.Projects.Select(ToSave).ToList(),
            Events = world.Events.Select(ToSave).ToList()
        };
    }

    private static WorldState FromSave(WorldSave save)
    {
        var world = new WorldState
        {
            Budget = save.Budget,
            SupportRating = Math.Clamp(save.SupportRating, 0f, 100f),
            IsInPower = save.IsInPower,
            IncomeTaxRate = Math.Clamp(save.IncomeTaxRate, 0f, 1f),
            BusinessTaxRate = Math.Clamp(save.BusinessTaxRate, 0f, 1f),
            BaseOperatingExpensePerTick = Math.Max(0f, save.BaseOperatingExpensePerTick),
            ProjectOperatingExpensePerTick = Math.Max(0f, save.ProjectOperatingExpensePerTick),
            ElectionIntervalTicks = save.ElectionIntervalTicks
        };
        world.Clock.SetCurrentTick(save.CurrentTick);
        world.Clock.TimeScale = save.TimeScale;
        world.ConfigureEconomicTickInterval(save.EconomicTickInterval);
        world.ConfigureDemography(
            save.SimulationSeed,
            save.DemographyTicksPerYear,
            save.BirthRatePerPersonPerYear,
            save.BaseDeathRatePerPersonPerYear,
            save.MigrationRatePerPersonPerYear);

        foreach (var districtSave in save.Districts)
        {
            world.Districts.Add(new District(districtSave.Name)
            {
                Id = districtSave.Id,
                Population = districtSave.Population,
                AverageSatisfaction = districtSave.AverageSatisfaction,
                AverageHousingSatisfaction = districtSave.AverageHousingSatisfaction,
                AverageSafetySatisfaction = districtSave.AverageSafetySatisfaction,
                AverageHealthcareSatisfaction = districtSave.AverageHealthcareSatisfaction,
                AverageEntertainmentSatisfaction = districtSave.AverageEntertainmentSatisfaction,
                ServiceLevel = districtSave.ServiceLevel,
                EconomicLevel = districtSave.EconomicLevel,
                SupportRating = districtSave.SupportRating,
                CrisisRisk = districtSave.CrisisRisk,
                HasActiveCrisis = districtSave.HasActiveCrisis,
                LastCrisisEventTick = districtSave.LastCrisisEventTick,
                TotalJobs = districtSave.TotalJobs,
                OpenJobs = districtSave.OpenJobs,
                EmploymentRate = districtSave.EmploymentRate,
                HousingCapacity = districtSave.HousingCapacity,
                OccupiedHousing = districtSave.OccupiedHousing,
                AvailableHousing = districtSave.AvailableHousing,
                ActiveProjects = districtSave.ActiveProjects,
                CompletedProjects = districtSave.CompletedProjects
            });
        }

        foreach (var housingSave in save.HousingUnits)
        {
            world.HousingUnits.Add(new HousingUnit(
                housingSave.Id,
                housingSave.DistrictId,
                housingSave.Capacity,
                housingSave.RentPerTick)
            {
                HouseholdId = housingSave.HouseholdId
            });
        }

        foreach (var citizenSave in save.Citizens)
        {
            var citizen = new Citizen(citizenSave.Name, citizenSave.Age, citizenSave.Profession, citizenSave.Gender, citizenSave.Id)
            {
                DistrictId = citizenSave.DistrictId,
                FamilyName = citizenSave.FamilyName,
                HouseholdId = citizenSave.HouseholdId,
                MotherId = citizenSave.MotherId,
                FatherId = citizenSave.FatherId,
                Cash = citizenSave.Cash > 0f ? citizenSave.Cash : citizenSave.Income,
                Income = citizenSave.Income,
                Satisfaction = citizenSave.Satisfaction,
                Mood = citizenSave.Mood,
                Health = citizenSave.Health,
                FoodSatisfaction = citizenSave.FoodSatisfaction,
                HousingSatisfaction = citizenSave.HousingSatisfaction,
                SafetySatisfaction = citizenSave.SafetySatisfaction,
                HealthcareSatisfaction = citizenSave.HealthcareSatisfaction,
                EntertainmentSatisfaction = citizenSave.EntertainmentSatisfaction
            };
            citizen.Job = citizenSave.Job;
            if (citizenSave.IsRetired)
            {
                citizen.Retire();
                citizen.Profession = citizenSave.Profession;
            }
            world.Citizens.Add(citizen);
        }

        foreach (var householdSave in save.Households)
        {
            var household = new Household(
                householdSave.DistrictId,
                householdSave.HousingUnitId,
                householdSave.HousingCapacity,
                householdSave.RentPerTick,
                householdSave.Id);
            household.MemberIds.AddRange(householdSave.MemberIds);
            world.Households.Add(household);
        }

        foreach (var businessSave in save.Businesses)
        {
            var business = new Business(businessSave.Name, businessSave.Type, businessSave.MaxEmployees)
            {
                Id = businessSave.Id,
                DistrictId = businessSave.DistrictId,
                ProductionType = businessSave.ProductionType,
                WagePerEmployee = businessSave.WagePerEmployee,
                EmployeeCount = businessSave.EmployeeCount,
                BaseOutput = businessSave.BaseOutput,
                UnitPrice = businessSave.UnitPrice,
                DemandMultiplier = businessSave.DemandMultiplier,
                BusinessLevel = Math.Clamp(businessSave.BusinessLevel <= 0 ? 1 : businessSave.BusinessLevel, 1, Business.MaxBusinessLevel),
                ProductQuality = businessSave.ProductQuality <= 0f ? 1f : businessSave.ProductQuality,
                InvestmentReserve = Math.Max(0f, businessSave.InvestmentReserve),
                LastInvestment = Math.Max(0f, businessSave.LastInvestment),
                Cash = businessSave.Cash > 0f ? businessSave.Cash : Math.Max(0f, businessSave.Revenue - businessSave.Expenses),
                Revenue = businessSave.Revenue,
                Expenses = businessSave.Expenses,
                RevenueThisTick = businessSave.RevenueThisTick,
                ExpensesThisTick = businessSave.ExpensesThisTick,
                TotalRevenue = businessSave.TotalRevenue > 0f ? businessSave.TotalRevenue : businessSave.Revenue,
                TotalExpenses = businessSave.TotalExpenses > 0f ? businessSave.TotalExpenses : businessSave.Expenses,
                LastProducedUnits = businessSave.LastProducedUnits,
                LastSoldUnits = businessSave.LastSoldUnits,
                LastSalesRevenue = businessSave.LastSalesRevenue,
                LastLocalSalesRevenue = businessSave.LastLocalSalesRevenue,
                LastExternalSalesRevenue = businessSave.LastExternalSalesRevenue,
                Status = businessSave.Status,
                ConsecutiveLossTicks = businessSave.ConsecutiveLossTicks,
                ClosedAtTick = businessSave.ClosedAtTick
            };
            business.EmployeeIds.AddRange(businessSave.EmployeeIds);
            world.Businesses.Add(business);
        }

        foreach (var projectSave in save.Projects)
        {
            world.Projects.Add(new GovernmentProject(projectSave.Name, projectSave.Cost, projectSave.DurationTicks, projectSave.Benefit, projectSave.Id)
            {
                RemainingTicks = projectSave.RemainingTicks,
                Type = projectSave.Type,
                DistrictId = projectSave.DistrictId,
                FoodSatisfactionEffect = projectSave.FoodSatisfactionEffect,
                HousingSatisfactionEffect = projectSave.HousingSatisfactionEffect,
                SafetySatisfactionEffect = projectSave.SafetySatisfactionEffect,
                HealthcareSatisfactionEffect = projectSave.HealthcareSatisfactionEffect,
                EntertainmentSatisfactionEffect = projectSave.EntertainmentSatisfactionEffect,
                SupportEffect = projectSave.SupportEffect,
                HousingUnitsToCreate = projectSave.HousingUnitsToCreate,
                HousingUnitCapacity = projectSave.HousingUnitCapacity,
                HousingUnitRentPerTick = projectSave.HousingUnitRentPerTick,
                LocalCostPaid = projectSave.LocalCostPaid,
                ExternalCostPaid = projectSave.ExternalCostPaid,
                Completed = projectSave.Completed,
                StartTick = projectSave.StartTick
            });
        }

        foreach (var eventSave in save.Events)
        {
            var gameEvent = new GameEvent(eventSave.Title, eventSave.Description, eventSave.Type, eventSave.Id)
            {
                CreatedAtTick = eventSave.CreatedAtTick,
                IsResolved = eventSave.IsResolved,
                SelectedChoiceId = eventSave.SelectedChoiceId
            };
            gameEvent.Choices.AddRange(eventSave.Choices.Select(ToChoice));
            world.Events.Add(gameEvent);
        }

        world.RecalculateHouseholds();
        world.DistrictsSystem.UpdateDistrictAggregates(world);
        world.RestoreRuntimeMetrics(
            save.LastUnemploymentRate,
            save.LastIncomeTaxCollected,
            save.LastBusinessTaxCollected,
            save.LastOperatingExpenses,
            save.LastNetBudgetChange,
            save.LastSalesRevenueGenerated,
            save.LastGrossWagesPaid,
            save.LastNetWagesPaid,
            save.LastProjectSpending,
            save.LastProjectBenefits,
            save.LastProjectRefunds,
            save.LastLocalGovernmentSpending,
            save.LastExternalGovernmentSpending,
            save.LastConsumerSpending,
            save.LastExternalInflow,
            save.LastExternalOutflow,
            save.LastInternalTransfers,
            save.LastElectionTick,
            save.LastElectionSupport,
            save.ElectionCount);
        world.RefreshMapAccessibility();
        return world;
    }

    private static CitizenSave ToSave(Citizen citizen) => new()
    {
        Id = citizen.Id,
        Name = citizen.Name,
        Age = citizen.Age,
        DistrictId = citizen.DistrictId,
        Profession = citizen.Profession,
        FamilyName = citizen.FamilyName,
        Job = citizen.Job,
        Cash = citizen.Cash,
        Income = citizen.Income,
        Satisfaction = citizen.Satisfaction,
        Mood = citizen.Mood,
        Health = citizen.Health,
        IsRetired = citizen.IsRetired,
        Gender = citizen.Gender,
        HouseholdId = citizen.HouseholdId,
        MotherId = citizen.MotherId,
        FatherId = citizen.FatherId,
        FoodSatisfaction = citizen.FoodSatisfaction,
        HousingSatisfaction = citizen.HousingSatisfaction,
        SafetySatisfaction = citizen.SafetySatisfaction,
        HealthcareSatisfaction = citizen.HealthcareSatisfaction,
        EntertainmentSatisfaction = citizen.EntertainmentSatisfaction
    };

    private static HouseholdSave ToSave(Household household) => new()
    {
        Id = household.Id,
        DistrictId = household.DistrictId,
        HousingUnitId = household.HousingUnitId,
        HousingCapacity = household.HousingCapacity,
        RentPerTick = household.RentPerTick,
        MemberIds = household.MemberIds.ToList()
    };

    private static HousingUnitSave ToSave(HousingUnit housingUnit) => new()
    {
        Id = housingUnit.Id,
        DistrictId = housingUnit.DistrictId,
        Capacity = housingUnit.Capacity,
        RentPerTick = housingUnit.RentPerTick,
        HouseholdId = housingUnit.HouseholdId
    };

    private static DistrictSave ToSave(District district) => new()
    {
        Id = district.Id,
        Name = district.Name,
        Population = district.Population,
        AverageSatisfaction = district.AverageSatisfaction,
        AverageHousingSatisfaction = district.AverageHousingSatisfaction,
        AverageSafetySatisfaction = district.AverageSafetySatisfaction,
        AverageHealthcareSatisfaction = district.AverageHealthcareSatisfaction,
        AverageEntertainmentSatisfaction = district.AverageEntertainmentSatisfaction,
        ServiceLevel = district.ServiceLevel,
        EconomicLevel = district.EconomicLevel,
        SupportRating = district.SupportRating,
        CrisisRisk = district.CrisisRisk,
        HasActiveCrisis = district.HasActiveCrisis,
        LastCrisisEventTick = district.LastCrisisEventTick,
        TotalJobs = district.TotalJobs,
        OpenJobs = district.OpenJobs,
        EmploymentRate = district.EmploymentRate,
        HousingCapacity = district.HousingCapacity,
        OccupiedHousing = district.OccupiedHousing,
        AvailableHousing = district.AvailableHousing,
        ActiveProjects = district.ActiveProjects,
        CompletedProjects = district.CompletedProjects
    };

    private static BusinessSave ToSave(Business business) => new()
    {
        Id = business.Id,
        Name = business.Name,
        DistrictId = business.DistrictId,
        Type = business.Type,
        ProductionType = business.ProductionType,
        EmployeeIds = business.EmployeeIds.ToList(),
        WagePerEmployee = business.WagePerEmployee,
        EmployeeCount = business.EmployeeCount,
        MaxEmployees = business.MaxEmployees,
        BaseOutput = business.BaseOutput,
        UnitPrice = business.UnitPrice,
        DemandMultiplier = business.DemandMultiplier,
        BusinessLevel = business.BusinessLevel,
        ProductQuality = business.ProductQuality,
        InvestmentReserve = business.InvestmentReserve,
        LastInvestment = business.LastInvestment,
        Cash = business.Cash,
        Revenue = business.Revenue,
        Expenses = business.Expenses,
        RevenueThisTick = business.RevenueThisTick,
        ExpensesThisTick = business.ExpensesThisTick,
        TotalRevenue = business.TotalRevenue,
        TotalExpenses = business.TotalExpenses,
        LastProducedUnits = business.LastProducedUnits,
        LastSoldUnits = business.LastSoldUnits,
        LastSalesRevenue = business.LastSalesRevenue,
        LastLocalSalesRevenue = business.LastLocalSalesRevenue,
        LastExternalSalesRevenue = business.LastExternalSalesRevenue,
        Status = business.Status,
        ConsecutiveLossTicks = business.ConsecutiveLossTicks,
        ClosedAtTick = business.ClosedAtTick
    };

    private static ProjectSave ToSave(GovernmentProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Cost = project.Cost,
        DurationTicks = project.DurationTicks,
        RemainingTicks = project.RemainingTicks,
        Type = project.Type,
        Benefit = project.Benefit,
        DistrictId = project.DistrictId,
        FoodSatisfactionEffect = project.FoodSatisfactionEffect,
        HousingSatisfactionEffect = project.HousingSatisfactionEffect,
        SafetySatisfactionEffect = project.SafetySatisfactionEffect,
        HealthcareSatisfactionEffect = project.HealthcareSatisfactionEffect,
        EntertainmentSatisfactionEffect = project.EntertainmentSatisfactionEffect,
        SupportEffect = project.SupportEffect,
        HousingUnitsToCreate = project.HousingUnitsToCreate,
        HousingUnitCapacity = project.HousingUnitCapacity,
        HousingUnitRentPerTick = project.HousingUnitRentPerTick,
        LocalCostPaid = project.LocalCostPaid,
        ExternalCostPaid = project.ExternalCostPaid,
        Completed = project.Completed,
        StartTick = project.StartTick
    };

    private static EventSave ToSave(GameEvent gameEvent) => new()
    {
        Id = gameEvent.Id,
        Title = gameEvent.Title,
        Description = gameEvent.Description,
        CreatedAtTick = gameEvent.CreatedAtTick,
        Type = gameEvent.Type,
        IsResolved = gameEvent.IsResolved,
        SelectedChoiceId = gameEvent.SelectedChoiceId,
        Choices = gameEvent.Choices.Select(ToSave).ToList()
    };

    private static EventChoiceSave ToSave(EventChoice choice) => new()
    {
        Id = choice.Id,
        Label = choice.Label,
        Description = choice.Description,
        DistrictId = choice.DistrictId,
        BudgetEffect = choice.BudgetEffect,
        SupportEffect = choice.SupportEffect,
        FoodSatisfactionEffect = choice.FoodSatisfactionEffect,
        HousingSatisfactionEffect = choice.HousingSatisfactionEffect,
        SafetySatisfactionEffect = choice.SafetySatisfactionEffect,
        HealthcareSatisfactionEffect = choice.HealthcareSatisfactionEffect,
        EntertainmentSatisfactionEffect = choice.EntertainmentSatisfactionEffect,
        ResolveDistrictCrisis = choice.ResolveDistrictCrisis
    };

    private static EventChoice ToChoice(EventChoiceSave save) => new(save.Id, save.Label, save.Description)
    {
        DistrictId = save.DistrictId,
        BudgetEffect = save.BudgetEffect,
        SupportEffect = save.SupportEffect,
        FoodSatisfactionEffect = save.FoodSatisfactionEffect,
        HousingSatisfactionEffect = save.HousingSatisfactionEffect,
        SafetySatisfactionEffect = save.SafetySatisfactionEffect,
        HealthcareSatisfactionEffect = save.HealthcareSatisfactionEffect,
        EntertainmentSatisfactionEffect = save.EntertainmentSatisfactionEffect,
        ResolveDistrictCrisis = save.ResolveDistrictCrisis
    };
}

public class WorldSave
{
    public long CurrentTick { get; set; }
    public float TimeScale { get; set; } = 1f;
    public float Budget { get; set; }
    public float SupportRating { get; set; }
    public bool IsInPower { get; set; }
    public float IncomeTaxRate { get; set; }
    public float BusinessTaxRate { get; set; }
    public float BaseOperatingExpensePerTick { get; set; }
    public float ProjectOperatingExpensePerTick { get; set; }
    public int ElectionIntervalTicks { get; set; }
    public int SimulationSeed { get; set; }
    public int EconomicTickInterval { get; set; } = 1440;
    public int DemographyTicksPerYear { get; set; } = 1440 * 365;
    public float BirthRatePerPersonPerYear { get; set; } = 0.02f;
    public float BaseDeathRatePerPersonPerYear { get; set; } = 0.01f;
    public float MigrationRatePerPersonPerYear { get; set; } = 0.005f;
    public float LastUnemploymentRate { get; set; }
    public float LastIncomeTaxCollected { get; set; }
    public float LastBusinessTaxCollected { get; set; }
    public float LastOperatingExpenses { get; set; }
    public float LastNetBudgetChange { get; set; }
    public float LastSalesRevenueGenerated { get; set; }
    public float LastGrossWagesPaid { get; set; }
    public float LastNetWagesPaid { get; set; }
    public float LastProjectSpending { get; set; }
    public float LastProjectBenefits { get; set; }
    public float LastProjectRefunds { get; set; }
    public float LastLocalGovernmentSpending { get; set; }
    public float LastExternalGovernmentSpending { get; set; }
    public float LastConsumerSpending { get; set; }
    public float LastExternalInflow { get; set; }
    public float LastExternalOutflow { get; set; }
    public float LastInternalTransfers { get; set; }
    public long LastElectionTick { get; set; } = -1;
    public float LastElectionSupport { get; set; }
    public int ElectionCount { get; set; }
    public List<CitizenSave> Citizens { get; set; } = new();
    public List<HouseholdSave> Households { get; set; } = new();
    public List<HousingUnitSave> HousingUnits { get; set; } = new();
    public List<DistrictSave> Districts { get; set; } = new();
    public List<BusinessSave> Businesses { get; set; } = new();
    public List<ProjectSave> Projects { get; set; } = new();
    public List<EventSave> Events { get; set; } = new();
}

public class CitizenSave
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public int? DistrictId { get; set; }
    public string Profession { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string? Job { get; set; }
    public float Cash { get; set; }
    public float Income { get; set; }
    public float Satisfaction { get; set; }
    public float Mood { get; set; }
    public float Health { get; set; }
    public bool IsRetired { get; set; }
    public Gender Gender { get; set; }
    public int? HouseholdId { get; set; }
    public int? MotherId { get; set; }
    public int? FatherId { get; set; }
    public float FoodSatisfaction { get; set; }
    public float HousingSatisfaction { get; set; }
    public float SafetySatisfaction { get; set; }
    public float HealthcareSatisfaction { get; set; }
    public float EntertainmentSatisfaction { get; set; }
}

public class HouseholdSave
{
    public int Id { get; set; }
    public int? DistrictId { get; set; }
    public int? HousingUnitId { get; set; }
    public int HousingCapacity { get; set; }
    public float RentPerTick { get; set; }
    public List<int> MemberIds { get; set; } = new();
}

public class HousingUnitSave
{
    public int Id { get; set; }
    public int? DistrictId { get; set; }
    public int Capacity { get; set; }
    public float RentPerTick { get; set; }
    public int? HouseholdId { get; set; }
}

public class DistrictSave
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Population { get; set; }
    public float AverageSatisfaction { get; set; }
    public float AverageHousingSatisfaction { get; set; }
    public float AverageSafetySatisfaction { get; set; }
    public float AverageHealthcareSatisfaction { get; set; }
    public float AverageEntertainmentSatisfaction { get; set; }
    public float ServiceLevel { get; set; }
    public float EconomicLevel { get; set; }
    public float SupportRating { get; set; }
    public float CrisisRisk { get; set; }
    public bool HasActiveCrisis { get; set; }
    public long LastCrisisEventTick { get; set; } = long.MinValue;
    public int TotalJobs { get; set; }
    public int OpenJobs { get; set; }
    public float EmploymentRate { get; set; }
    public int HousingCapacity { get; set; }
    public int OccupiedHousing { get; set; }
    public int AvailableHousing { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
}

public class BusinessSave
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DistrictId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ProductionType { get; set; } = string.Empty;
    public List<int> EmployeeIds { get; set; } = new();
    public float WagePerEmployee { get; set; }
    public int EmployeeCount { get; set; }
    public int MaxEmployees { get; set; }
    public float BaseOutput { get; set; }
    public float UnitPrice { get; set; }
    public float DemandMultiplier { get; set; }
    public int BusinessLevel { get; set; } = 1;
    public float ProductQuality { get; set; } = 1f;
    public float InvestmentReserve { get; set; }
    public float LastInvestment { get; set; }
    public float Cash { get; set; }
    public float Revenue { get; set; }
    public float Expenses { get; set; }
    public float RevenueThisTick { get; set; }
    public float ExpensesThisTick { get; set; }
    public float TotalRevenue { get; set; }
    public float TotalExpenses { get; set; }
    public float LastProducedUnits { get; set; }
    public float LastSoldUnits { get; set; }
    public float LastSalesRevenue { get; set; }
    public float LastLocalSalesRevenue { get; set; }
    public float LastExternalSalesRevenue { get; set; }
    public BusinessStatus Status { get; set; }
    public int ConsecutiveLossTicks { get; set; }
    public long? ClosedAtTick { get; set; }
}

public class ProjectSave
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Cost { get; set; }
    public int DurationTicks { get; set; }
    public int RemainingTicks { get; set; }
    public ProjectType Type { get; set; }
    public float Benefit { get; set; }
    public int? DistrictId { get; set; }
    public float FoodSatisfactionEffect { get; set; }
    public float HousingSatisfactionEffect { get; set; }
    public float SafetySatisfactionEffect { get; set; }
    public float HealthcareSatisfactionEffect { get; set; }
    public float EntertainmentSatisfactionEffect { get; set; }
    public float SupportEffect { get; set; }
    public int HousingUnitsToCreate { get; set; }
    public int HousingUnitCapacity { get; set; }
    public float HousingUnitRentPerTick { get; set; }
    public float LocalCostPaid { get; set; }
    public float ExternalCostPaid { get; set; }
    public bool Completed { get; set; }
    public long StartTick { get; set; }
}

public class EventSave
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long CreatedAtTick { get; set; }
    public EventType Type { get; set; }
    public bool IsResolved { get; set; }
    public string? SelectedChoiceId { get; set; }
    public List<EventChoiceSave> Choices { get; set; } = new();
}

public class EventChoiceSave
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DistrictId { get; set; }
    public float BudgetEffect { get; set; }
    public float SupportEffect { get; set; }
    public float FoodSatisfactionEffect { get; set; }
    public float HousingSatisfactionEffect { get; set; }
    public float SafetySatisfactionEffect { get; set; }
    public float HealthcareSatisfactionEffect { get; set; }
    public float EntertainmentSatisfactionEffect { get; set; }
    public bool ResolveDistrictCrisis { get; set; }
}
