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
    public List<District> Districts { get; } = new();
    public List<Business> Businesses { get; } = new();
    public List<GovernmentProject> Projects { get; } = new();
    public List<GameEvent> Events { get; } = new();
    
    // Player state
    public float Budget { get; set; } = 10000f;
    public float SupportRating { get; set; } = 75f; // 0-100%
    public bool IsInPower { get; set; } = true;
    
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
            _economySystem.ProcessPayroll(this);
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
        // Setup will happen through external data loaders
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
        return Citizens.Where(c => string.IsNullOrEmpty(c.Job)).ToList();
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
}

/// <summary>
/// Represents a single simulated citizen.
/// </summary>
public class Citizen
{
    private static int _nextId = 1;
    
    public int Id { get; }
    public string Name { get; set; }
    public int Age { get; set; }
    public int? DistrictId { get; set; }
    public string Profession { get; set; }
    public string? Job { get; set; } // Which business they work at (if any)
    public float Income { get; set; }
    public float Satisfaction { get; set; } = 50f; // 0-100%
    public float Mood { get; set; } = 50f; // 0-100%
    public float Health { get; set; } = 100f; // 0-100%
    public bool IsRetired { get; set; } = false;
    
    public Gender Gender { get; set; }
    public int? HouseholdId { get; set; }
    
    // Needs satisfaction (0-100%)
    public float FoodSatisfaction { get; set; }
    public float HousingSatisfaction { get; set; }
    public float SafetySatisfaction { get; set; }
    public float HealthcareSatisfaction { get; set; }
    public float EntertainmentSatisfaction { get; set; }
    
    public Citizen(string name, int age, string profession, Gender gender = Gender.Female)
    {
        Id = _nextId++;
        Name = name;
        Age = age;
        Profession = profession;
        Gender = gender;
    }
    
    /// <summary>
    /// Calculate overall satisfaction from individual needs.
    /// </summary>
    public void RecalculateSatisfaction()
    {
        Satisfaction = (FoodSatisfaction + HousingSatisfaction + SafetySatisfaction + 
                       HealthcareSatisfaction + EntertainmentSatisfaction) / 5f;
        Satisfaction = Math.Clamp(Satisfaction, 0f, 100f);
    }
    
    /// <summary>
    /// Update mood based on satisfaction and other factors.
    /// </summary>
    public void UpdateMood()
    {
        // Mood shifts towards current satisfaction (do not overwrite Satisfaction here)
        Mood += (Satisfaction - Mood) * 0.1f; // Gradual change
        Mood = Math.Clamp(Mood, 0f, 100f);
        
        // Health decay if not eating well
        if (FoodSatisfaction < 30f)
        {
            Health -= 1f;
        }
        else if (FoodSatisfaction > 70f)
        {
            Health += 0.5f;
        }
        Health = Math.Clamp(Health, 0f, 100f);
    }
}

/// <summary>
/// Represents a district or region.
/// </summary>
public class District
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Population { get; set; }
    public float AverageSatisfaction { get; set; }
    public float EconomicLevel { get; set; } // 0-100%
    
    public District(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Represents a business or workplace.
/// </summary>
public class Business
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? DistrictId { get; set; }
    public string Type { get; set; } // farm, factory, shop, etc.
    public List<int> EmployeeIds { get; } = new();
    public float WagePerEmployee { get; set; } = 500f;
    public int EmployeeCount { get; set; }
    public int MaxEmployees { get; set; }
    public float Revenue { get; set; }
    public float Expenses { get; set; }
    
    public Business(string name, string type, int maxEmployees)
    {
        Name = name;
        Type = type;
        MaxEmployees = maxEmployees;
    }
    
    public float GetProfit() => Revenue - Expenses;
}

/// <summary>
/// Represents a game event or notification.
/// </summary>
public class GameEvent
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public long CreatedAtTick { get; set; }
    public EventType Type { get; set; }
    
    public GameEvent(string title, string description, EventType type)
    {
        Title = title;
        Description = description;
        Type = type;
    }
}

public enum EventType
{
    Notification,
    Crisis,
    Decision,
    Election,
    Economic,
    Social,
    Political
}

public enum Gender
{
    Male,
    Female,
    Other
}
