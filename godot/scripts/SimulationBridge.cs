using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Scenarios;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class SimulationBridge : Node
{
    public WorldState World { get; private set; } = new();

    public override void _Ready()
    {
        if (World.Citizens.Count == 0)
        {
            ResetWorld();
        }
    }

    public void ResetWorld(int seed = 0)
    {
        var scenario = WorldScenarioLoader.CreateDefault();
        scenario.Seed = seed;

        World = new WorldState(seed);
        World.Initialize(scenario);
    }

    public void StepTicks(int ticks)
    {
        if (ticks <= 0) return;
        SimulationRunner.Run(World, ticks);
    }

    public bool StartProject(ProjectType type, int? districtId)
    {
        var project = GovernmentProject.CreateTyped(type, districtId);
        return World.Government.StartProject(World, project);
    }

    public global::Godot.Collections.Dictionary GetSnapshot()
    {
        return new global::Godot.Collections.Dictionary
        {
            ["tick"] = World.Clock.CurrentTick,
            ["time"] = World.Clock.GetTimeString(),
            ["day"] = World.Clock.Day,
            ["population"] = World.GetTotalPopulation(),
            ["budget"] = World.Budget,
            ["support"] = World.SupportRating,
            ["satisfaction"] = World.GetAverageSatisfaction(),
            ["unemployment"] = World.LastUnemploymentRate,
            ["events"] = World.Events.Count,
            ["businesses"] = World.Businesses.Count,
            ["activeBusinesses"] = World.Businesses.Count(b => b.Status == BusinessStatus.Active)
        };
    }
}
