using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Defines the order of system updates during each simulation tick.
/// </summary>
public enum UpdatePhase
{
    TimeUpdate = 0,              // 1. Time update
    CitizenNeedsUpdate = 1,      // 2. Citizen needs update
    JobAndIncomeUpdate = 2,      // 3. Job and income update
    EconomyUpdate = 3,           // 4. Economy update
    BusinessUpdate = 4,          // 5. Business update
    DistrictAggregates = 5,      // 6. District aggregates update
    EventTriggerCheck = 6,       // 7. Event trigger check
    CrisisProgression = 7,       // 8. Crisis progression
    PoliticalSupportUpdate = 8,  // 9. Political support update
    ElectionCheck = 9,           // 10. Election check
    NotificationGeneration = 10, // 11. Notification generation
    UIRefreshRequest = 11        // 12. UI refresh request
}

/// <summary>
/// Manages the simulation update cycle.
/// Ensures predictable and debuggable update order.
/// </summary>
public class UpdateManager
{
    private readonly Dictionary<UpdatePhase, List<Action>> _updateHandlers = new();
    
    /// <summary>
    /// Register a handler for a specific update phase.
    /// </summary>
    public void Register(UpdatePhase phase, Action handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
            
        if (!_updateHandlers.TryGetValue(phase, out var handlers))
        {
            handlers = new List<Action>();
            _updateHandlers[phase] = handlers;
        }

        handlers.Add(handler);
    }
    
    /// <summary>
    /// Unregister a handler for a specific phase.
    /// </summary>
    public void Unregister(UpdatePhase phase)
    {
        _updateHandlers.Remove(phase);
    }
    
    /// <summary>
    /// Execute all registered handlers in the correct order.
    /// </summary>
    public void ExecutePhase(UpdatePhase phase)
    {
        if (_updateHandlers.TryGetValue(phase, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in {phase}: {ex.Message}");
                    throw;
                }
            }
        }
    }
    
    /// <summary>
    /// Execute all phases in the correct order.
    /// </summary>
    public void ExecuteFullCycle()
    {
        for (int i = 0; i <= (int)UpdatePhase.UIRefreshRequest; i++)
        {
            ExecutePhase((UpdatePhase)i);
        }
    }
    
    /// <summary>
    /// Get all registered phases.
    /// </summary>
    public IEnumerable<UpdatePhase> GetRegisteredPhases()
    {
        return _updateHandlers.Keys.OrderBy(p => (int)p);
    }
    
    /// <summary>
    /// Clear all handlers.
    /// </summary>
    public void Clear()
    {
        _updateHandlers.Clear();
    }
}
