using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GreenDistrict.Simulation.Economy;

public class BusinessTypeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxEmployees { get; set; }
    public string ProductionType { get; set; } = string.Empty;
    public float BaseOutput { get; set; }
    public float UnitPrice { get; set; } = 1f;
    public float DemandMultiplier { get; set; } = 1f;
}

public class BusinessTypeCatalog
{
    private readonly Dictionary<string, BusinessTypeDefinition> _types;

    public BusinessTypeCatalog(IEnumerable<BusinessTypeDefinition> types)
    {
        if (types == null) throw new ArgumentNullException(nameof(types));

        _types = types
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<BusinessTypeDefinition> Types => _types.Values;

    public bool TryGet(string id, out BusinessTypeDefinition definition)
    {
        return _types.TryGetValue(id, out definition!);
    }

    public static BusinessTypeCatalog LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Business type JSON cannot be empty.", nameof(json));

        var document = JsonSerializer.Deserialize<BusinessTypesDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (document == null) throw new InvalidOperationException("Business type JSON is invalid.");
        return new BusinessTypeCatalog(document.BusinessTypes);
    }

    public static BusinessTypeCatalog LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Business type path cannot be empty.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    private sealed class BusinessTypesDocument
    {
        public List<BusinessTypeDefinition> BusinessTypes { get; set; } = new();
    }
}
