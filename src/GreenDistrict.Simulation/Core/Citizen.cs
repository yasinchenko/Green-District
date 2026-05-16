using System;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a single simulated citizen.
/// </summary>
public class Citizen
{
    private static int _nextId = 1;
    private int _age;
    private string? _job;

    public int Id { get; }
    public string Name { get; set; }
    public int Age
    {
        get => _age;
        set
        {
            _age = Math.Max(0, value);
            RefreshLifeStage();
            RefreshEmploymentStatus();
        }
    }
    public int? DistrictId { get; set; }
    public string Profession { get; set; }
    public string FamilyName { get; set; }
    public string? Job
    {
        get => _job;
        set
        {
            _job = value;
            RefreshEmploymentStatus();
        }
    } // Which business they work at (if any)
    public float Income { get; set; }
    public float Satisfaction { get; set; } = 50f; // 0-100%
    public float Mood { get; set; } = 50f; // 0-100%
    public float Health { get; set; } = 100f; // 0-100%
    public bool IsRetired
    {
        get => EmploymentStatus == EmploymentStatus.Retired;
        set
        {
            if (value)
            {
                _job = null;
                LifeStage = LifeStage.Retired;
                EmploymentStatus = EmploymentStatus.Retired;
            }
            else if (EmploymentStatus == EmploymentStatus.Retired)
            {
                RefreshLifeStage();
                RefreshEmploymentStatus();
            }
        }
    }

    public Gender Gender { get; set; }
    public int? HouseholdId { get; set; }
    public int? MotherId { get; set; }
    public int? FatherId { get; set; }
    public LifeStage LifeStage { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; }

    // Needs satisfaction (0-100%)
    public float FoodSatisfaction { get; set; }
    public float HousingSatisfaction { get; set; }
    public float SafetySatisfaction { get; set; }
    public float HealthcareSatisfaction { get; set; }
    public float EntertainmentSatisfaction { get; set; }

    public Citizen(string name, int age, string profession, Gender gender = Gender.Female, int? id = null)
    {
        Id = id ?? _nextId++;
        if (id.HasValue && id.Value >= _nextId)
        {
            _nextId = id.Value + 1;
        }

        Name = name;
        _age = Math.Max(0, age);
        Profession = profession;
        FamilyName = ExtractFamilyName(name);
        Gender = gender;
        RefreshLifeStage();
        RefreshEmploymentStatus();
    }

    public void Retire()
    {
        IsRetired = true;
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
        Mood += (Satisfaction - Mood) * 0.1f;
        Mood = Math.Clamp(Mood, 0f, 100f);

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

    private void RefreshLifeStage()
    {
        if (EmploymentStatus == EmploymentStatus.Retired || string.Equals(Profession, "Retired", StringComparison.OrdinalIgnoreCase))
        {
            LifeStage = LifeStage.Retired;
            return;
        }

        LifeStage = _age switch
        {
            < 14 => LifeStage.Child,
            < 18 => LifeStage.Student,
            _ => LifeStage.Adult
        };
    }

    private void RefreshEmploymentStatus()
    {
        if (LifeStage == LifeStage.Retired || string.Equals(Profession, "Retired", StringComparison.OrdinalIgnoreCase))
        {
            EmploymentStatus = EmploymentStatus.Retired;
            return;
        }

        if (LifeStage == LifeStage.Child || LifeStage == LifeStage.Student)
        {
            EmploymentStatus = EmploymentStatus.Student;
            return;
        }

        EmploymentStatus = string.IsNullOrEmpty(_job)
            ? EmploymentStatus.Unemployed
            : EmploymentStatus.Employed;
    }

    private static string ExtractFamilyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : string.Empty;
    }
}

public enum Gender
{
    Male,
    Female,
    Other
}

public enum LifeStage
{
    Child,
    Student,
    Adult,
    Retired
}

public enum EmploymentStatus
{
    Unemployed,
    Employed,
    Retired,
    Student
}
