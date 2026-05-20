using System;

namespace GreenDistrict.Simulation.Core;

public class GovernmentProject
{
    private static int _nextId = 1;
    public int Id { get; }
    public string Name { get; set; }
    public float Cost { get; set; }
    public int DurationTicks { get; set; }
    public int RemainingTicks { get; set; }
    public ProjectType Type { get; set; }
    public float Benefit { get; set; } // optional one-time budget benefit upon completion
    public int? DistrictId { get; set; }
    public float FoodSatisfactionEffect { get; set; }
    public float HousingSatisfactionEffect { get; set; }
    public float SafetySatisfactionEffect { get; set; }
    public float HealthcareSatisfactionEffect { get; set; }
    public float EntertainmentSatisfactionEffect { get; set; }
    public float SupportEffect { get; set; }
    public int HousingUnitsToCreate { get; set; }
    public int HousingUnitCapacity { get; set; }
    public float HousingUnitRentPerTick { get; set; }
    public float LocalCostPaid { get; set; }
    public float ExternalCostPaid { get; set; }
    public bool Completed { get; set; }
    public long StartTick { get; set; }

    public GovernmentProject(string name, float cost, int durationTicks, float benefit = 0f, int? id = null)
    {
        Id = id ?? _nextId++;
        if (id.HasValue && id.Value >= _nextId)
        {
            _nextId = id.Value + 1;
        }

        Name = name;
        Cost = cost;
        DurationTicks = durationTicks;
        RemainingTicks = durationTicks;
        Type = ProjectType.Custom;
        Benefit = benefit;
        Completed = false;
        StartTick = -1;
    }

    public static GovernmentProject CreateTyped(ProjectType type, int? districtId = null)
    {
        var project = type switch
        {
            ProjectType.Road => new GovernmentProject("Road Repair", 1200f, 20)
            {
                SafetySatisfactionEffect = 3f,
                EntertainmentSatisfactionEffect = 2f,
                SupportEffect = 1f
            },
            ProjectType.Clinic => new GovernmentProject("Community Clinic", 2500f, 35)
            {
                HealthcareSatisfactionEffect = 14f,
                SupportEffect = 2f
            },
            ProjectType.School => new GovernmentProject("Public School", 3000f, 45)
            {
                HealthcareSatisfactionEffect = 4f,
                EntertainmentSatisfactionEffect = 6f,
                SupportEffect = 2f
            },
            ProjectType.Police => new GovernmentProject("Police Station", 2200f, 30)
            {
                SafetySatisfactionEffect = 15f,
                SupportEffect = 1.5f
            },
            ProjectType.Housing => new GovernmentProject("Affordable Housing", 3500f, 50)
            {
                HousingSatisfactionEffect = 10f,
                SupportEffect = 2f,
                HousingUnitsToCreate = 3,
                HousingUnitCapacity = 3,
                HousingUnitRentPerTick = 18f
            },
            ProjectType.Park => new GovernmentProject("Public Park", 1400f, 25)
            {
                EntertainmentSatisfactionEffect = 12f,
                HealthcareSatisfactionEffect = 3f,
                SupportEffect = 1f
            },
            _ => new GovernmentProject("Custom Project", 1000f, 10)
        };

        project.Type = type;
        project.DistrictId = districtId;
        return project;
    }
}

public enum ProjectType
{
    Custom,
    Road,
    Clinic,
    School,
    Police,
    Housing,
    Park
}
