using System;
using System.Linq;
using System.Collections.Generic;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Map;
using Xunit;

namespace GreenDistrict.Tests;

public class MapGridTests
{
    [Fact]
    public void MapGrid_Uses_One_Meter_Cells_By_Default()
    {
        var grid = new MapGrid(20, 10);

        Assert.Equal(20, grid.WidthMeters);
        Assert.Equal(10, grid.HeightMeters);
        Assert.Equal(1f, grid.CellSizeMeters);
        Assert.Equal(new GridPosition(3, 4), grid.WorldToGrid(new MapPoint(3.9f, 4.1f)));
        Assert.Equal(new MapPoint(3.5f, 4.5f), grid.GridToWorldCenter(new GridPosition(3, 4)));
    }

    [Fact]
    public void PlacedObject_Reserves_Full_Footprint()
    {
        var grid = new MapGrid(30, 30);
        var shop = new PlacedMapObject(
            "shop-1",
            PlacedMapObjectType.Business,
            districtId: 1,
            position: new GridPosition(5, 6),
            widthMeters: 4,
            lengthMeters: 3,
            assetKey: "business.shop.small");

        Assert.True(grid.TryPlaceObject(shop));
        Assert.Equal("business.shop.small", shop.AssetKey);
        Assert.Equal(12, shop.FootprintCells().Count());
        Assert.Equal("shop-1", grid.GetCell(new GridPosition(8, 8)).ObjectId);
        Assert.False(grid.GetCell(new GridPosition(9, 8)).HasObject);
    }

    [Fact]
    public void PlacedObject_Cannot_Overlap_Water_Roads_Or_Objects()
    {
        var grid = new MapGrid(20, 20);
        grid.SetSurface(new GridPosition(4, 4), MapSurfaceType.Water);
        grid.SetInfrastructure(new GridPosition(6, 6), MapInfrastructureType.Road);

        var onWater = new PlacedMapObject("water-house", PlacedMapObjectType.Housing, 1, new GridPosition(4, 4), 2, 2);
        var onRoad = new PlacedMapObject("road-house", PlacedMapObjectType.Housing, 1, new GridPosition(6, 6), 2, 2);
        var first = new PlacedMapObject("first", PlacedMapObjectType.Housing, 1, new GridPosition(10, 10), 3, 3);
        var overlapping = new PlacedMapObject("overlap", PlacedMapObjectType.Housing, 1, new GridPosition(12, 12), 3, 3);

        Assert.False(grid.TryPlaceObject(onWater));
        Assert.False(grid.TryPlaceObject(onRoad));
        Assert.True(grid.TryPlaceObject(first));
        Assert.False(grid.TryPlaceObject(overlapping));
    }

    [Fact]
    public void PlacedObject_Cannot_Cross_Its_District_Boundary()
    {
        var grid = CreateGridWithDistrict(12, 12, districtId: 1, minX: 2, minY: 2, maxX: 7, maxY: 7);

        var inside = new PlacedMapObject("inside", PlacedMapObjectType.Housing, 1, new GridPosition(3, 3), 2, 2);
        var crossing = new PlacedMapObject("crossing", PlacedMapObjectType.Housing, 1, new GridPosition(6, 6), 3, 3);
        var wrongDistrict = new PlacedMapObject("wrong-district", PlacedMapObjectType.Housing, 2, new GridPosition(3, 6), 2, 2);

        Assert.True(grid.TryPlaceObject(inside));
        Assert.False(grid.TryPlaceObject(crossing));
        Assert.False(grid.TryPlaceObject(wrongDistrict));
    }

    [Fact]
    public void PlacedObject_Requires_Minimum_Clearance_From_Other_Objects()
    {
        var grid = CreateGridWithDistrict(20, 20, districtId: 1, minX: 0, minY: 0, maxX: 19, maxY: 19);
        var first = new PlacedMapObject("first", PlacedMapObjectType.Housing, 1, new GridPosition(5, 5), 2, 2);
        var adjacent = new PlacedMapObject("adjacent", PlacedMapObjectType.Housing, 1, new GridPosition(7, 5), 2, 2);
        var separated = new PlacedMapObject("separated", PlacedMapObjectType.Housing, 1, new GridPosition(8, 5), 2, 2);

        Assert.True(grid.TryPlaceObject(first));
        Assert.False(grid.TryPlaceObject(adjacent));
        Assert.True(grid.TryPlaceObject(separated));
    }

    [Fact]
    public void Building_Has_Access_When_Road_Touches_Allowed_Side()
    {
        var grid = new MapGrid(20, 20);
        var clinic = new PlacedMapObject(
            "clinic-1",
            PlacedMapObjectType.Service,
            districtId: 1,
            position: new GridPosition(5, 5),
            widthMeters: 4,
            lengthMeters: 3,
            accessSides: MapAccessSide.South);

        Assert.True(grid.TryPlaceObject(clinic));
        Assert.False(grid.HasRoadAccess(clinic));

        grid.SetInfrastructure(new GridPosition(6, 8), MapInfrastructureType.Road);

        Assert.True(grid.HasRoadAccess(clinic));
    }

    [Fact]
    public void Grid_Generator_Connects_Starter_Objects_Without_Full_District_Road_Cross()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Central") { Id = 1, Population = 50, SupportRating = 75f });
        world.Businesses.Add(new Business("Central Farm", "farm", 8) { Id = 1, DistrictId = 1 });
        world.Businesses.Add(new Business("Central Shop", "shop", 4) { Id = 2, DistrictId = 1 });
        world.Businesses.Add(new Business("Central Workshop", "workshop", 6) { Id = 3, DistrictId = 1 });
        for (var i = 1; i <= 14; i++)
        {
            world.HousingUnits.Add(new HousingUnit(i, 1, capacity: i % 3 == 0 ? 3 : 2));
        }

        var map = new MapGridGenerator().Generate(world);
        var area = map.DistrictAreas[1];
        var districtRoads = map.Grid.Cells
            .Where(cell => cell.DistrictId == 1 && cell.HasRoad)
            .ToList();

        Assert.NotEmpty(districtRoads);
        Assert.All(map.Grid.Objects.Values.Where(mapObject => mapObject.Type != PlacedMapObjectType.Marker), mapObject =>
            Assert.True(map.Grid.HasRoadAccess(mapObject), $"Object {mapObject.Id} should have road access."));
        Assert.False(districtRoads.Any(cell => cell.Position.Y <= area.MinY + 3) &&
            districtRoads.Any(cell => cell.Position.Y >= area.MaxY - 3));
        Assert.False(districtRoads.Any(cell => cell.Position.X <= area.MinX + 3) &&
            districtRoads.Any(cell => cell.Position.X >= area.MaxX - 3));
    }

    [Fact]
    public void Bridge_Can_Only_Be_Placed_On_Water()
    {
        var grid = new MapGrid(10, 10);
        grid.SetSurface(new GridPosition(3, 3), MapSurfaceType.Water);

        grid.SetInfrastructure(new GridPosition(3, 3), MapInfrastructureType.Bridge);

        Assert.Equal(MapInfrastructureType.Bridge, grid.GetCell(new GridPosition(3, 3)).Infrastructure);
        Assert.Throws<InvalidOperationException>(() =>
            grid.SetInfrastructure(new GridPosition(4, 4), MapInfrastructureType.Bridge));
    }

    [Fact]
    public void Road_Cannot_Be_Placed_Directly_On_Water()
    {
        var grid = new MapGrid(10, 10);
        grid.SetSurface(new GridPosition(3, 3), MapSurfaceType.Water);

        Assert.Throws<InvalidOperationException>(() =>
            grid.SetInfrastructure(new GridPosition(3, 3), MapInfrastructureType.Road));
    }

    [Fact]
    public void Default_Size_Catalog_Creates_Objects_With_Planned_Dimensions()
    {
        var catalog = MapObjectSizeCatalog.Defaults;

        var smallHouse = catalog.CreateObject("house-1", "house.small", 1, new GridPosition(2, 3));
        var shop = catalog.CreateObject("shop-1", "shop", 1, new GridPosition(12, 3));
        var clinic = catalog.CreateObject("clinic-1", "clinic", 1, new GridPosition(28, 3));

        Assert.Equal((4, 5), (smallHouse.WidthMeters, smallHouse.LengthMeters));
        Assert.Equal((6, 8), (shop.WidthMeters, shop.LengthMeters));
        Assert.Equal((10, 14), (clinic.WidthMeters, clinic.LengthMeters));
        Assert.Equal("service.clinic", clinic.AssetKey);
    }

    [Fact]
    public void Size_Catalog_Loads_Definitions_From_Json()
    {
        var catalog = MapObjectSizeCatalog.LoadJson("""
            {
              "objects": [
                {
                  "key": "kiosk",
                  "type": "Business",
                  "widthMeters": 5,
                  "lengthMeters": 7,
                  "accessSides": "South",
                  "assetKey": "business.kiosk"
                }
              ]
            }
            """);

        var kiosk = catalog.CreateObject("business:1", "kiosk", 1, new GridPosition(2, 3));

        Assert.Equal(PlacedMapObjectType.Business, kiosk.Type);
        Assert.Equal((5, 7), (kiosk.WidthMeters, kiosk.LengthMeters));
        Assert.Equal(MapAccessSide.South, kiosk.AccessSides);
        Assert.Equal("business.kiosk", kiosk.AssetKey);
    }

    [Fact]
    public void Surface_Asset_Keys_Include_Shoreline_For_Land_Next_To_Water()
    {
        var grid = new MapGrid(5, 5);
        grid.SetSurface(new GridPosition(3, 2), MapSurfaceType.Water);
        grid.SetSurface(new GridPosition(0, 4), MapSurfaceType.Park);

        Assert.Equal("terrain.water", grid.GetSurfaceAssetKey(new GridPosition(3, 2)));
        Assert.Equal("terrain.shoreline", grid.GetSurfaceAssetKey(new GridPosition(2, 2)));
        Assert.Equal("terrain.grass", grid.GetSurfaceAssetKey(new GridPosition(0, 0)));
        Assert.Equal("terrain.park", grid.GetSurfaceAssetKey(new GridPosition(0, 4)));
    }

    [Fact]
    public void Rotated_Object_Swaps_Footprint_Dimensions()
    {
        var workshop = MapObjectSizeCatalog.Defaults.CreateObject(
            "workshop-1",
            "workshop",
            1,
            new GridPosition(5, 5),
            rotationDegrees: 90);

        Assert.Equal(9, workshop.WidthMeters);
        Assert.Equal(12, workshop.LengthMeters);
        Assert.Equal(12, workshop.FootprintWidth);
        Assert.Equal(9, workshop.FootprintLength);
    }

    [Fact]
    public void Road_Cells_Calculate_Connections_And_Intersection_Tile()
    {
        var grid = new MapGrid(10, 10);
        grid.SetRoad(new GridPosition(5, 5), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(5, 4), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(6, 5), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(5, 6), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(4, 5), RoadKind.LocalRoad, 6, districtId: 1);

        var center = grid.GetCell(new GridPosition(5, 5));

        Assert.Equal(MapInfrastructureType.Intersection, center.Infrastructure);
        Assert.Equal(RoadDirection.North | RoadDirection.East | RoadDirection.South | RoadDirection.West, center.RoadConnections);
        Assert.Equal(RoadTileKind.Cross, center.RoadTileKind);
        Assert.Equal("road.cross", center.RoadAssetKey);
        Assert.Equal(1, center.RoadDistrictId);
    }

    [Fact]
    public void Bridge_Cells_Keep_Bridge_Kind_And_Use_Bridge_Asset_Key()
    {
        var grid = new MapGrid(10, 10);
        grid.SetSurface(new GridPosition(4, 5), MapSurfaceType.Water);
        grid.SetSurface(new GridPosition(5, 5), MapSurfaceType.Water);

        grid.SetRoad(new GridPosition(3, 5), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(6, 5), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(4, 5), RoadKind.Bridge, 6, districtId: 1);
        grid.SetRoad(new GridPosition(5, 5), RoadKind.Bridge, 6, districtId: 1);

        var bridge = grid.GetCell(new GridPosition(4, 5));

        Assert.Equal(MapInfrastructureType.Bridge, bridge.Infrastructure);
        Assert.Equal(RoadKind.Bridge, bridge.RoadKind);
        Assert.Equal(RoadDirection.East | RoadDirection.West, bridge.RoadConnections);
        Assert.Equal(RoadTileKind.Straight, bridge.RoadTileKind);
        Assert.Equal("bridge.straight", bridge.RoadAssetKey);
    }

    [Fact]
    public void Pathfinding_Finds_Cell_Path_Around_Blocked_Cells()
    {
        var grid = new MapGrid(8, 8);
        for (var y = 0; y < 7; y++)
        {
            if (y == 3) continue;
            grid.SetSurface(new GridPosition(3, y), MapSurfaceType.Blocked);
        }

        var result = new RoadPathfinder().FindPath(grid, new GridPosition(1, 1), new GridPosition(6, 1));

        Assert.True(result.Found);
        Assert.Contains(new GridPosition(3, 3), result.Cells);
        Assert.DoesNotContain(result.Cells, position => grid.GetCell(position).IsBlocked);
    }

    [Fact]
    public void Pathfinding_Rejects_Water_When_Bridges_Are_Disabled()
    {
        var grid = new MapGrid(6, 3);
        grid.SetSurface(new GridPosition(2, 1), MapSurfaceType.Water);
        grid.SetSurface(new GridPosition(3, 1), MapSurfaceType.Water);

        for (var x = 0; x < 6; x++)
        {
            grid.SetSurface(new GridPosition(x, 0), MapSurfaceType.Blocked);
            grid.SetSurface(new GridPosition(x, 2), MapSurfaceType.Blocked);
        }

        var result = new RoadPathfinder().FindPath(
            grid,
            new GridPosition(0, 1),
            new GridPosition(5, 1),
            new RoadPathOptions(AllowBridges: false));

        Assert.False(result.Found);
    }

    [Fact]
    public void Pathfinding_Can_Use_Water_As_Bridge_When_Bridges_Are_Allowed()
    {
        var grid = new MapGrid(6, 3);
        grid.SetSurface(new GridPosition(2, 1), MapSurfaceType.Water);
        grid.SetSurface(new GridPosition(3, 1), MapSurfaceType.Water);

        for (var x = 0; x < 6; x++)
        {
            grid.SetSurface(new GridPosition(x, 0), MapSurfaceType.Blocked);
            grid.SetSurface(new GridPosition(x, 2), MapSurfaceType.Blocked);
        }

        var result = new RoadPathfinder().FindPath(grid, new GridPosition(0, 1), new GridPosition(5, 1));

        Assert.True(result.Found);
        Assert.Contains(new GridPosition(2, 1), result.Cells);
        Assert.Contains(new GridPosition(3, 1), result.Cells);

        Assert.True(grid.TryBuildRoadPath(result.Cells, RoadKind.AccessRoad, 6, districtId: 1));
        Assert.Equal(RoadKind.Bridge, grid.GetCell(new GridPosition(2, 1)).RoadKind);
        Assert.Equal(RoadKind.AccessRoad, grid.GetCell(new GridPosition(1, 1)).RoadKind);
    }

    [Fact]
    public void Built_Path_Connects_To_Existing_Road_And_Forms_Intersection()
    {
        var grid = new MapGrid(8, 8);
        grid.SetRoad(new GridPosition(4, 2), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(4, 3), RoadKind.LocalRoad, 6, districtId: 1);
        grid.SetRoad(new GridPosition(4, 4), RoadKind.LocalRoad, 6, districtId: 1);

        var result = new RoadPathfinder().FindPath(grid, new GridPosition(1, 3), new GridPosition(4, 3));

        Assert.True(result.Found);
        Assert.True(grid.TryBuildRoadPath(result.Cells, RoadKind.AccessRoad, 4, districtId: 1));

        var intersection = grid.GetCell(new GridPosition(4, 3));
        Assert.Equal(MapInfrastructureType.Intersection, intersection.Infrastructure);
        Assert.Equal(RoadTileKind.TJunction, intersection.RoadTileKind);
        Assert.Equal(RoadDirection.North | RoadDirection.South | RoadDirection.West, intersection.RoadConnections);
    }

    [Fact]
    public void District_Expansion_Claims_Free_Cells_Only_In_Selected_Direction()
    {
        var grid = CreateGridWithDistrict(20, 20, districtId: 1, minX: 5, minY: 5, maxX: 9, maxY: 9);
        grid.SetRoad(new GridPosition(8, 7), RoadKind.LocalRoad, 6, districtId: 1);

        var expanded = new MapDistrictExpander().TryExpandDistrict(
            grid,
            districtId: 1,
            MapEdgeDirection.East,
            depthMeters: 2,
            population: 25,
            out var result);

        Assert.True(expanded);
        Assert.NotNull(result);
        Assert.Equal(10, result.AddedCells);
        Assert.True(result.RoadNetworkUpdated);
        Assert.Equal(25, result.Coverage.Population);
        Assert.All(Enumerable.Range(5, 5), y =>
        {
            Assert.Equal(1, grid.GetCell(new GridPosition(10, y)).DistrictId);
            Assert.Equal(1, grid.GetCell(new GridPosition(11, y)).DistrictId);
        });
        Assert.Contains(result.Boundary.Cells, position =>
            position.X is 10 or 11 && grid.GetCell(position).HasRoad);
        Assert.Null(grid.GetCell(new GridPosition(4, 5)).DistrictId);
        Assert.True(result.Boundary.Contains(new GridPosition(11, 7)));
    }

    [Fact]
    public void District_Expansion_Is_Rejected_When_Selected_Edge_Has_No_Free_Cells()
    {
        var grid = CreateGridWithDistrict(12, 12, districtId: 1, minX: 0, minY: 3, maxX: 4, maxY: 8);

        var expanded = new MapDistrictExpander().TryExpandDistrict(
            grid,
            districtId: 1,
            MapEdgeDirection.West,
            depthMeters: 2,
            out var result);

        Assert.False(expanded);
        Assert.Null(result);
        Assert.All(Enumerable.Range(3, 6), y => Assert.Equal(1, grid.GetCell(new GridPosition(0, y)).DistrictId));
    }

    [Fact]
    public void Coverage_Uses_Road_Network_Instead_Of_Straight_Line_Distance()
    {
        var grid = CreateGridWithDistrict(30, 12, districtId: 1, minX: 0, minY: 0, maxX: 29, maxY: 11);
        for (var x = 0; x <= 8; x++)
        {
            grid.SetRoad(new GridPosition(x, 5), RoadKind.LocalRoad, 6, districtId: 1);
        }

        for (var x = 18; x <= 24; x++)
        {
            grid.SetRoad(new GridPosition(x, 5), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var shop = new PlacedMapObject(
            "shop-1",
            PlacedMapObjectType.Business,
            districtId: 1,
            position: new GridPosition(2, 3),
            widthMeters: 2,
            lengthMeters: 2,
            accessSides: MapAccessSide.South,
            assetKey: "business.shop");
        Assert.True(grid.TryPlaceObject(shop));

        var profiles = new MapCoverageProfileCatalog(new[]
        {
            ("business.shop", new MapCoverageProfile(MapCoverageKind.Trade, RadiusMeters: 6, WalkAccessMeters: 2, CoversPopulation: true))
        });
        var report = new MapCoverageAnalyzer(profiles).AnalyzeDistrict(grid, districtId: 1, population: 100);

        var summary = Assert.Contains(MapCoverageKind.Trade, report.Summaries);
        var shopCoverage = Assert.Single(report.Objects);
        Assert.True(summary.CoveredCells > 0);
        Assert.True(summary.CoveredPopulationEstimate > 0);
        Assert.True(summary.UncoveredPopulationEstimate > 0);
        Assert.Contains(new GridPosition(4, 4), shopCoverage.CoveredCells);
        Assert.DoesNotContain(new GridPosition(20, 4), shopCoverage.CoveredCells);
    }

    [Fact]
    public void Farms_Are_Resource_Production_And_Do_Not_Create_Population_Coverage()
    {
        var grid = CreateGridWithDistrict(20, 12, districtId: 1, minX: 0, minY: 0, maxX: 19, maxY: 11);
        for (var x = 0; x <= 8; x++)
        {
            grid.SetRoad(new GridPosition(x, 5), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var farm = new PlacedMapObject(
            "farm-1",
            PlacedMapObjectType.Business,
            districtId: 1,
            position: new GridPosition(2, 3),
            widthMeters: 2,
            lengthMeters: 2,
            accessSides: MapAccessSide.South,
            assetKey: "business.farm");
        Assert.True(grid.TryPlaceObject(farm));

        var report = new MapCoverageAnalyzer().AnalyzeDistrict(grid, districtId: 1, population: 100);
        var farmCoverage = Assert.Single(report.Objects);

        Assert.Equal(MapCoverageKind.ResourceProduction, farmCoverage.Kind);
        Assert.Equal(0, farmCoverage.CoveredCellCount);
        Assert.Empty(farmCoverage.CoveredCells);
        Assert.Empty(report.Summaries);
    }

    [Fact]
    public void District_Formation_Analyzer_Uses_Space_Coverage_Resources_And_Social_Reasons()
    {
        var grid = CreateGridWithDistrict(20, 20, districtId: 1, minX: 0, minY: 0, maxX: 19, maxY: 19);
        var map = CreateMapGenerationResult(grid, districtId: 1);
        var world = new WorldState();
        world.Districts.Add(new District("Central")
        {
            Id = 1,
            Population = 80,
            SupportRating = 40f,
            HasActiveCrisis = true
        });
        world.Businesses.Add(new Business("Farm 1", "farm", 6)
        {
            DistrictId = 1,
            ProductionType = "food"
        });
        world.Businesses.Add(new Business("Farm 2", "farm", 6)
        {
            DistrictId = 1,
            ProductionType = "food"
        });

        var assessment = new MapDistrictFormationAnalyzer(new MapDistrictFormationOptions(
            PopulationPerHectareThreshold: 100f,
            MinimumDistrictFreeCells: 500,
            FarmCountPressureThreshold: 2)).Analyze(world, map, districtId: 1);

        Assert.True(assessment.ShouldCreateDistrict);
        Assert.Contains(MapDistrictFormationReason.NoExpansionSpace, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.PopulationDensityPressure, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.ResourceProductionSpacePressure, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.TradeCoverageGap, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.HealthcareCoverageGap, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.SocialPressure, assessment.Reasons);
    }

    [Fact]
    public void District_Formation_Analyzer_Uses_Logistics_Distance_As_A_New_District_Reason()
    {
        var grid = CreateGridWithDistrict(60, 16, districtId: 1, minX: 0, minY: 0, maxX: 59, maxY: 15);
        grid.SetRoad(new GridPosition(1, 7), RoadKind.RegionalRoad, 8);
        for (var x = 2; x <= 55; x++)
        {
            grid.SetRoad(new GridPosition(x, 7), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var workshop = new PlacedMapObject(
            "business:10",
            PlacedMapObjectType.Business,
            districtId: 1,
            position: new GridPosition(50, 5),
            widthMeters: 2,
            lengthMeters: 2,
            accessSides: MapAccessSide.South,
            assetKey: "business.workshop");
        Assert.True(grid.TryPlaceObject(workshop));

        var map = CreateMapGenerationResult(grid, districtId: 1, includeExpansionSpace: false);
        var world = new WorldState();
        world.Districts.Add(new District("Central") { Id = 1, Population = 40, SupportRating = 75f });
        world.Businesses.Add(new Business("Workshop", "workshop", 6)
        {
            Id = 10,
            DistrictId = 1,
            ProductionType = "goods"
        });

        var assessment = new MapDistrictFormationAnalyzer(new MapDistrictFormationOptions(
            PopulationPerHectareThreshold: 1000f,
            MaximumAverageLogisticsDistanceMeters: 20)).Analyze(world, map, districtId: 1);

        Assert.True(assessment.ShouldCreateDistrict);
        Assert.Contains(MapDistrictFormationReason.NoExpansionSpace, assessment.Reasons);
        Assert.Contains(MapDistrictFormationReason.LogisticsDistancePressure, assessment.Reasons);
    }

    [Fact]
    public void District_Founder_Creates_New_District_In_Free_Space_With_Road_Connection()
    {
        var grid = CreateGridWithDistrict(70, 36, districtId: 1, minX: 4, minY: 8, maxX: 20, maxY: 24);
        for (var x = 6; x <= 18; x++)
        {
            grid.SetRoad(new GridPosition(x, 16), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var created = new MapDistrictFounder().TryCreateDistrict(
            grid,
            existingDistrictId: 1,
            newDistrictId: 2,
            name: "Northfield",
            new MapNewDistrictOptions(WidthMeters: 14, HeightMeters: 12, CandidateStepMeters: 3),
            out var result);

        Assert.True(created);
        Assert.NotNull(result);
        Assert.Equal(2, result.Area.DistrictId);
        Assert.True(result.RegionalRoadPath.Count > 0);
        Assert.All(result.Boundary.Cells, position => Assert.Equal(2, grid.GetCell(position).DistrictId));
        Assert.Contains(grid.Cells, cell => cell.RoadKind == RoadKind.RegionalRoad);
        Assert.Contains(grid.Cells, cell => cell.DistrictId == 2 && cell.RoadKind == RoadKind.LocalRoad);
        Assert.DoesNotContain(result.Boundary.Cells, position => grid.GetCell(position).DistrictId == 1);
    }

    [Fact]
    public void District_Founder_Rejects_New_District_When_No_Free_Area_Fits()
    {
        var grid = CreateGridWithDistrict(20, 20, districtId: 1, minX: 0, minY: 0, maxX: 19, maxY: 19);
        for (var x = 2; x <= 18; x++)
        {
            grid.SetRoad(new GridPosition(x, 10), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var created = new MapDistrictFounder().TryCreateDistrict(
            grid,
            existingDistrictId: 1,
            newDistrictId: 2,
            name: "Northfield",
            new MapNewDistrictOptions(WidthMeters: 10, HeightMeters: 10, CandidateStepMeters: 2),
            out var result);

        Assert.False(created);
        Assert.Null(result);
        Assert.DoesNotContain(grid.Cells, cell => cell.DistrictId == 2);
    }

    [Fact]
    public void Growth_Planner_Creates_New_District_From_Simulation_Pressure()
    {
        var grid = CreateGridWithDistrict(70, 36, districtId: 1, minX: 4, minY: 8, maxX: 20, maxY: 24);
        for (var x = 6; x <= 18; x++)
        {
            grid.SetRoad(new GridPosition(x, 16), RoadKind.LocalRoad, 6, districtId: 1);
        }

        var map = CreateMapGenerationResult(grid, districtId: 1, includeExpansionSpace: false);
        var world = new WorldState();
        world.Districts.Add(new District("Central")
        {
            Id = 1,
            Population = 90,
            SupportRating = 75f
        });

        var created = new MapDistrictGrowthPlanner().TryCreateDistrictFromPressure(
            world,
            map,
            new MapDistrictGrowthOptions(
                FormationOptions: new MapDistrictFormationOptions(PopulationPerHectareThreshold: 100f),
                NewDistrictOptions: new MapNewDistrictOptions(WidthMeters: 14, HeightMeters: 12, CandidateStepMeters: 3),
                DistrictNamePrefix: "Northfield"),
            out var result);

        Assert.True(created);
        Assert.NotNull(result);
        Assert.Equal(1, result.SourceDistrictId);
        Assert.Equal(2, result.NewDistrictId);
        Assert.Contains(world.Districts, district => district.Id == 2 && district.Name == "Northfield 2");
        Assert.Contains(MapDistrictFormationReason.NoExpansionSpace, result.Assessment.Reasons);
        Assert.True(result.FoundedDistrict.RegionalRoadPath.Count > 0);
        Assert.Contains(grid.Cells, cell => cell.DistrictId == 2);
        Assert.Contains(grid.Cells, cell => cell.RoadKind == RoadKind.RegionalRoad);
    }

    private static MapGrid CreateGridWithDistrict(
        int width,
        int height,
        int districtId,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        var grid = new MapGrid(width, height);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                grid.GetCell(new GridPosition(x, y)).DistrictId = districtId;
            }
        }

        return grid;
    }

    private static MapGridGenerationResult CreateMapGenerationResult(
        MapGrid grid,
        int districtId,
        bool includeExpansionSpace = true)
    {
        var area = new MapDistrictGridArea(districtId, "Central", new GridPosition(0, 0), grid.WidthMeters, grid.HeightMeters);
        var areas = new Dictionary<int, MapDistrictGridArea> { [districtId] = area };
        var boundary = MapDistrictBoundary.Build(grid, districtId);
        var boundaries = new Dictionary<int, MapDistrictBoundary> { [districtId] = boundary };
        var expansionSpaces = includeExpansionSpace
            ? new Dictionary<int, MapDistrictExpansionSpace>
            {
                [districtId] = MapDistrictExpansionSpace.Build(grid, boundary)
            }
            : new Dictionary<int, MapDistrictExpansionSpace>();

        return new MapGridGenerationResult(
            grid,
            areas,
            MapFreeSpaceIndex.Build(grid),
            boundaries,
            expansionSpaces);
    }
}
