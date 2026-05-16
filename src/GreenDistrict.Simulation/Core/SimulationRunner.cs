using System;
using System.Threading;
using System.Threading.Tasks;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Simple headless simulation runner. Runs the simulation for a number of ticks.
/// </summary>
public static class SimulationRunner
{
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
}
