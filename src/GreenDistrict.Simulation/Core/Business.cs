using System.Collections.Generic;
using System;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a business or workplace.
/// </summary>
public class Business
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? DistrictId { get; set; }
    public string Type { get; set; } // farm, factory, shop, etc.
    public string ProductionType { get; set; } = string.Empty;
    public List<int> EmployeeIds { get; } = new();
    public float WagePerEmployee { get; set; } = 500f;
    public int EmployeeCount { get; set; }
    public int MaxEmployees { get; set; }
    public float BaseOutput { get; set; }
    public float UnitPrice { get; set; } = 1f;
    public float DemandMultiplier { get; set; } = 1f;
    public float Revenue { get; set; }
    public float Expenses { get; set; }
    public float LastProducedUnits { get; set; }
    public float LastSoldUnits { get; set; }
    public float LastSalesRevenue { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.Active;
    public int ConsecutiveLossTicks { get; set; }
    public long? ClosedAtTick { get; set; }

    public Business(string name, string type, int maxEmployees)
    {
        Name = name;
        Type = type;
        MaxEmployees = maxEmployees;
    }

    public float GetStaffingRatio()
    {
        if (MaxEmployees <= 0) return 0f;
        return Math.Clamp(EmployeeIds.Count / (float)MaxEmployees, 0f, 1f);
    }

    public float GetProfit() => Revenue - Expenses;
}

public enum BusinessStatus
{
    Active,
    Bankrupt,
    Closed
}
