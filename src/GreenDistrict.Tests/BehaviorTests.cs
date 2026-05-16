using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Behavior;

public class BehaviorTests
{
    [Fact]
    public void Retirement_Sets_IsRetired_And_Clears_Job()
    {
        var world = new WorldState();
        var c = new Citizen("Old", 65, "Worker") { Job = "Shop" };
        world.Citizens.Add(c);
        var behavior = new BehaviorSystem(retirementAge: 65);
        behavior.UpdateTick(world);
        Assert.True(c.IsRetired);
        Assert.Null(c.Job);
        Assert.Equal("Retired", c.Profession);
    }

    [Fact]
    public void LowSatisfaction_Causes_Quit()
    {
        var world = new WorldState();
        var biz = new Business("Shop", "shop", 5);
        world.Businesses.Add(biz);
        var c = new Citizen("Worker", 30, "Worker") { Job = "Shop", Satisfaction = 10f };
        world.Citizens.Add(c);
        biz.EmployeeIds.Add(c.Id);
        var behavior = new BehaviorSystem(quitSatisfactionThreshold: 20f);
        behavior.UpdateTick(world);
        Assert.Null(c.Job);
        Assert.DoesNotContain(c.Id, biz.EmployeeIds);
    }
}
