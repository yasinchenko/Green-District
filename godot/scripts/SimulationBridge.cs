using System;
using System.IO;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Persistence;
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

    public void ResetWorld(int? seed = null)
    {
        var worldSeed = seed ?? Random.Shared.Next(1, int.MaxValue);
        var scenario = WorldScenarioLoader.CreateDefault();
        scenario.Seed = worldSeed;

        World = new WorldState(worldSeed);
        World.Initialize(scenario);
    }

    public void StepTicks(int ticks)
    {
        if (ticks <= 0) return;
        SimulationRunner.Run(World, ticks);
    }

    public GovernmentProject? StartProject(ProjectType type, int? districtId)
    {
        var project = GovernmentProject.CreateTyped(type, districtId);
        var started = World.Government.StartProject(World, project);
        if (started)
        {
            World.DistrictsSystem.UpdateDistrictAggregates(World);
            return project;
        }

        return null;
    }

    public bool ResolveEventChoice(int eventId, string choiceId)
    {
        return World.ResolveEventChoice(eventId, choiceId);
    }

    public void SaveWorld(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path cannot be empty.", nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WorldStateSerializer.SaveJsonFile(World, path);
    }

    public bool LoadWorld(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path cannot be empty.", nameof(path));
        if (!File.Exists(path)) return false;

        World = WorldStateSerializer.LoadJsonFile(path);
        World.DistrictsSystem.UpdateDistrictAggregates(World);
        return true;
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
