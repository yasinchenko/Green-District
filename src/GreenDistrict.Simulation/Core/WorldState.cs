namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Central repository for all game world state.
/// All systems read and write to this state.
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using GreenDistrict.Simulation.Government;
using GreenDistrict.Simulation.Economy;
using GreenDistrict.Simulation.Needs;
using GreenDistrict.Simulation.World;
using GreenDistrict.Simulation.Demography;
using GreenDistrict.Simulation.Behavior;
using GreenDistrict.Simulation.Scenarios;

    public class WorldState
{
    private SimulationClock _clock;
    private UpdateManager _updateManager;
    private GovernmentSystem _governmentSystem;
    private EconomySystem _economySystem;
    private NeedsSystem _needsSystem;
    private DistrictSystem _districtSystem;
    private DemographySystem _demographySystem;
    private BehaviorSystem _behaviorSystem;
    
    // Core collections
    public List<Citizen> Citizens { get; } = new();
    public List<Household> Households { get; } = new();
    public List<HousingUnit> HousingUnits { get; } = new();
    public List<District> Districts { get; } = new();
    public List<Business> Businesses { get; } = new();
    public List<GovernmentProject> Projects { get; } = new();
    public List<GameEvent> Events { get; } = new();
    
    // Player state
    public float Budget { get; set; } = 10000f;
    public float SupportRating { get; set; } = 75f; // 0-100%
    public bool IsInPower { get; set; } = true;
    public float IncomeTaxRate { get; set; } = 0.15f;
    public float BusinessTaxRate { get; set; } = 0.10f;
    public float BaseOperatingExpensePerTick { get; set; } = 0f;
    public float ProjectOperatingExpensePerTick { get; set; } = 0f;
    
    public SimulationClock Clock => _clock;
    public UpdateManager UpdateManager => _updateManager;
    public GovernmentSystem Government => _governmentSystem;
    public EconomySystem Economy => _economySystem;
    public NeedsSystem Needs => _needsSystem;
    public DistrictSystem DistrictsSystem => _districtSystem;
    public DemographySystem Demography => _demographySystem;
    public BehaviorSystem Behavior => _behaviorSystem;
    // Last computed metrics
    public float LastUnemploymentRate { get; private set; }
    public float LastIncomeTaxCollected { get; set; }
    public float LastBusinessTaxCollected { get; set; }
    public float LastOperatingExpenses { get; set; }
    public float LastNetBudgetChange { get; set; }
    
    public WorldState()
    {
        _clock = new SimulationClock();
        _updateManager = new UpdateManager();
        _governmentSystem = new GovernmentSystem();
        _economySystem = new EconomySystem();
        _needsSystem = new NeedsSystem();
        _districtSystem = new DistrictSystem();

        // Register needs update to CitizenNeedsUpdate phase
        _updateManager.Register(UpdatePhase.CitizenNeedsUpdate, () => _needsSystem.UpdateTick(this));

        // Instantiate behavior system and register before economy assign
        _behaviorSystem = new BehaviorSystem();

        // Register behavior, then economy/job and payroll processing to JobAndIncomeUpdate phase
        _updateManager.Register(UpdatePhase.JobAndIncomeUpdate, () => {
            _behaviorSystem.UpdateTick(this);
            _economySystem.AssignJobs(this);
            _economySystem.ProcessProductionAndSales(this);
            _economySystem.ProcessPayroll(this);
            _economySystem.ProcessBusinessTaxes(this);
            _economySystem.UpdateBusinessViability(this);
            _economySystem.ProcessGovernmentExpenses(this);
            RecalculateHouseholds();
        });

        // Register government project tick to EventTriggerCheck phase
        _updateManager.Register(UpdatePhase.EventTriggerCheck, () => _governmentSystem.TickProjects(this));

        // Register district aggregates update to DistrictAggregates phase
        _updateManager.Register(UpdatePhase.DistrictAggregates, () => _districtSystem.UpdateDistrictAggregates(this));

        // EconomyUpdate: compute unemployment rate (no direct state mutation)
        _updateManager.Register(UpdatePhase.EconomyUpdate, () => {
            LastUnemploymentRate = _economySystem.GetUnemploymentRate(this);
        });

        // BusinessUpdate: ensure business employee counts reflect employee ID lists
        _updateManager.Register(UpdatePhase.BusinessUpdate, () => {
            foreach (var b in Businesses)
            {
                b.EmployeeCount = b.EmployeeIds.Count;
            }
        });

        // CrisisProgression: small, bounded support penalty per active crisis event
        _updateManager.Register(UpdatePhase.CrisisProgression, () => {
            var crises = Events.Count(e => e.Type == EventType.Crisis);
            if (crises > 0)
            {
                SupportRating = Math.Clamp(SupportRating - crises * 0.05f, 0f, 100f);
            }
        });

        // PoliticalSupportUpdate: nudge support towards average satisfaction
        _updateManager.Register(UpdatePhase.PoliticalSupportUpdate, () => {
            var avg = GetAverageSatisfaction();
            SupportRating = Math.Clamp(SupportRating + (avg - SupportRating) * 0.01f, 0f, 100f);
        });
        // Instantiate DemographySystem
        _demographySystem = new DemographySystem();
        // Demography: run aging/births/deaths on TimeUpdate (annual within DemographySystem)
        _updateManager.Register(UpdatePhase.TimeUpdate, () => _demographySystem.UpdateTick(this));
    }
    
    /// <summary>
    /// Initialize world state from save data or defaults.
    /// </summary>
    public void Initialize()
    {
        Initialize(WorldScenarioLoader.CreateDefault());
    }

    public void InitializeFromJson(string json)
    {
        Initialize(WorldScenarioLoader.LoadJson(json));
    }

    public void InitializeFromJsonFile(string path)
    {
        Initialize(WorldScenarioLoader.LoadJsonFile(path));
    }

    public void Initialize(WorldScenario scenario)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));

        ClearSimulationState();

        Budget = scenario.Budget;
        SupportRating = Math.Clamp(scenario.SupportRating, 0f, 100f);
        IsInPower = scenario.IsInPower;
        IncomeTaxRate = Math.Clamp(scenario.IncomeTaxRate, 0f, 1f);
        BusinessTaxRate = Math.Clamp(scenario.BusinessTaxRate, 0f, 1f);
        BaseOperatingExpensePerTick = Math.Max(0f, scenario.BaseOperatingExpensePerTick);
        ProjectOperatingExpensePerTick = Math.Max(0f, scenario.ProjectOperatingExpensePerTick);

        foreach (var districtSeed in scenario.Districts)
        {
            Districts.Add(new District(districtSeed.Name)
            {
                Id = districtSeed.Id
            });
        }

        foreach (var businessSeed in scenario.Businesses)
        {
            Businesses.Add(new Business(businessSeed.Name, businessSeed.Type, businessSeed.MaxEmployees)
            {
                Id = businessSeed.Id,
                DistrictId = businessSeed.DistrictId,
                WagePerEmployee = businessSeed.WagePerEmployee,
                ProductionType = businessSeed.ProductionType,
                BaseOutput = businessSeed.BaseOutput,
                UnitPrice = businessSeed.UnitPrice,
                DemandMultiplier = businessSeed.DemandMultiplier,
                Revenue = businessSeed.Revenue,
                Expenses = businessSeed.Expenses,
                Status = ParseBusinessStatus(businessSeed.Status)
            });
        }

        foreach (var housingSeed in scenario.HousingUnits)
        {
            HousingUnits.Add(new HousingUnit(
                housingSeed.Id,
                housingSeed.DistrictId,
                housingSeed.Capacity,
                housingSeed.RentPerTick));
        }

        foreach (var citizenSeed in scenario.Citizens)
        {
            var citizen = new Citizen(
                citizenSeed.Name,
                citizenSeed.Age,
                citizenSeed.Profession,
                ParseGender(citizenSeed.Gender))
            {
                DistrictId = citizenSeed.DistrictId,
                Income = citizenSeed.Income,
                Satisfaction = Math.Clamp(citizenSeed.Satisfaction, 0f, 100f),
                Mood = Math.Clamp(citizenSeed.Mood, 0f, 100f),
                Health = Math.Clamp(citizenSeed.Health, 0f, 100f),
                FoodSatisfaction = Math.Clamp(citizenSeed.FoodSatisfaction, 0f, 100f),
                HousingSatisfaction = Math.Clamp(citizenSeed.HousingSatisfaction, 0f, 100f),
                SafetySatisfaction = Math.Clamp(citizenSeed.SafetySatisfaction, 0f, 100f),
                HealthcareSatisfaction = Math.Clamp(citizenSeed.HealthcareSatisfaction, 0f, 100f),
                EntertainmentSatisfaction = Math.Clamp(citizenSeed.EntertainmentSatisfaction, 0f, 100f)
            };

            citizen.Job = citizenSeed.Job;
            if (citizenSeed.IsRetired)
            {
                citizen.Retire();
                citizen.Profession = "Retired";
            }

            Citizens.Add(citizen);
        }

        foreach (var householdSeed in scenario.Households)
        {
            var members = householdSeed.MemberNames
                .Select(GetCitizenByName)
                .Where(c => c != null)
                .Cast<Citizen>()
                .ToList();

            CreateHousehold(
                householdSeed.DistrictId,
                members,
                householdSeed.HousingUnitId,
                householdSeed.HousingCapacity,
                householdSeed.RentPerTick);
        }

        ReconcileScenarioJobs();
        RecalculateHouseholds();
        _districtSystem.UpdateDistrictAggregates(this);
        LastUnemploymentRate = _economySystem.GetUnemploymentRate(this);
    }
    
    /// <summary>
    /// Execute one full simulation tick.
    /// </summary>
    public void Tick()
    {
        _clock.Tick();
        _updateManager.ExecuteFullCycle();
    }
    
    /// <summary>
    /// Get a citizen by ID.
    /// </summary>
    public Citizen? GetCitizen(int id)
    {
        return Citizens.FirstOrDefault(c => c.Id == id);
    }

    public Citizen? GetCitizenByName(string name)
    {
        return Citizens.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Get a household by ID.
    /// </summary>
    public Household? GetHousehold(int id)
    {
        return Households.FirstOrDefault(h => h.Id == id);
    }

    /// <summary>
    /// Get a housing unit by ID.
    /// </summary>
    public HousingUnit? GetHousingUnit(int id)
    {
        return HousingUnits.FirstOrDefault(h => h.Id == id);
    }

    public HousingUnit AddHousingUnit(int id, int? districtId, int capacity, float rentPerTick = 0f)
    {
        var housingUnit = new HousingUnit(id, districtId, capacity, rentPerTick);
        HousingUnits.Add(housingUnit);
        return housingUnit;
    }

    /// <summary>
    /// Create a household in a district and optionally seed it with citizens.
    /// </summary>
    public Household CreateHousehold(
        int? districtId,
        IEnumerable<Citizen>? members = null,
        int? housingUnitId = null,
        int housingCapacity = 0,
        float rentPerTick = 0f)
    {
        var household = new Household(districtId, housingUnitId, housingCapacity, rentPerTick);
        Households.Add(household);

        if (members != null)
        {
            foreach (var member in members)
            {
                AddCitizenToHousehold(member, household);
            }
        }

        if (housingUnitId.HasValue)
        {
            var housingUnit = GetHousingUnit(housingUnitId.Value);
            if (housingUnit != null)
            {
                AssignHouseholdToHousingUnit(household, housingUnit);
            }
        }

        return household;
    }

    public bool AssignHouseholdToHousingUnit(Household household, HousingUnit housingUnit)
    {
        if (household == null) throw new ArgumentNullException(nameof(household));
        if (housingUnit == null) throw new ArgumentNullException(nameof(housingUnit));
        if (housingUnit.HouseholdId.HasValue && housingUnit.HouseholdId.Value != household.Id)
        {
            return false;
        }

        if (household.HousingUnitId.HasValue && household.HousingUnitId.Value != housingUnit.Id)
        {
            var currentHousingUnit = GetHousingUnit(household.HousingUnitId.Value);
            if (currentHousingUnit != null && currentHousingUnit.HouseholdId == household.Id)
            {
                currentHousingUnit.HouseholdId = null;
            }
        }

        housingUnit.HouseholdId = household.Id;
        household.HousingUnitId = housingUnit.Id;
        household.HousingCapacity = housingUnit.Capacity;
        household.RentPerTick = housingUnit.RentPerTick;

        if (housingUnit.DistrictId.HasValue)
        {
            household.DistrictId = housingUnit.DistrictId;
            foreach (var memberId in household.MemberIds)
            {
                var member = GetCitizen(memberId);
                if (member != null)
                {
                    member.DistrictId = housingUnit.DistrictId;
                }
            }
        }

        return true;
    }

    public void ReleaseHouseholdHousing(Household household)
    {
        if (household == null) throw new ArgumentNullException(nameof(household));

        if (household.HousingUnitId.HasValue)
        {
            var housingUnit = GetHousingUnit(household.HousingUnitId.Value);
            if (housingUnit != null && housingUnit.HouseholdId == household.Id)
            {
                housingUnit.HouseholdId = null;
            }
        }

        household.HousingUnitId = null;
        household.HousingCapacity = 0;
        household.RentPerTick = 0f;
    }

    /// <summary>
    /// Recalculate derived household values from current citizens.
    /// </summary>
    public void RecalculateHouseholds()
    {
        foreach (var household in Households.ToList())
        {
            household.MemberIds.RemoveAll(memberId => GetCitizen(memberId) == null);
            if (household.MemberIds.Count == 0)
            {
                ReleaseHouseholdHousing(household);
                Households.Remove(household);
                continue;
            }

            household.RecalculateIncome(Citizens);
        }
    }

    /// <summary>
    /// Assign a citizen to a household while keeping both sides in sync.
    /// </summary>
    public void AddCitizenToHousehold(Citizen citizen, Household household)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));
        if (household == null) throw new ArgumentNullException(nameof(household));

        if (citizen.HouseholdId.HasValue && citizen.HouseholdId.Value != household.Id)
        {
            RemoveCitizenFromHousehold(citizen);
        }

        citizen.HouseholdId = household.Id;
        if (!household.MemberIds.Contains(citizen.Id))
        {
            household.MemberIds.Add(citizen.Id);
        }

        if (citizen.DistrictId == null)
        {
            citizen.DistrictId = household.DistrictId;
        }
        else if (household.DistrictId == null)
        {
            household.DistrictId = citizen.DistrictId;
        }

        household.RecalculateIncome(Citizens);
    }

    /// <summary>
    /// Remove a citizen from their household while keeping both sides in sync.
    /// </summary>
    public void RemoveCitizenFromHousehold(Citizen citizen)
    {
        if (citizen == null) throw new ArgumentNullException(nameof(citizen));
        if (!citizen.HouseholdId.HasValue) return;

        var household = GetHousehold(citizen.HouseholdId.Value);
        if (household != null)
        {
            household.MemberIds.Remove(citizen.Id);
            if (household.MemberIds.Count == 0)
            {
                ReleaseHouseholdHousing(household);
                Households.Remove(household);
            }
            else
            {
                household.RecalculateIncome(Citizens);
            }
        }

        citizen.HouseholdId = null;
    }
    
    /// <summary>
    /// Get all citizens by profession.
    /// </summary>
    public List<Citizen> GetCitizensByProfession(string profession)
    {
        return Citizens.Where(c => c.Profession == profession).ToList();
    }
    
    /// <summary>
    /// Get all unemployed citizens.
    /// </summary>
    public List<Citizen> GetUnemployedCitizens()
    {
        return Citizens.Where(c => c.EmploymentStatus == EmploymentStatus.Unemployed).ToList();
    }
    
    /// <summary>
    /// Calculate total population.
    /// </summary>
    public int GetTotalPopulation() => Citizens.Count;
    
    /// <summary>
    /// Calculate average satisfaction across all citizens.
    /// </summary>
    public float GetAverageSatisfaction()
    {
        if (Citizens.Count == 0) return 0f;
        return Citizens.Average(c => c.Satisfaction);
    }
    
    /// <summary>
    /// Debug: Get system state summary.
    /// </summary>
    public string GetStateDebugInfo()
    {
        return $"""
            === World State Debug Info ===
            Time: {_clock.GetFullTimeString()}
            Population: {GetTotalPopulation()}
            Budget: ${Budget:F2}
            Support: {SupportRating:F1}%
            In Power: {IsInPower}
            Average Satisfaction: {GetAverageSatisfaction():F1}%
            """;
    }

    private void ClearSimulationState()
    {
        _clock.Reset();
        Citizens.Clear();
        Households.Clear();
        HousingUnits.Clear();
        Districts.Clear();
        Businesses.Clear();
        Projects.Clear();
        Events.Clear();
        LastUnemploymentRate = 0f;
        LastIncomeTaxCollected = 0f;
        LastBusinessTaxCollected = 0f;
        LastOperatingExpenses = 0f;
        LastNetBudgetChange = 0f;
    }

    private void ReconcileScenarioJobs()
    {
        foreach (var business in Businesses)
        {
            business.EmployeeIds.Clear();
            business.EmployeeCount = 0;
        }

        foreach (var citizen in Citizens)
        {
            if (string.IsNullOrEmpty(citizen.Job)) continue;
            if (!EconomySystem.IsEligibleForWork(citizen))
            {
                citizen.Job = null;
                continue;
            }

            var business = Businesses.FirstOrDefault(b => string.Equals(b.Name, citizen.Job, StringComparison.Ordinal));
            if (business == null || business.EmployeeIds.Count >= business.MaxEmployees)
            {
                citizen.Job = null;
                continue;
            }

            if (!business.EmployeeIds.Contains(citizen.Id))
            {
                business.EmployeeIds.Add(citizen.Id);
                business.EmployeeCount = business.EmployeeIds.Count;
            }
        }
    }

    private static Gender ParseGender(string? gender)
    {
        return Enum.TryParse<Gender>(gender, ignoreCase: true, out var parsed)
            ? parsed
            : Gender.Other;
    }

    private static BusinessStatus ParseBusinessStatus(string? status)
    {
        return Enum.TryParse<BusinessStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : BusinessStatus.Active;
    }
}

