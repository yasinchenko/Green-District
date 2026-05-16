using System;

namespace GreenDistrict.Simulation.Core;

public class GovernmentProject
{
    private static int _nextId = 1;
    public int Id { get; }
    public string Name { get; set; }
    public float Cost { get; set; }
    public int DurationTicks { get; set; }
    public int RemainingTicks { get; set; }
    public float Benefit { get; set; } // one-time budget benefit upon completion
    public int? DistrictId { get; set; }
    public bool Completed { get; set; }
    public long StartTick { get; set; }

    public GovernmentProject(string name, float cost, int durationTicks, float benefit = 0f)
    {
        Id = _nextId++;
        Name = name;
        Cost = cost;
        DurationTicks = durationTicks;
        RemainingTicks = durationTicks;
        Benefit = benefit;
        Completed = false;
        StartTick = -1;
    }
}
