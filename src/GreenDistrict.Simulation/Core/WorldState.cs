namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Central repository for all game world state.
/// All systems read and write to this state.
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;

public class WorldState
{
    private SimulationClock _clock;
    private UpdateManager _updateManager;
    
    // Core collections
    public List<Citizen> Citizens { get; } = new();
    public List<District> Districts { get; } = new();
    public List<Business> Businesses { get; } = new();
    public List<GameEvent> Events { get; } = new();
    
    // Player state
    public float Budget { get; set; } = 10000f;
    public float SupportRating { get; set; } = 75f; // 0-100%
    public bool IsInPower { get; set; } = true;
    
    public SimulationClock Clock => _clock;
    public UpdateManager UpdateManager => _updateManager;
    
    public WorldState()
    {
        _clock = new SimulationClock();
        _updateManager = new UpdateManager();
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
    
    // Needs satisfaction (0-100%)
    public float FoodSatisfaction { get; set; }
    public float HousingSatisfaction { get; set; }
    public float SafetySatisfaction { get; set; }
    public float HealthcareSatisfaction { get; set; }
    public float EntertainmentSatisfaction { get; set; }
    
    public Citizen(string name, int age, string profession)
    {
        Id = _nextId++;
        Name = name;
        Age = age;
        Profession = profession;
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
