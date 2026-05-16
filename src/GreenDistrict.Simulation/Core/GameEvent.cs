namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a game event or notification.
/// </summary>
public class GameEvent
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public long CreatedAtTick { get; set; }
    public EventType Type { get; set; }

    public GameEvent(string title, string description, EventType type)
    {
        Title = title;
        Description = description;
        Type = type;
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
