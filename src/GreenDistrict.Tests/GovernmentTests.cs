using Xunit;
using System;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Government;

namespace GreenDistrict.Tests;

public class GovernmentTests
{
    [Fact]
    public void StartProject_Deducts_Budget_And_Adds_Project()
    {
        var world = new WorldState();
        var govt = new GovernmentSystem();
        var project = new GovernmentProject("Road", 2000f, 2, benefit: 500f);

        var started = govt.StartProject(world, project);
        Assert.True(started);
        Assert.Contains(project, world.Projects);
        Assert.Equal(8000f, world.Budget); // 10000 - 2000
        Assert.Equal(2000f, world.LastProjectSpending);
        Assert.Equal(2000f, world.LastExternalOutflow);
        Assert.Equal(-2000f, world.LastNetBudgetChange);
    }

    [Fact]
    public void TickProjects_Completes_And_Applies_Benefit()
    {
        var world = new WorldState();
        var govt = new GovernmentSystem();
        var project = new GovernmentProject("Bridge", 1000f, 2, benefit: 300f);

        govt.StartProject(world, project);
        // tick 1
        world.Clock.Tick();
        govt.TickProjects(world);
        Assert.False(project.Completed);

        // tick 2
        world.Clock.Tick();
        govt.TickProjects(world);
        Assert.True(project.Completed);
        Assert.Equal(10000f - 1000f + 300f, world.Budget);
        Assert.Equal(300f, world.LastProjectBenefits);
        Assert.Equal(300f, world.LastExternalInflow);
        Assert.Equal(-700f, world.LastNetBudgetChange);
        Assert.NotEmpty(world.Events);
    }

    [Fact]
    public void TickProjects_Completes_And_Applies_District_Effects()
    {
        var world = new WorldState();
        world.Districts.Add(new District("North") { Id = 1 });
        world.Districts.Add(new District("South") { Id = 2 });

        var northCitizen = new Citizen("North Resident", 30, "Worker")
        {
            DistrictId = 1,
            FoodSatisfaction = 50f,
            HousingSatisfaction = 50f,
            SafetySatisfaction = 40f,
            HealthcareSatisfaction = 30f,
            EntertainmentSatisfaction = 20f
        };
        var southCitizen = new Citizen("South Resident", 30, "Worker")
        {
            DistrictId = 2,
            FoodSatisfaction = 50f,
            HousingSatisfaction = 50f,
            SafetySatisfaction = 40f,
            HealthcareSatisfaction = 30f,
            EntertainmentSatisfaction = 20f
        };
        world.Citizens.Add(northCitizen);
        world.Citizens.Add(southCitizen);

        var govt = new GovernmentSystem();
        var project = new GovernmentProject("Clinic Upgrade", 1000f, 1)
        {
            DistrictId = 1,
            HealthcareSatisfactionEffect = 15f,
            SafetySatisfactionEffect = 5f,
            SupportEffect = 2f
        };

        govt.StartProject(world, project);
        world.Clock.Tick();
        govt.TickProjects(world);

        Assert.True(project.Completed);
        Assert.Equal(45f, northCitizen.SafetySatisfaction);
        Assert.Equal(45f, northCitizen.HealthcareSatisfaction);
        Assert.Equal(40f, southCitizen.SafetySatisfaction);
        Assert.Equal(30f, southCitizen.HealthcareSatisfaction);
        Assert.Equal(77f, world.SupportRating);
        Assert.Equal(45f, world.Districts[0].AverageSafetySatisfaction);
        Assert.Equal(45f, world.Districts[0].AverageHealthcareSatisfaction);
        Assert.Equal(37.5f, world.Districts[0].ServiceLevel);
    }

    [Fact]
    public void CreateTyped_Returns_Configured_Project_Types()
    {
        var projectTypes = new[]
        {
            ProjectType.Road,
            ProjectType.Clinic,
            ProjectType.School,
            ProjectType.Police,
            ProjectType.Housing,
            ProjectType.Park
        };

        foreach (var type in projectTypes)
        {
            var project = GovernmentProject.CreateTyped(type, districtId: 5);
            var effectTotal =
                Math.Abs(project.FoodSatisfactionEffect) +
                Math.Abs(project.HousingSatisfactionEffect) +
                Math.Abs(project.SafetySatisfactionEffect) +
                Math.Abs(project.HealthcareSatisfactionEffect) +
                Math.Abs(project.EntertainmentSatisfactionEffect) +
                Math.Abs(project.SupportEffect) +
                project.HousingUnitsToCreate;

            Assert.Equal(type, project.Type);
            Assert.Equal(5, project.DistrictId);
            Assert.NotEqual(string.Empty, project.Name);
            Assert.True(project.Cost > 0f);
            Assert.True(project.DurationTicks > 0);
            Assert.True(effectTotal > 0f);
        }
    }

    [Fact]
    public void Housing_Project_Creates_HousingUnits_On_Completion()
    {
        var world = new WorldState();
        world.Districts.Add(new District("North") { Id = 1 });
        world.AddHousingUnit(10, 1, 2, 25f);

        var govt = new GovernmentSystem();
        var project = GovernmentProject.CreateTyped(ProjectType.Housing, districtId: 1);
        project.DurationTicks = 1;

        govt.StartProject(world, project);
        world.Clock.Tick();
        govt.TickProjects(world);

        var createdUnits = world.HousingUnits.Where(h => h.Id >= 11).ToList();
        Assert.True(project.Completed);
        Assert.Equal(3, createdUnits.Count);
        Assert.All(createdUnits, h =>
        {
            Assert.Equal(1, h.DistrictId);
            Assert.Equal(3, h.Capacity);
            Assert.Equal(18f, h.RentPerTick);
            Assert.False(h.IsOccupied);
        });
        Assert.Equal(11, world.Districts[0].HousingCapacity);
        Assert.Equal(4, world.Districts[0].AvailableHousing);
    }

    [Fact]
    public void CancelProject_Refunds_Portion()
    {
        var world = new WorldState();
        var govt = new GovernmentSystem();
        var project = new GovernmentProject("Park", 500f, 5, benefit: 0f);

        govt.StartProject(world, project);
        var refund = govt.CancelProject(world, project.Id);

        Assert.Equal(250f, refund);
        Assert.Equal(500f, world.LastProjectSpending);
        Assert.Equal(250f, world.LastProjectRefunds);
        Assert.Equal(250f, world.LastExternalInflow);
        Assert.Equal(500f, world.LastExternalOutflow);
        Assert.Equal(-250f, world.LastNetBudgetChange);
        Assert.DoesNotContain(project, world.Projects);
    }
}
