using System;
using System.IO;
using System.Text.Json;

namespace GreenDistrict.Simulation.Scenarios;

public static class WorldScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static WorldScenario LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Scenario JSON cannot be empty.", nameof(json));

        var scenario = JsonSerializer.Deserialize<WorldScenario>(json, Options);
        if (scenario == null) throw new InvalidOperationException("Scenario JSON is invalid.");

        return scenario;
    }

    public static WorldScenario LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Scenario path cannot be empty.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public static WorldScenario CreateDefault()
    {
        return new WorldScenario
        {
            Budget = 10000f,
            SupportRating = 75f,
            IsInPower = true,
            IncomeTaxRate = 0.15f,
            BusinessTaxRate = 0.10f,
            BaseOperatingExpensePerTick = 25f,
            ProjectOperatingExpensePerTick = 5f,
            Seed = 0,
            EconomicTickInterval = 1440,
            InitialPopulation = 50,
            Districts =
            {
                new DistrictScenario
                {
                    Id = 1,
                    Name = "Central",
                    HousingSatisfaction = 72f,
                    SafetySatisfaction = 68f,
                    HealthcareSatisfaction = 70f,
                    EntertainmentSatisfaction = 45f
                },
                new DistrictScenario
                {
                    Id = 2,
                    Name = "North Residential",
                    HousingSatisfaction = 42f,
                    SafetySatisfaction = 64f,
                    HealthcareSatisfaction = 48f,
                    EntertainmentSatisfaction = 58f
                },
                new DistrictScenario
                {
                    Id = 3,
                    Name = "South Works",
                    HousingSatisfaction = 64f,
                    SafetySatisfaction = 38f,
                    HealthcareSatisfaction = 62f,
                    EntertainmentSatisfaction = 52f
                }
            },
            Businesses =
            {
                new BusinessScenario
                {
                    Id = 1,
                    Name = "Central Farm",
                    Type = "farm",
                    MaxEmployees = 18,
                    DistrictId = 1,
                    WagePerEmployee = 35f,
                    ProductionType = "food",
                    BaseOutput = 2500f,
                    UnitPrice = 2f,
                    DemandMultiplier = 0.85f,
                    Cash = 50000f,
                    Revenue = 5000f
                },
                new BusinessScenario
                {
                    Id = 2,
                    Name = "Central Shop",
                    Type = "shop",
                    MaxEmployees = 14,
                    DistrictId = 1,
                    WagePerEmployee = 38f,
                    ProductionType = "trade",
                    BaseOutput = 300f,
                    UnitPrice = 5f,
                    DemandMultiplier = 0.9f,
                    Cash = 50000f,
                    Revenue = 3500f
                },
                new BusinessScenario
                {
                    Id = 3,
                    Name = "South Workshop",
                    Type = "workshop",
                    MaxEmployees = 14,
                    DistrictId = 3,
                    WagePerEmployee = 36f,
                    ProductionType = "goods",
                    BaseOutput = 650f,
                    UnitPrice = 4f,
                    DemandMultiplier = 0.8f,
                    Cash = 50000f,
                    Revenue = 4200f
                },
                new BusinessScenario
                {
                    Id = 4,
                    Name = "Central Clinic",
                    Type = "clinic",
                    MaxEmployees = 8,
                    DistrictId = 1,
                    WagePerEmployee = 42f,
                    ProductionType = "services",
                    BaseOutput = 220f,
                    UnitPrice = 7f,
                    DemandMultiplier = 0.75f,
                    Cash = 50000f,
                    Revenue = 3600f
                },
                new BusinessScenario
                {
                    Id = 5,
                    Name = "North Market",
                    Type = "shop",
                    MaxEmployees = 10,
                    DistrictId = 2,
                    WagePerEmployee = 34f,
                    ProductionType = "trade",
                    BaseOutput = 220f,
                    UnitPrice = 5f,
                    DemandMultiplier = 0.7f,
                    Cash = 50000f,
                    Revenue = 2600f
                },
                new BusinessScenario
                {
                    Id = 6,
                    Name = "South Depot",
                    Type = "factory",
                    MaxEmployees = 12,
                    DistrictId = 3,
                    WagePerEmployee = 37f,
                    ProductionType = "goods",
                    BaseOutput = 520f,
                    UnitPrice = 4f,
                    DemandMultiplier = 0.65f,
                    Cash = 50000f,
                    Revenue = 3000f
                }
            },
            HousingUnits =
            {
                new HousingUnitScenario { Id = 1, DistrictId = 1, Capacity = 4, RentPerTick = 20f },
                new HousingUnitScenario { Id = 2, DistrictId = 1, Capacity = 2, RentPerTick = 25f },
                new HousingUnitScenario { Id = 3, DistrictId = 1, Capacity = 3, RentPerTick = 30f },
                new HousingUnitScenario { Id = 4, DistrictId = 2, Capacity = 4, RentPerTick = 18f },
                new HousingUnitScenario { Id = 5, DistrictId = 2, Capacity = 3, RentPerTick = 20f },
                new HousingUnitScenario { Id = 6, DistrictId = 3, Capacity = 3, RentPerTick = 16f }
            },
            Citizens =
            {
                new CitizenScenario { Name = "Maria Green", Age = 29, Profession = "Worker", Gender = "Female", DistrictId = 1, Job = "Central Farm" },
                new CitizenScenario { Name = "Ivan Green", Age = 31, Profession = "Worker", Gender = "Male", DistrictId = 1, Job = "Central Farm" },
                new CitizenScenario { Name = "Sofia Green", Age = 7, Profession = "Child", Gender = "Female", DistrictId = 1 },
                new CitizenScenario { Name = "Anna Green", Age = 35, Profession = "Trader", Gender = "Female", DistrictId = 2, Job = "North Market" },
                new CitizenScenario { Name = "Dmitry Forge", Age = 42, Profession = "Worker", Gender = "Male", DistrictId = 3, Job = "South Workshop" },
                new CitizenScenario { Name = "Elena Forge", Age = 39, Profession = "Worker", Gender = "Female", DistrictId = 3, Job = "South Depot" }
            },
            Households =
            {
                new HouseholdScenario
                {
                    DistrictId = 1,
                    HousingUnitId = 1,
                    HousingCapacity = 4,
                    RentPerTick = 20f,
                    MemberNames = { "Maria Green", "Ivan Green", "Sofia Green" }
                },
                new HouseholdScenario
                {
                    DistrictId = 2,
                    HousingUnitId = 4,
                    HousingCapacity = 4,
                    RentPerTick = 18f,
                    MemberNames = { "Anna Green" }
                },
                new HouseholdScenario
                {
                    DistrictId = 3,
                    HousingUnitId = 6,
                    HousingCapacity = 3,
                    RentPerTick = 16f,
                    MemberNames = { "Dmitry Forge", "Elena Forge" }
                }
            },
            Projects =
            {
                new ProjectScenario
                {
                    Id = 1,
                    Name = "Central Park",
                    Type = "Park",
                    DistrictId = 1,
                    RemainingTicks = 25,
                    StartTick = -1
                },
                new ProjectScenario
                {
                    Id = 2,
                    Name = "North Housing Block",
                    Type = "Housing",
                    DistrictId = 2,
                    RemainingTicks = 35,
                    StartTick = -1
                },
                new ProjectScenario
                {
                    Id = 3,
                    Name = "South Safety Office",
                    Type = "Police",
                    DistrictId = 3,
                    RemainingTicks = 30,
                    StartTick = -1
                }
            }
        };
    }
}
