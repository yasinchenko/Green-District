using System;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a dwelling that can host one household.
/// </summary>
public class HousingUnit
{
    public int Id { get; set; }
    public int? DistrictId { get; set; }
    public int Capacity { get; set; }
    public float RentPerTick { get; set; }
    public int? HouseholdId { get; set; }

    public bool IsOccupied => HouseholdId.HasValue;

    public HousingUnit(int id, int? districtId, int capacity, float rentPerTick = 0f)
    {
        Id = id;
        DistrictId = districtId;
        Capacity = Math.Max(0, capacity);
        RentPerTick = Math.Max(0f, rentPerTick);
    }
}
