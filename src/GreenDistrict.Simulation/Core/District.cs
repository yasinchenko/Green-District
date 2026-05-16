namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a district or region.
/// </summary>
public class District
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Population { get; set; }
    public float AverageSatisfaction { get; set; }
    public float AverageHousingSatisfaction { get; set; }
    public float AverageSafetySatisfaction { get; set; }
    public float AverageHealthcareSatisfaction { get; set; }
    public float AverageEntertainmentSatisfaction { get; set; }
    public float ServiceLevel { get; set; }
    public float EconomicLevel { get; set; } // 0-100%
    public float SupportRating { get; set; } = 75f;
    public float CrisisRisk { get; set; }
    public bool HasActiveCrisis { get; set; }
    public int TotalJobs { get; set; }
    public int OpenJobs { get; set; }
    public float EmploymentRate { get; set; }
    public int HousingCapacity { get; set; }
    public int OccupiedHousing { get; set; }
    public int AvailableHousing { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }

    public District(string name)
    {
        Name = name;
    }
}
