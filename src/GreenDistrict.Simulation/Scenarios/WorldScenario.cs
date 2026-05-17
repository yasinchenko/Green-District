using System.Collections.Generic;

namespace GreenDistrict.Simulation.Scenarios;

public class WorldScenario
{
    public float Budget { get; set; } = 10000f;
    public float SupportRating { get; set; } = 75f;
    public bool IsInPower { get; set; } = true;
    public float IncomeTaxRate { get; set; } = 0.15f;
    public float BusinessTaxRate { get; set; } = 0.10f;
    public float BaseOperatingExpensePerTick { get; set; }
    public float ProjectOperatingExpensePerTick { get; set; }
    public int Seed { get; set; }
    public int? InitialPopulation { get; set; }
    public int DemographyTicksPerYear { get; set; } = 1440 * 365;
    public float BirthRatePerPersonPerYear { get; set; } = 0.02f;
    public float BaseDeathRatePerPersonPerYear { get; set; } = 0.01f;
    public float MigrationRatePerPersonPerYear { get; set; } = 0.005f;
    public List<DistrictScenario> Districts { get; set; } = new();
    public List<BusinessScenario> Businesses { get; set; } = new();
    public List<HousingUnitScenario> HousingUnits { get; set; } = new();
    public List<CitizenScenario> Citizens { get; set; } = new();
    public List<HouseholdScenario> Households { get; set; } = new();
    public List<ProjectScenario> Projects { get; set; } = new();
}

public class DistrictScenario
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BusinessScenario
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int MaxEmployees { get; set; }
    public int? DistrictId { get; set; }
    public float WagePerEmployee { get; set; } = 500f;
    public string ProductionType { get; set; } = string.Empty;
    public float BaseOutput { get; set; }
    public float UnitPrice { get; set; } = 1f;
    public float DemandMultiplier { get; set; } = 1f;
    public float Revenue { get; set; }
    public float Expenses { get; set; }
    public string Status { get; set; } = "Active";
}

public class CitizenScenario
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Profession { get; set; } = "Worker";
    public string Gender { get; set; } = "Female";
    public int? DistrictId { get; set; }
    public string? Job { get; set; }
    public float Income { get; set; }
    public float Satisfaction { get; set; } = 50f;
    public float Mood { get; set; } = 50f;
    public float Health { get; set; } = 100f;
    public bool IsRetired { get; set; }
    public float FoodSatisfaction { get; set; } = 80f;
    public float HousingSatisfaction { get; set; } = 80f;
    public float SafetySatisfaction { get; set; } = 80f;
    public float HealthcareSatisfaction { get; set; } = 80f;
    public float EntertainmentSatisfaction { get; set; } = 80f;
}

public class HousingUnitScenario
{
    public int Id { get; set; }
    public int? DistrictId { get; set; }
    public int Capacity { get; set; }
    public float RentPerTick { get; set; }
}

public class HouseholdScenario
{
    public int? DistrictId { get; set; }
    public int? HousingUnitId { get; set; }
    public int HousingCapacity { get; set; }
    public float RentPerTick { get; set; }
    public List<string> MemberNames { get; set; } = new();
}

public class ProjectScenario
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Custom";
    public float? Cost { get; set; }
    public int? DurationTicks { get; set; }
    public int? RemainingTicks { get; set; }
    public float Benefit { get; set; }
    public int? DistrictId { get; set; }
    public float? FoodSatisfactionEffect { get; set; }
    public float? HousingSatisfactionEffect { get; set; }
    public float? SafetySatisfactionEffect { get; set; }
    public float? HealthcareSatisfactionEffect { get; set; }
    public float? EntertainmentSatisfactionEffect { get; set; }
    public float? SupportEffect { get; set; }
    public int? HousingUnitsToCreate { get; set; }
    public int? HousingUnitCapacity { get; set; }
    public float? HousingUnitRentPerTick { get; set; }
    public bool Completed { get; set; }
    public long StartTick { get; set; } = -1;
}
