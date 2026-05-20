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
    public float AvailableMoney { get; private set; }
    public float MandatoryExpenses { get; private set; }
    public float DiscretionarySpending { get; private set; }
    public float Savings { get; private set; }
    public float PerCapitaIncome => MemberCount == 0 ? 0f : TotalIncome / MemberCount;
    public List<int> MemberIds { get; } = new();

    public int MemberCount => MemberIds.Count;
    public bool HasHousing => HousingUnitId.HasValue || HousingCapacity > 0;
    public bool IsOvercrowded => HasHousing && HousingCapacity > 0 && MemberCount > HousingCapacity;

    public Household(int? districtId = null, int? housingUnitId = null, int housingCapacity = 0, float rentPerTick = 0f, int? id = null)
    {
        Id = id ?? _nextId++;
        if (id.HasValue && id.Value >= _nextId)
        {
            _nextId = id.Value + 1;
        }

        DistrictId = districtId;
        HousingUnitId = housingUnitId;
        HousingCapacity = Math.Max(0, housingCapacity);
        RentPerTick = Math.Max(0f, rentPerTick);
    }

    public void RecalculateIncome(IEnumerable<Citizen> citizens)
    {
        if (citizens == null) throw new ArgumentNullException(nameof(citizens));

        var members = citizens
            .Where(c => MemberIds.Contains(c.Id))
            .ToList();

        TotalIncome = members.Sum(c => c.Income);
        AvailableMoney = members.Sum(c => Math.Max(0f, c.Cash));
        MandatoryExpenses = Math.Max(0f, RentPerTick);

        var afterMandatory = Math.Max(0f, AvailableMoney - MandatoryExpenses);
        DiscretionarySpending = afterMandatory * 0.35f;
        Savings = Math.Max(0f, afterMandatory - DiscretionarySpending);
    }
}
