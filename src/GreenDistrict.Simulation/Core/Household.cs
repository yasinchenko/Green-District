using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenDistrict.Simulation.Core;

/// <summary>
/// Represents a family or shared home.
/// </summary>
public class Household
{
    private static int _nextId = 1;

    public int Id { get; }
    public int? DistrictId { get; set; }
    public int? HousingUnitId { get; set; }
    public int HousingCapacity { get; set; }
    public float RentPerTick { get; set; }
    public float TotalIncome { get; private set; }
    public float PerCapitaIncome => MemberCount == 0 ? 0f : TotalIncome / MemberCount;
    public List<int> MemberIds { get; } = new();

    public int MemberCount => MemberIds.Count;
    public bool HasHousing => HousingUnitId.HasValue || HousingCapacity > 0;
    public bool IsOvercrowded => HasHousing && HousingCapacity > 0 && MemberCount > HousingCapacity;

    public Household(int? districtId = null, int? housingUnitId = null, int housingCapacity = 0, float rentPerTick = 0f)
    {
        Id = _nextId++;
        DistrictId = districtId;
        HousingUnitId = housingUnitId;
        HousingCapacity = Math.Max(0, housingCapacity);
        RentPerTick = Math.Max(0f, rentPerTick);
    }

    public void RecalculateIncome(IEnumerable<Citizen> citizens)
    {
        if (citizens == null) throw new ArgumentNullException(nameof(citizens));

        TotalIncome = citizens
            .Where(c => MemberIds.Contains(c.Id))
            .Sum(c => c.Income);
    }
}
