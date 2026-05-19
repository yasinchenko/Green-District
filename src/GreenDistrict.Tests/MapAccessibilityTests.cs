using System.Collections.Generic;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;
using GreenDistrict.Simulation.Map;
using GreenDistrict.Simulation.Needs;
using Xunit;

namespace GreenDistrict.Tests;

public class MapAccessibilityTests
{
    [Fact]
    public void Accessibility_Analyzer_Reports_Entity_Road_Access()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Central") { Id = 1, Population = 10 });
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });
        var grid = CreateGridWithDistrict(30, 20, districtId: 1);
        grid.SetRoad(new GridPosition(5, 12), RoadKind.LocalRoad, 6, districtId: 1);
        var shop = new PlacedMapObject(
            "business:100",
            PlacedMapObjectType.Business,
            districtId: 1,
            position: new GridPosition(4, 8),
            widthMeters: 4,
            lengthMeters: 4,
            accessSides: MapAccessSide.South,
            assetKey: "business.shop",
            entityKind: MapObjectEntityKind.Business,
            entityId: 100);
        Assert.True(grid.TryPlaceObject(shop));

        var report = new MapAccessibilityAnalyzer().Analyze(world, CreateResult(grid, districtId: 1));

        Assert.True(report.IsBusinessAccessible(100));
        var entity = Assert.Contains((MapObjectEntityKind.Business, 100), report.Entities);
        Assert.Equal("business:100", entity.ObjectId);
    }

    [Fact]
    public void Economy_Can_Use_Map_Accessibility_To_Block_Inaccessible_Production()
    {
        var world = new WorldState();
        var worker = new Citizen("Worker", 30, "Worker", Gender.Female);
        world.Citizens.Add(worker);
        var business = new Business("Workshop", "workshop", 1)
        {
            Id = 10,
            DistrictId = 1,
            BaseOutput = 100f,
            UnitPrice = 2f,
            DemandMultiplier = 1f
        };
        business.EmployeeIds.Add(worker.Id);
        worker.Job = business.Name;
        world.Businesses.Add(business);
        var report = new MapAccessibilityReport(
            new Dictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility>
            {
                [(MapObjectEntityKind.Business, 10)] = new(
                    MapObjectEntityKind.Business,
                    10,
                    "business:10",
                    1,
                    HasRoadAccess: false,
                    CoveragePercent: 0f)
            },
            new Dictionary<int, MapDistrictAccessibility>());

        new EconomySystem().ProcessProductionAndSales(world, report);

        Assert.Equal(0f, business.LastProducedUnits);
        Assert.Equal(0f, business.LastSoldUnits);
        Assert.Equal(0f, business.LastSalesRevenue);
    }

    [Fact]
    public void Needs_Can_Use_Map_Accessibility_To_Adjust_Decay()
    {
        var world = new WorldState();
        var citizen = new Citizen("Resident", 30, "Worker", Gender.Male)
        {
            DistrictId = 1,
            FoodSatisfaction = 100f,
            HousingSatisfaction = 100f,
            SafetySatisfaction = 100f,
            HealthcareSatisfaction = 100f,
            EntertainmentSatisfaction = 100f
        };
        world.Citizens.Add(citizen);
        var report = new MapAccessibilityReport(
            new Dictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility>(),
            new Dictionary<int, MapDistrictAccessibility>
            {
                [1] = new(1, new Dictionary<MapCoverageKind, MapCoverageSummary>())
            });

        new NeedsSystem(
            foodDecayPerTick: 10f,
            housingDecayPerTick: 0f,
            safetyDecayPerTick: 0f,
            healthcareDecayPerTick: 0f,
            entertainmentDecayPerTick: 0f,
            noHousingPenaltyPerTick: 0f,
            stableHousingRecoveryPerTick: 0f).UpdateTick(world, report);

        Assert.Equal(80f, citizen.FoodSatisfaction);
    }

    private static MapGrid CreateGridWithDistrict(int width, int height, int districtId)
    {
        var grid = new MapGrid(width, height);
        foreach (var cell in grid.Cells)
        {
            cell.DistrictId = districtId;
        }

        return grid;
    }

    private static MapGridGenerationResult CreateResult(MapGrid grid, int districtId)
    {
        var area = new MapDistrictGridArea(districtId, "Central", new GridPosition(0, 0), grid.WidthMeters, grid.HeightMeters);
        var boundary = MapDistrictBoundary.Build(grid, districtId);
        return new MapGridGenerationResult(
            grid,
            new Dictionary<int, MapDistrictGridArea> { [districtId] = area },
            MapFreeSpaceIndex.Build(grid),
            new Dictionary<int, MapDistrictBoundary> { [districtId] = boundary },
            new Dictionary<int, MapDistrictExpansionSpace>
            {
                [districtId] = MapDistrictExpansionSpace.Build(grid, boundary)
            });
    }
}
