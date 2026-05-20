using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Map;
using GreenDistrict.Simulation.Scenarios;
using Xunit;

namespace GreenDistrict.Tests;

public class MapGridGeneratorTests
{
    [Fact]
    public void Generator_Assigns_District_Cells()
    {
        var world = CreateWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));

        Assert.Equal(2, result.DistrictAreas.Count);
        foreach (var district in world.Districts)
        {
            Assert.Contains(result.Grid.Cells, cell => cell.DistrictId == district.Id);
        }
    }

    [Fact]
    public void DefaultScenario_Generator_Builds_All_Districts_With_Roads_And_Objects()
    {
        var scenario = WorldScenarioLoader.CreateDefault();
        var world = new WorldState(scenario.Seed);
        world.Initialize(scenario);

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(180, 120));

        Assert.Equal(world.Districts.Count, result.DistrictAreas.Count);
        foreach (var district in world.Districts)
        {
            Assert.Contains(result.Grid.Cells, cell => cell.DistrictId == district.Id);
            Assert.Contains(result.Grid.Cells, cell => cell.DistrictId == district.Id && cell.HasRoad);
            Assert.Contains(result.Grid.Objects.Values, mapObject => mapObject.DistrictId == district.Id);
        }
    }

    [Fact]
    public void Generator_Creates_Local_Roads_For_Districts_With_Starter_Objects()
    {
        var world = CreateWorld();
        world.HousingUnits.Add(new HousingUnit(100, districtId: 1, capacity: 2));
        world.HousingUnits.Add(new HousingUnit(101, districtId: 2, capacity: 2));

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));

        foreach (var district in world.Districts)
        {
            Assert.Contains(result.Grid.Cells, cell =>
                cell.DistrictId == district.Id &&
                cell.RoadKind == RoadKind.LocalRoad);
        }
    }

    [Fact]
    public void Generator_Creates_At_Least_One_Regional_Road_Between_Districts()
    {
        var world = CreateWorldWithStarterObjects();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));

        Assert.Contains(result.Grid.Cells, cell => cell.RoadKind == RoadKind.RegionalRoad);
    }

    [Fact]
    public void Generated_Regional_Road_Connects_To_District_Road_Networks()
    {
        var world = CreateWorldWithStarterObjects();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));
        var regionalRoadCells = result.Grid.Cells.Where(cell => cell.RoadKind == RoadKind.RegionalRoad).ToList();

        foreach (var district in world.Districts)
        {
            Assert.Contains(result.Grid.Cells, cell =>
                cell.DistrictId == district.Id &&
                cell.HasRoad &&
                regionalRoadCells.Any(regional => AreAdjacent(cell.Position, regional.Position) || regional.Position == cell.Position));
        }
    }

    [Fact]
    public void Generator_Adds_Water_Terrain_Layer()
    {
        var world = CreateWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));

        Assert.Contains(result.Grid.Cells, cell => cell.Surface == MapSurfaceType.Water);
    }

    [Fact]
    public void Generator_Creates_Irregular_Water_Boundaries()
    {
        var world = CreateWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));
        var waterRows = result.Grid.Cells
            .Where(cell => cell.Surface == MapSurfaceType.Water)
            .GroupBy(cell => cell.Position.Y)
            .Select(row => new
            {
                Y = row.Key,
                MinX = row.Min(cell => cell.Position.X),
                MaxX = row.Max(cell => cell.Position.X),
                Width = row.Max(cell => cell.Position.X) - row.Min(cell => cell.Position.X) + 1
            })
            .OrderBy(row => row.Y)
            .ToList();

        Assert.True(waterRows.Count > 8);
        Assert.True(waterRows.Select(row => row.MinX).Distinct().Count() > 4);
        Assert.True(waterRows.Select(row => row.MaxX).Distinct().Count() > 4);
        Assert.True(waterRows.Select(row => row.Width).Distinct().Count() > 4);
    }

    [Fact]
    public void Generator_Rejects_Map_Size_Above_Configured_Maximum()
    {
        var world = CreateWorld();
        var options = new MapGridGenerationOptions(
            WidthMeters: 121,
            HeightMeters: 80,
            MaxWidthMeters: 120,
            MaxHeightMeters: 80);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => new MapGridGenerator().Generate(world, options));
    }

    [Fact]
    public void Generator_Builds_Free_Space_Index_For_Districts_And_Reserve_Cells()
    {
        var world = CreateWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));

        Assert.True(result.FreeSpace.Count(MapSpaceCategory.DistrictFreeLand) > 0);
        Assert.True(result.FreeSpace.Count(MapSpaceCategory.FutureDistrictReserve) > 0);
        Assert.True(result.FreeSpace.Count(MapSpaceCategory.Water) > 0);
        Assert.Equal(result.Grid.Cells.Count(), result.FreeSpace.CellsByCategory.Values.Sum(cells => cells.Count));
    }

    [Fact]
    public void Generator_Builds_District_Boundaries_As_Cell_Sets()
    {
        var world = CreateWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));
        var boundary = Assert.Contains(1, result.DistrictBoundaries);

        Assert.True(boundary.Cells.Count > 0);
        Assert.All(boundary.Cells, position => Assert.Equal(1, result.Grid.GetCell(position).DistrictId));
        Assert.Contains(boundary.EdgeCells.Values, edge => edge.Count > 0);
    }

    [Fact]
    public void Generator_Calculates_Free_Expansion_Space_By_Direction()
    {
        var world = CreateSingleDistrictWorld();

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(120, 80));
        var expansion = Assert.Contains(1, result.ExpansionSpaces);

        Assert.True(expansion.CanExpand);
        Assert.Contains(expansion.FreeCellsByDirection.Values, cells => cells.Count > 0);
        Assert.All(expansion.FreeCellsByDirection.Values.SelectMany(cells => cells), position =>
        {
            var cell = result.Grid.GetCell(position);
            Assert.Null(cell.DistrictId);
            Assert.False(cell.IsWater);
            Assert.False(cell.IsBlocked);
            Assert.False(cell.HasObject);
            Assert.False(cell.HasInfrastructure);
        });
    }

    [Fact]
    public void Generator_Places_Active_Businesses_As_Accessible_Map_Objects()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });
        world.Businesses.Add(new Business("Workshop", "workshop", 6)
        {
            Id = 101,
            DistrictId = 2,
            ProductionType = "goods"
        });

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        var shop = Assert.Contains("business:100", result.Grid.Objects);
        var workshop = Assert.Contains("business:101", result.Grid.Objects);
        Assert.Equal("business.shop", shop.AssetKey);
        Assert.Equal("business.workshop", workshop.AssetKey);
        Assert.Equal(MapObjectEntityKind.Business, shop.EntityKind);
        Assert.Equal(100, shop.EntityId);
        Assert.Equal(MapObjectEntityKind.Business, workshop.EntityKind);
        Assert.Equal(101, workshop.EntityId);
        Assert.True(result.Grid.HasRoadAccess(shop));
        Assert.True(result.Grid.HasRoadAccess(workshop));
    }

    [Fact]
    public void Generator_Uses_Configured_Size_Catalog_By_Default()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        var shop = Assert.Contains("business:100", result.Grid.Objects);
        Assert.Equal((12, 16), (shop.WidthMeters, shop.LengthMeters));
        Assert.Equal("business.shop", shop.AssetKey);
    }

    [Fact]
    public void Generator_Orients_Object_Access_Toward_Adjacent_Road()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));
        var shop = Assert.Contains("business:100", result.Grid.Objects);

        Assert.NotEqual(MapAccessSide.Any, shop.AccessSides);
        Assert.True(shop.AccessSides is MapAccessSide.North or MapAccessSide.East or MapAccessSide.South or MapAccessSide.West);
        Assert.True(result.Grid.HasRoadAccess(shop));
        Assert.Contains(result.Grid.GetAccessCells(shop), position =>
            result.Grid.TryGetCell(position, out var cell) && cell is { HasRoad: true });
    }

    [Fact]
    public void Generator_Places_Projects_As_Accessible_Map_Objects()
    {
        var world = CreateWorld();
        var clinic = GovernmentProject.CreateTyped(ProjectType.Clinic, districtId: 1);
        var park = GovernmentProject.CreateTyped(ProjectType.Park, districtId: 2);
        world.Projects.Add(clinic);
        world.Projects.Add(park);

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        var clinicObject = Assert.Contains($"project:{clinic.Id}", result.Grid.Objects);
        var parkObject = Assert.Contains($"project:{park.Id}", result.Grid.Objects);
        Assert.Equal("service.clinic", clinicObject.AssetKey);
        Assert.Equal("park.small", parkObject.AssetKey);
        Assert.Equal(MapObjectEntityKind.GovernmentProject, clinicObject.EntityKind);
        Assert.Equal(clinic.Id, clinicObject.EntityId);
        Assert.Equal(MapObjectEntityKind.GovernmentProject, parkObject.EntityKind);
        Assert.Equal(park.Id, parkObject.EntityId);
        Assert.True(result.Grid.HasRoadAccess(clinicObject));
        Assert.True(result.Grid.HasRoadAccess(parkObject));
    }

    [Fact]
    public void Generator_Places_Housing_Units_As_Accessible_Map_Objects()
    {
        var world = CreateWorld();
        world.HousingUnits.Add(new HousingUnit(200, districtId: 1, capacity: 2));
        world.HousingUnits.Add(new HousingUnit(201, districtId: 2, capacity: 4));

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        var smallHouse = Assert.Contains("housing:200", result.Grid.Objects);
        var mediumHouse = Assert.Contains("housing:201", result.Grid.Objects);
        Assert.Equal("building.house.small", smallHouse.AssetKey);
        Assert.Equal("building.house.medium", mediumHouse.AssetKey);
        Assert.Equal(MapObjectEntityKind.HousingUnit, smallHouse.EntityKind);
        Assert.Equal(200, smallHouse.EntityId);
        Assert.Equal(MapObjectEntityKind.HousingUnit, mediumHouse.EntityKind);
        Assert.Equal(201, mediumHouse.EntityId);
        Assert.True(result.Grid.HasRoadAccess(smallHouse));
        Assert.True(result.Grid.HasRoadAccess(mediumHouse));
    }

    [Fact]
    public void Generator_Places_Unresolved_District_Events_As_Map_Markers()
    {
        var world = CreateWorld();
        var gameEvent = new GameEvent("Crisis in Central", "Central needs a response.", EventType.Crisis)
        {
            Id = 300
        };
        gameEvent.Choices.Add(new EventChoice("fund", "Fund response")
        {
            DistrictId = 1
        });
        world.Events.Add(gameEvent);

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        var marker = Assert.Contains("event:300", result.Grid.Objects);
        Assert.Equal(PlacedMapObjectType.Marker, marker.Type);
        Assert.Equal("marker.crisis", marker.AssetKey);
        Assert.Equal(MapObjectEntityKind.GameEvent, marker.EntityKind);
        Assert.Equal(300, marker.EntityId);
        Assert.Equal(1, marker.DistrictId);
    }

    [Fact]
    public void Generator_Skips_Resolved_Event_Markers()
    {
        var world = CreateWorld();
        var gameEvent = new GameEvent("Resolved", "Already handled.", EventType.Decision)
        {
            Id = 301,
            IsResolved = true
        };
        gameEvent.Choices.Add(new EventChoice("ok", "Ok")
        {
            DistrictId = 1
        });
        world.Events.Add(gameEvent);

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        Assert.DoesNotContain("event:301", result.Grid.Objects.Keys);
    }

    [Fact]
    public void Generator_Keeps_Object_Positions_Stable_For_Same_World_State()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });
        world.HousingUnits.Add(new HousingUnit(200, districtId: 1, capacity: 2));
        var gameEvent = new GameEvent("Central decision", "Choose a response.", EventType.Decision)
        {
            Id = 300
        };
        gameEvent.Choices.Add(new EventChoice("support", "Support")
        {
            DistrictId = 1
        });
        world.Events.Add(gameEvent);

        var first = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));
        var second = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));

        Assert.Equal(first.Grid.Objects.Keys.OrderBy(id => id), second.Grid.Objects.Keys.OrderBy(id => id));
        foreach (var objectId in first.Grid.Objects.Keys)
        {
            Assert.Equal(first.Grid.Objects[objectId].Position, second.Grid.Objects[objectId].Position);
            Assert.Equal(first.Grid.Objects[objectId].AssetKey, second.Grid.Objects[objectId].AssetKey);
        }
    }

    [Fact]
    public void Generated_Map_Objects_Do_Not_Overlap_Water_Or_Roads()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Corner Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });

        var result = new MapGridGenerator().Generate(world, new MapGridGenerationOptions(160, 100));
        var mapObject = Assert.Contains("business:100", result.Grid.Objects);

        foreach (var position in mapObject.FootprintCells())
        {
            var cell = result.Grid.GetCell(position);
            Assert.False(cell.IsWater);
            Assert.False(cell.HasRoad);
            Assert.Equal("business:100", cell.ObjectId);
        }
    }

    [Fact]
    public void Generator_Skips_Object_When_No_Valid_Placement_Exists()
    {
        var world = CreateWorld();
        world.Businesses.Add(new Business("Oversized Shop", "shop", 4)
        {
            Id = 100,
            DistrictId = 1,
            ProductionType = "trade"
        });
        var catalog = new MapObjectSizeCatalog(new[]
        {
            new MapObjectSizeDefinition("shop", PlacedMapObjectType.Business, 80, 80, MapAccessSide.Any, "business.shop")
        });

        var result = new MapGridGenerator(catalog).Generate(world, new MapGridGenerationOptions(120, 80));

        Assert.DoesNotContain("business:100", result.Grid.Objects.Keys);
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Central") { Id = 1, Population = 25 });
        world.Districts.Add(new District("Riverside") { Id = 2, Population = 25 });
        return world;
    }

    private static WorldState CreateSingleDistrictWorld()
    {
        var world = new WorldState();
        world.Districts.Add(new District("Central") { Id = 1, Population = 25 });
        return world;
    }

    private static WorldState CreateWorldWithStarterObjects()
    {
        var world = CreateWorld();
        world.HousingUnits.Add(new HousingUnit(100, districtId: 1, capacity: 2));
        world.HousingUnits.Add(new HousingUnit(101, districtId: 2, capacity: 2));
        return world;
    }

    private static bool AreAdjacent(GridPosition a, GridPosition b)
    {
        return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) == 1;
    }
}
