using System.Collections.Generic;
using System;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a business or workplace.
/// </summary>
public class Business
{
    public const int MaxBusinessLevel = 5;

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
    public int BusinessLevel { get; set; } = 1;
    public float ProductQuality { get; set; } = 1f;
    public float InvestmentReserve { get; set; }
    public float LastInvestment { get; set; }
    public float Cash { get; set; }
    public float Revenue { get; set; }
    public float Expenses { get; set; }
    public float RevenueThisTick { get; set; }
    public float ExpensesThisTick { get; set; }
    public float ProfitThisTick => RevenueThisTick - ExpensesThisTick;
    public float TotalRevenue { get; set; }
    public float TotalExpenses { get; set; }
    public float LastProducedUnits { get; set; }
    public float LastSoldUnits { get; set; }
    public float LastSalesRevenue { get; set; }
    public float LastLocalSalesRevenue { get; set; }
    public float LastExternalSalesRevenue { get; set; }
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

    public float GetProductionMultiplier()
    {
        return 1f + (Math.Clamp(BusinessLevel, 1, MaxBusinessLevel) - 1) * 0.18f;
    }

    public float GetQualityDemandMultiplier()
    {
        return Math.Clamp(ProductQuality, 0.5f, 2f);
    }

    public float GetUpgradeCost()
    {
        return 450f * Math.Clamp(BusinessLevel, 1, MaxBusinessLevel) + Math.Max(0, MaxEmployees) * 25f;
    }

    public bool CanUpgrade() => BusinessLevel < MaxBusinessLevel && InvestmentReserve >= GetUpgradeCost();

    public bool TryUpgrade()
    {
        if (!CanUpgrade()) return false;

        InvestmentReserve -= GetUpgradeCost();
        BusinessLevel++;
        ProductQuality = Math.Clamp(ProductQuality + 0.08f, 0.5f, 2f);
        BaseOutput *= 1.06f;
        return true;
    }

    public void ResetTickAccounting()
    {
        RevenueThisTick = 0f;
        ExpensesThisTick = 0f;
        LastProducedUnits = 0f;
        LastSoldUnits = 0f;
        LastSalesRevenue = 0f;
        LastLocalSalesRevenue = 0f;
        LastExternalSalesRevenue = 0f;
        LastInvestment = 0f;
    }

    public void RecordRevenue(float amount)
    {
        amount = Math.Max(0f, amount);
        Cash += amount;
        Revenue += amount;
        TotalRevenue += amount;
        RevenueThisTick += amount;
    }

    public void RecordExpense(float amount)
    {
        amount = Math.Max(0f, amount);
        Cash -= amount;
        Expenses += amount;
        TotalExpenses += amount;
        ExpensesThisTick += amount;
    }

    public void Invest(float amount)
    {
        amount = Math.Max(0f, amount);
        if (amount <= 0f) return;

        Cash -= amount;
        InvestmentReserve += amount;
        LastInvestment += amount;
    }
}

public enum BusinessStatus
{
    Active,
    Bankrupt,
    Closed
}
