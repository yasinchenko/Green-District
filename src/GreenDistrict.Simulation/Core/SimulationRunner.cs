using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Simple headless simulation runner. Runs the simulation for a number of ticks.
/// </summary>
public static class SimulationRunner
{
    public const int DefaultTicksPerYear = 1440 * 365;

    public static void Run(WorldState world, long ticksToRun, Action<WorldState>? perTick = null, CancellationToken? token = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (ticksToRun < 0) throw new ArgumentOutOfRangeException(nameof(ticksToRun));

        var ct = token ?? CancellationToken.None;
        var end = world.Clock.CurrentTick + ticksToRun;
        while (!ct.IsCancellationRequested && world.Clock.CurrentTick < end)
        {
            world.Tick();
            perTick?.Invoke(world);
        }
    }

    public static async Task RunAsync(WorldState world, long ticksToRun, Action<WorldState>? perTick = null, int ticksPerSecond = 0, CancellationToken? token = null)
    {
        if (ticksPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        var ct = token ?? CancellationToken.None;
        var end = world.Clock.CurrentTick + ticksToRun;
        var delayMs = ticksPerSecond <= 0 ? 0 : 1000 / Math.Max(1, ticksPerSecond);
        while (!ct.IsCancellationRequested && world.Clock.CurrentTick < end)
        {
            world.Tick();
            perTick?.Invoke(world);
            if (delayMs > 0) await Task.Delay(delayMs, ct);
        }
    }

    public static HeadlessRunSummary RunYears(
        WorldState world,
        int yearsToRun,
        int ticksPerYear = DefaultTicksPerYear,
        Action<WorldState>? perTick = null,
        CancellationToken? token = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (yearsToRun < 0) throw new ArgumentOutOfRangeException(nameof(yearsToRun));
        if (ticksPerYear <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerYear));

        var startTick = world.Clock.CurrentTick;
        var ticksToRun = checked((long)yearsToRun * ticksPerYear);
        Run(world, ticksToRun, perTick, token);

        return HeadlessRunSummary.FromWorld(world, yearsToRun, world.Clock.CurrentTick - startTick, ticksPerYear);
    }

    public static IReadOnlyList<HeadlessRunSummary> RunYearSeries(
        Func<WorldState> worldFactory,
        IEnumerable<int> yearsToRun,
        int ticksPerYear = DefaultTicksPerYear,
        CancellationToken? token = null)
    {
        if (worldFactory == null) throw new ArgumentNullException(nameof(worldFactory));
        if (yearsToRun == null) throw new ArgumentNullException(nameof(yearsToRun));

        var results = new List<HeadlessRunSummary>();
        foreach (var years in yearsToRun)
        {
            var world = worldFactory();
            results.Add(RunYears(world, years, ticksPerYear, token: token));
        }

        return results;
    }
}

public sealed record HeadlessRunSummary
{
    public int YearsRun { get; init; }
    public long TicksRun { get; init; }
    public int TicksPerYear { get; init; }
    public long FinalTick { get; init; }
    public int Population { get; init; }
    public int Households { get; init; }
    public int Districts { get; init; }
    public int Businesses { get; init; }
    public int ActiveBusinesses { get; init; }
    public IReadOnlyList<string> ActiveBusinessNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ClosedBusinessNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BusinessRunSummary> BusinessSummaries { get; init; } = Array.Empty<BusinessRunSummary>();
    public int Projects { get; init; }
    public int ActiveProjects { get; init; }
    public int Events { get; init; }
    public float Budget { get; init; }
    public float SupportRating { get; init; }
    public bool IsInPower { get; init; }
    public float AverageSatisfaction { get; init; }
    public float UnemploymentRate { get; init; }
    public float LastNetBudgetChange { get; init; }

    public static HeadlessRunSummary FromWorld(WorldState world, int yearsRun, long ticksRun, int ticksPerYear)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        return new HeadlessRunSummary
        {
            YearsRun = yearsRun,
            TicksRun = ticksRun,
            TicksPerYear = ticksPerYear,
            FinalTick = world.Clock.CurrentTick,
            Population = world.GetTotalPopulation(),
            Households = world.Households.Count,
            Districts = world.Districts.Count,
            Businesses = world.Businesses.Count,
            ActiveBusinesses = world.Businesses.Count(b => b.Status == BusinessStatus.Active),
            ActiveBusinessNames = world.Businesses
                .Where(b => b.Status == BusinessStatus.Active)
                .Select(b => b.Name)
                .ToArray(),
            ClosedBusinessNames = world.Businesses
                .Where(b => b.Status != BusinessStatus.Active)
                .Select(b => b.Name)
                .ToArray(),
            BusinessSummaries = world.Businesses
                .Select(b => new BusinessRunSummary
                {
                    Name = b.Name,
                    Status = b.Status.ToString(),
                    Employees = b.EmployeeIds.Count,
                    MaxEmployees = b.MaxEmployees,
                    Profit = b.GetProfit(),
                    ConsecutiveLossTicks = b.ConsecutiveLossTicks
                })
                .ToArray(),
            Projects = world.Projects.Count,
            ActiveProjects = world.Projects.Count(p => !p.Completed),
            Events = world.Events.Count,
            Budget = world.Budget,
            SupportRating = world.SupportRating,
            IsInPower = world.IsInPower,
            AverageSatisfaction = world.GetAverageSatisfaction(),
            UnemploymentRate = world.LastUnemploymentRate,
            LastNetBudgetChange = world.LastNetBudgetChange
        };
    }
}

public sealed record BusinessRunSummary
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Employees { get; init; }
    public int MaxEmployees { get; init; }
    public float Profit { get; init; }
    public int ConsecutiveLossTicks { get; init; }
}
