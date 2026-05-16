using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GreenDistrict.Simulation.Economy;

public class ProfessionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float BaseWage { get; set; }
    public int SkillLevel { get; set; }
}

public class ProfessionCatalog
{
    private readonly Dictionary<string, ProfessionDefinition> _byId;
    private readonly Dictionary<string, ProfessionDefinition> _byName;

    public ProfessionCatalog(IEnumerable<ProfessionDefinition> professions)
    {
        if (professions == null) throw new ArgumentNullException(nameof(professions));

        var validProfessions = professions
            .Where(p => !string.IsNullOrWhiteSpace(p.Id) || !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        _byId = validProfessions
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        _byName = validProfessions
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ProfessionDefinition> Professions => _byId.Values;

    public bool TryGet(string idOrName, out ProfessionDefinition definition)
    {
        definition = default!;
        if (string.IsNullOrWhiteSpace(idOrName)) return false;
        return _byId.TryGetValue(idOrName, out definition!) ||
               _byName.TryGetValue(idOrName, out definition!);
    }

    public float GetBaseWageOrDefault(string profession, float fallbackWage)
    {
        return TryGet(profession, out var definition)
            ? Math.Max(0f, definition.BaseWage)
            : fallbackWage;
    }

    public static ProfessionCatalog LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Profession JSON cannot be empty.", nameof(json));

        var document = JsonSerializer.Deserialize<ProfessionsDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (document == null) throw new InvalidOperationException("Profession JSON is invalid.");
        return new ProfessionCatalog(document.Professions);
    }

    public static ProfessionCatalog LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Profession path cannot be empty.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    private sealed class ProfessionsDocument
    {
        public List<ProfessionDefinition> Professions { get; set; } = new();
    }
}
