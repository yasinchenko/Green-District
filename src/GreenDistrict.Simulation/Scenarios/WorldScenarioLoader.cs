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
            InitialPopulation = 100,
            Districts =
            {
                new DistrictScenario
                {
                    Id = 1,
                    Name = "Green District",
                    HousingSatisfaction = 72f,
                    SafetySatisfaction = 68f,
                    HealthcareSatisfaction = 70f,
                    EntertainmentSatisfaction = 45f
                }
            },
            Businesses =
            {
                new BusinessScenario
                {
                    Id = 1,
                    Name = "City Farm",
                    Type = "farm",
                    MaxEmployees = 24,
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
                    Name = "City Market",
                    Type = "shop",
                    MaxEmployees = 18,
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
                    Name = "City Workshop",
                    Type = "workshop",
                    MaxEmployees = 20,
                    DistrictId = 1,
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
                    Name = "City Clinic",
                    Type = "clinic",
                    MaxEmployees = 12,
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
                    Name = "Neighborhood Store",
                    Type = "shop",
                    MaxEmployees = 16,
                    DistrictId = 1,
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
                    Name = "City Depot",
                    Type = "factory",
                    MaxEmployees = 18,
                    DistrictId = 1,
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
                new HousingUnitScenario { Id = 4, DistrictId = 1, Capacity = 4, RentPerTick = 18f },
                new HousingUnitScenario { Id = 5, DistrictId = 1, Capacity = 3, RentPerTick = 20f },
                new HousingUnitScenario { Id = 6, DistrictId = 1, Capacity = 3, RentPerTick = 16f }
            },
            Citizens =
            {
                new CitizenScenario { Name = "Maria Green", Age = 29, Profession = "Worker", Gender = "Female", DistrictId = 1, Job = "City Farm" },
                new CitizenScenario { Name = "Ivan Green", Age = 31, Profession = "Worker", Gender = "Male", DistrictId = 1, Job = "City Farm" },
                new CitizenScenario { Name = "Sofia Green", Age = 7, Profession = "Child", Gender = "Female", DistrictId = 1 },
                new CitizenScenario { Name = "Anna Green", Age = 35, Profession = "Trader", Gender = "Female", DistrictId = 1, Job = "Neighborhood Store" },
                new CitizenScenario { Name = "Dmitry Forge", Age = 42, Profession = "Worker", Gender = "Male", DistrictId = 1, Job = "City Workshop" },
                new CitizenScenario { Name = "Elena Forge", Age = 39, Profession = "Worker", Gender = "Female", DistrictId = 1, Job = "City Depot" }
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
                    DistrictId = 1,
                    HousingUnitId = 4,
                    HousingCapacity = 4,
                    RentPerTick = 18f,
                    MemberNames = { "Anna Green" }
                },
                new HouseholdScenario
                {
                    DistrictId = 1,
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
                    Name = "City Park",
                    Type = "Park",
                    DistrictId = 1,
                    RemainingTicks = 25 * 1440,
                    StartTick = -1
                },
                new ProjectScenario
                {
                    Id = 2,
                    Name = "City Housing Block",
                    Type = "Housing",
                    DistrictId = 1,
                    RemainingTicks = 35 * 1440,
                    StartTick = -1
                },
                new ProjectScenario
                {
                    Id = 3,
                    Name = "City Safety Office",
                    Type = "Police",
                    DistrictId = 1,
                    RemainingTicks = 30 * 1440,
                    StartTick = -1
                }
            }
        };
    }
}
