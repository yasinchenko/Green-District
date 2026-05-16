using Xunit;
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
        Assert.NotEmpty(world.Events);
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
        Assert.DoesNotContain(project, world.Projects);
    }
}
