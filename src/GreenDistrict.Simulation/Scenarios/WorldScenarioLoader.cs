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
            Districts =
            {
                new DistrictScenario { Id = 1, Name = "Central" },
                new DistrictScenario { Id = 2, Name = "Riverside" }
            },
            Businesses =
            {
                new BusinessScenario
                {
                    Id = 1,
                    Name = "Central Farm",
                    Type = "farm",
                    MaxEmployees = 4,
                    DistrictId = 1,
                    WagePerEmployee = 400f,
                    ProductionType = "food",
                    BaseOutput = 500f,
                    UnitPrice = 2f,
                    DemandMultiplier = 0.85f,
                    Revenue = 5000f
                },
                new BusinessScenario
                {
                    Id = 2,
                    Name = "Riverside Shop",
                    Type = "shop",
                    MaxEmployees = 3,
                    DistrictId = 2,
                    WagePerEmployee = 500f,
                    ProductionType = "trade",
                    BaseOutput = 300f,
                    UnitPrice = 5f,
                    DemandMultiplier = 0.9f,
                    Revenue = 3500f
                }
            },
            HousingUnits =
            {
                new HousingUnitScenario { Id = 1, DistrictId = 1, Capacity = 4, RentPerTick = 20f },
                new HousingUnitScenario { Id = 2, DistrictId = 2, Capacity = 2, RentPerTick = 25f },
                new HousingUnitScenario { Id = 3, DistrictId = 2, Capacity = 3, RentPerTick = 30f }
            },
            Citizens =
            {
                new CitizenScenario { Name = "Maria Green", Age = 29, Profession = "Worker", Gender = "Female", DistrictId = 1, Job = "Central Farm" },
                new CitizenScenario { Name = "Ivan Green", Age = 31, Profession = "Worker", Gender = "Male", DistrictId = 1, Job = "Central Farm" },
                new CitizenScenario { Name = "Sofia Green", Age = 7, Profession = "Child", Gender = "Female", DistrictId = 1 },
                new CitizenScenario { Name = "Anna River", Age = 35, Profession = "Trader", Gender = "Female", DistrictId = 2, Job = "Riverside Shop" }
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
                    HousingUnitId = 2,
                    HousingCapacity = 2,
                    RentPerTick = 25f,
                    MemberNames = { "Anna River" }
                }
            }
        };
    }
}
