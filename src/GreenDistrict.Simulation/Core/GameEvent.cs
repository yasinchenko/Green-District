using System.Collections.Generic;
using GreenDistrict.Simulation.Map;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a game event or notification.
/// </summary>
public class GameEvent
{
    private static int _nextId = 1;

    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public long CreatedAtTick { get; set; }
    public EventType Type { get; set; }
    public bool IsResolved { get; set; }
    public string? SelectedChoiceId { get; set; }
    public MapObjectEntityKind TargetEntityKind { get; set; } = MapObjectEntityKind.None;
    public int? TargetEntityId { get; set; }
    public LocalBuildingEventKind? LocalBuildingEventKind { get; set; }
    public float Severity { get; set; }
    public List<EventChoice> Choices { get; } = new();
    public bool HasChoices => Choices.Count > 0;
    public bool HasTargetEntity => TargetEntityKind != MapObjectEntityKind.None && TargetEntityId.HasValue;

    public GameEvent(string title, string description, EventType type, int? id = null)
    {
        Id = id ?? _nextId++;
        if (id.HasValue && id.Value >= _nextId)
        {
            _nextId = id.Value + 1;
        }

        Title = title;
        Description = description;
        Type = type;
    }
}

public enum LocalBuildingEventKind
{
    Fire,
    Flood,
    PowerOutage,
    UtilityFailure,
    Vandalism,
    Sanitation,
    StructuralWear
}

public class EventChoice
{
    public string Id { get; set; }
    public string Label { get; set; }
    public string Description { get; set; }
    public int? DistrictId { get; set; }
    public float BudgetEffect { get; set; }
    public float SupportEffect { get; set; }
    public float FoodSatisfactionEffect { get; set; }
    public float HousingSatisfactionEffect { get; set; }
    public float SafetySatisfactionEffect { get; set; }
    public float HealthcareSatisfactionEffect { get; set; }
    public float EntertainmentSatisfactionEffect { get; set; }
    public bool ResolveDistrictCrisis { get; set; }

    public EventChoice(string id, string label, string description = "")
    {
        Id = id;
        Label = label;
        Description = description;
    }
}

public enum EventType
{
    Notification,
    Crisis,
    Decision,
    Election,
    Economic,
    Social,
    Political
}
