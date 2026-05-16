using System;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Manages simulation time and ticks.
/// 1 tick = 1 game minute
/// 60 ticks = 1 game hour
/// 1440 ticks = 1 game day
/// </summary>
public class SimulationClock
{
    private long _currentTick = 0;
    private float _timeScale = 1.0f; // 1x speed by default
    
    public long CurrentTick => _currentTick;
    
    public int Hour => (int)((_currentTick % 1440) / 60);
    public int Minute => (int)(_currentTick % 60);
    public int Day => (int)(_currentTick / 1440);
    
    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Max(0.1f, value); // Min 0.1x speed
    }
    
    /// <summary>
    /// Advances simulation by one tick.
    /// Returns the number of ticks passed.
    /// </summary>
    public long Tick()
    {
        _currentTick++;
        return _currentTick;
    }
    
    /// <summary>
    /// Advance simulation by multiple ticks at once.
    /// </summary>
    public long AdvanceTicks(long count)
    {
        _currentTick += Math.Max(0, count);
        return _currentTick;
    }

    public void SetCurrentTick(long tick)
    {
        _currentTick = Math.Max(0, tick);
    }
    
    /// <summary>
    /// Get formatted time string (HH:MM format)
    /// </summary>
    public string GetTimeString() => $"{Hour:D2}:{Minute:D2}";
    
    /// <summary>
    /// Get formatted full date and time
    /// </summary>
    public string GetFullTimeString() => $"Day {Day}, {GetTimeString()}";
    
    /// <summary>
    /// Reset to day 0, tick 0
    /// </summary>
    public void Reset()
    {
        _currentTick = 0;
        _timeScale = 1.0f;
    }
}
