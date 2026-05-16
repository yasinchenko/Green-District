using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

try
{
    var summaries = SimulationRunner.RunYearSeries(
        () => CreateWorld(options),
        options.Years,
        options.TicksPerYear);

    if (options.Json)
    {
        Console.WriteLine(JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        PrintText(summaries);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine("Run with --help to see available options.");
    return 1;
}

static WorldState CreateWorld(CliOptions options)
{
    var scenario = string.IsNullOrWhiteSpace(options.ScenarioPath)
        ? WorldScenarioLoader.CreateDefault()
        : WorldScenarioLoader.LoadJsonFile(options.ScenarioPath);

    if (options.Seed.HasValue)
    {
        scenario.Seed = options.Seed.Value;
    }

    if (options.DemographyTicksPerYear.HasValue)
    {
        scenario.DemographyTicksPerYear = options.DemographyTicksPerYear.Value;
    }

    var world = new WorldState(scenario.Seed);
    world.Initialize(scenario);
    return world;
}

static void PrintText(IReadOnlyList<HeadlessRunSummary> summaries)
{
    foreach (var summary in summaries)
    {
        Console.WriteLine($"Years: {summary.YearsRun}");
        Console.WriteLine($"Ticks: {summary.TicksRun} (final: {summary.FinalTick})");
        Console.WriteLine($"Population: {summary.Population}, households: {summary.Households}");
        Console.WriteLine($"Budget: {summary.Budget.ToString("F2", CultureInfo.InvariantCulture)}, net/tick: {summary.LastNetBudgetChange.ToString("F2", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Support: {summary.SupportRating.ToString("F1", CultureInfo.InvariantCulture)}%, in power: {summary.IsInPower}");
        Console.WriteLine($"Satisfaction: {summary.AverageSatisfaction.ToString("F1", CultureInfo.InvariantCulture)}%, unemployment: {summary.UnemploymentRate.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Businesses: {summary.ActiveBusinesses}/{summary.Businesses}, projects active: {summary.ActiveProjects}, events: {summary.Events}");
        Console.WriteLine();
    }
}

static void PrintHelp()
{
    Console.WriteLine("GreenDistrict headless runner");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  GreenDistrict.Cli --years 1,10,50 [--scenario path] [--seed n] [--ticks-per-year n] [--demography-ticks-per-year n] [--json]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --years                      Comma-separated run lengths. Default: 1,10,50.");
    Console.WriteLine("  --scenario                   Scenario JSON path. Default: built-in scenario.");
    Console.WriteLine("  --seed                       Override scenario seed.");
    Console.WriteLine("  --ticks-per-year             Ticks to run per game year. Default: 525600.");
    Console.WriteLine("  --demography-ticks-per-year  Override annual demography cadence in scenario.");
    Console.WriteLine("  --json                       Print JSON summary.");
}

internal sealed class CliOptions
{
    public IReadOnlyList<int> Years { get; private init; } = new[] { 1, 10, 50 };
    public string? ScenarioPath { get; private init; }
    public int? Seed { get; private init; }
    public int TicksPerYear { get; private init; } = SimulationRunner.DefaultTicksPerYear;
    public int? DemographyTicksPerYear { get; private init; }
    public bool Json { get; private init; }
    public bool ShowHelp { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        var years = new List<int>();
        string? scenarioPath = null;
        int? seed = null;
        int ticksPerYear = SimulationRunner.DefaultTicksPerYear;
        int? demographyTicksPerYear = null;
        var json = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--years":
                    years = ParseYears(ReadValue(args, ref i, arg)).ToList();
                    break;
                case "--scenario":
                    scenarioPath = ReadValue(args, ref i, arg);
                    break;
                case "--seed":
                    seed = ParsePositiveOrZeroInt(ReadValue(args, ref i, arg), arg);
                    break;
                case "--ticks-per-year":
                    ticksPerYear = ParsePositiveInt(ReadValue(args, ref i, arg), arg);
                    break;
                case "--demography-ticks-per-year":
                    demographyTicksPerYear = ParsePositiveInt(ReadValue(args, ref i, arg), arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arg}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(scenarioPath) && !File.Exists(scenarioPath))
        {
            throw new FileNotFoundException("Scenario file was not found.", scenarioPath);
        }

        return new CliOptions
        {
            Years = years.Count == 0 ? new[] { 1, 10, 50 } : years,
            ScenarioPath = scenarioPath,
            Seed = seed,
            TicksPerYear = ticksPerYear,
            DemographyTicksPerYear = demographyTicksPerYear,
            Json = json,
            ShowHelp = showHelp
        };
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index++;
        return args[index];
    }

    private static IEnumerable<int> ParseYears(string value)
    {
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return ParsePositiveOrZeroInt(part, "--years");
        }
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        var parsed = ParsePositiveOrZeroInt(value, optionName);
        if (parsed <= 0) throw new ArgumentException($"{optionName} must be greater than zero.");
        return parsed;
    }

    private static int ParsePositiveOrZeroInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new ArgumentException($"{optionName} must be a non-negative integer.");
        }

        return parsed;
    }
}
