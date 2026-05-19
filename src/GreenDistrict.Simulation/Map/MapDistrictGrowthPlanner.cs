using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Map;

public sealed record MapDistrictGrowthOptions(
    MapDistrictFormationOptions? FormationOptions = null,
    MapNewDistrictOptions? NewDistrictOptions = null,
    string DistrictNamePrefix = "District");

public sealed record MapDistrictGrowthResult(
    int SourceDistrictId,
    int NewDistrictId,
    MapDistrictFormationAssessment Assessment,
    MapNewDistrictResult FoundedDistrict);

public sealed class MapDistrictGrowthPlanner
{
    public bool TryCreateDistrictFromPressure(
        WorldState world,
        MapGridGenerationResult map,
        MapDistrictGrowthOptions? options,
        out MapDistrictGrowthResult? result)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (map == null) throw new ArgumentNullException(nameof(map));

        options ??= new MapDistrictGrowthOptions();
        var analyzer = new MapDistrictFormationAnalyzer(options.FormationOptions);
        var assessments = world.Districts
            .OrderBy(district => district.Id)
            .Select(district => analyzer.Analyze(world, map, district.Id))
            .Where(assessment => assessment.ShouldCreateDistrict)
            .OrderByDescending(assessment => assessment.Reasons.Count)
            .ToList();

        foreach (var assessment in assessments)
        {
            var newDistrictId = NextDistrictId(world);
            var name = $"{options.DistrictNamePrefix} {newDistrictId}";
            if (!new MapDistrictFounder().TryCreateDistrict(
                    map.Grid,
                    assessment.DistrictId,
                    newDistrictId,
                    name,
                    options.NewDistrictOptions,
                    out var foundedDistrict) ||
                foundedDistrict == null)
            {
                continue;
            }

            world.Districts.Add(new District(name) { Id = newDistrictId });
            result = new MapDistrictGrowthResult(
                assessment.DistrictId,
                newDistrictId,
                assessment,
                foundedDistrict);
            return true;
        }

        result = null;
        return false;
    }

    public IReadOnlyList<MapDistrictFormationAssessment> AnalyzeAll(
        WorldState world,
        MapGridGenerationResult map,
        MapDistrictFormationOptions? options = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (map == null) throw new ArgumentNullException(nameof(map));

        var analyzer = new MapDistrictFormationAnalyzer(options);
        return new ReadOnlyCollection<MapDistrictFormationAssessment>(
            world.Districts
                .OrderBy(district => district.Id)
                .Select(district => analyzer.Analyze(world, map, district.Id))
                .ToList());
    }

    private static int NextDistrictId(WorldState world)
    {
        return world.Districts.Count == 0 ? 1 : world.Districts.Max(district => district.Id) + 1;
    }
}
