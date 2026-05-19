using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreenDistrict.Simulation.Map;

public sealed record MapObjectSizeDefinition(
    string Key,
    PlacedMapObjectType Type,
    int WidthMeters,
    int LengthMeters,
    MapAccessSide AccessSides,
    string AssetKey);

public sealed class MapObjectSizeCatalog
{
    private const string DefaultConfigRelativePath = "data/config/map_object_sizes.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, MapObjectSizeDefinition> _definitions;

    public MapObjectSizeCatalog(IEnumerable<MapObjectSizeDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);
    }

    public static MapObjectSizeCatalog Defaults { get; } = new(new[]
    {
        new MapObjectSizeDefinition("house.small", PlacedMapObjectType.Housing, 8, 10, MapAccessSide.Any, "building.house.small"),
        new MapObjectSizeDefinition("house.medium", PlacedMapObjectType.Housing, 10, 14, MapAccessSide.Any, "building.house.medium"),
        new MapObjectSizeDefinition("shop", PlacedMapObjectType.Business, 12, 16, MapAccessSide.Any, "business.shop"),
        new MapObjectSizeDefinition("workshop", PlacedMapObjectType.Business, 18, 24, MapAccessSide.Any, "business.workshop"),
        new MapObjectSizeDefinition("farm", PlacedMapObjectType.Business, 24, 18, MapAccessSide.Any, "business.farm"),
        new MapObjectSizeDefinition("clinic", PlacedMapObjectType.Service, 20, 28, MapAccessSide.Any, "service.clinic"),
        new MapObjectSizeDefinition("school", PlacedMapObjectType.Service, 35, 45, MapAccessSide.Any, "service.school"),
        new MapObjectSizeDefinition("police", PlacedMapObjectType.Service, 18, 22, MapAccessSide.Any, "service.police"),
        new MapObjectSizeDefinition("park.small", PlacedMapObjectType.Park, 24, 24, MapAccessSide.Any, "park.small"),
        new MapObjectSizeDefinition("marker.event", PlacedMapObjectType.Marker, 3, 3, MapAccessSide.None, "marker.event"),
        new MapObjectSizeDefinition("marker.crisis", PlacedMapObjectType.Marker, 3, 3, MapAccessSide.None, "marker.crisis"),
        new MapObjectSizeDefinition("marker.decision", PlacedMapObjectType.Marker, 3, 3, MapAccessSide.None, "marker.decision")
    });

    public static MapObjectSizeCatalog LoadConfiguredOrDefaults()
    {
        var path = FindConfigFile(Directory.GetCurrentDirectory(), DefaultConfigRelativePath);
        return path == null ? Defaults : LoadJsonFile(path);
    }

    public static MapObjectSizeCatalog LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Map object sizes JSON cannot be empty.", nameof(json));

        var document = JsonSerializer.Deserialize<MapObjectSizesDocument>(json, JsonOptions);
        if (document == null || document.Objects.Count == 0)
        {
            throw new InvalidOperationException("Map object sizes JSON does not contain any object definitions.");
        }

        return new MapObjectSizeCatalog(document.Objects.Select(item => item.ToDefinition()));
    }

    public static MapObjectSizeCatalog LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Map object sizes path cannot be empty.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public IReadOnlyCollection<MapObjectSizeDefinition> Definitions => _definitions.Values;

    public MapObjectSizeDefinition Get(string key)
    {
        if (_definitions.TryGetValue(key, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Map object size '{key}' is not defined.");
    }

    public bool TryGet(string key, out MapObjectSizeDefinition? definition)
    {
        return _definitions.TryGetValue(key, out definition);
    }

    public PlacedMapObject CreateObject(
        string objectId,
        string sizeKey,
        int? districtId,
        GridPosition position,
        int rotationDegrees = 0)
    {
        var definition = Get(sizeKey);
        return new PlacedMapObject(
            objectId,
            definition.Type,
            districtId,
            position,
            definition.WidthMeters,
            definition.LengthMeters,
            rotationDegrees,
            definition.AccessSides,
            definition.AssetKey);
    }

    private static string? FindConfigFile(string startDirectory, string relativePath)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    private sealed class MapObjectSizesDocument
    {
        public List<MapObjectSizeItem> Objects { get; set; } = new();
    }

    private sealed class MapObjectSizeItem
    {
        public string Key { get; set; } = string.Empty;
        public PlacedMapObjectType Type { get; set; }
        public int WidthMeters { get; set; }
        public int LengthMeters { get; set; }
        public MapAccessSide AccessSides { get; set; } = MapAccessSide.Any;
        public string AssetKey { get; set; } = string.Empty;

        public MapObjectSizeDefinition ToDefinition()
        {
            if (string.IsNullOrWhiteSpace(Key)) throw new InvalidOperationException("Map object size key is required.");
            if (WidthMeters <= 0) throw new InvalidOperationException($"Map object size '{Key}' must have positive width.");
            if (LengthMeters <= 0) throw new InvalidOperationException($"Map object size '{Key}' must have positive length.");
            if (string.IsNullOrWhiteSpace(AssetKey)) throw new InvalidOperationException($"Map object size '{Key}' must have an asset key.");

            return new MapObjectSizeDefinition(Key, Type, WidthMeters, LengthMeters, AccessSides, AssetKey);
        }
    }
}
