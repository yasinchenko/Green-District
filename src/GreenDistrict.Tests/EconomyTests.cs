using Xunit;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;
using System;
using System.IO;

namespace GreenDistrict.Tests;

public class EconomyTests
{
    [Fact]
    public void BusinessTypeCatalog_Loads_Data_File()
    {
        var root = GetRepositoryRoot();
        var catalog = BusinessTypeCatalog.LoadJsonFile(Path.Combine(root, "data", "businesses", "business_types.json"));

        Assert.True(catalog.TryGet("farm", out var farm));
        Assert.Equal("food", farm.ProductionType);
        Assert.True(farm.BaseOutput > 0f);
        Assert.True(farm.UnitPrice > 0f);
    }

    [Fact]
    public void ProfessionCatalog_Loads_Data_File_And_Finds_By_Id_Or_Name()
    {
        var root = GetRepositoryRoot();
        var catalog = ProfessionCatalog.LoadJsonFile(Path.Combine(root, "data", "jobs", "professions.json"));

        Assert.True(catalog.TryGet("doctor", out var doctorById));
        Assert.Equal(1200f, doctorById.BaseWage);
        Assert.True(catalog.TryGet("Factory Worker", out var workerByName));
        Assert.Equal(500f, workerByName.BaseWage);
    }

    [Fact]
    public void ApplyBusinessTypes_Copies_Production_Settings_To_Businesses()
    {
        var world = new WorldState();
        var business = new Business("Farm", "farm", 0);
        world.Businesses.Add(business);
        var catalog = new BusinessTypeCatalog(new[]
        {
            new BusinessTypeDefinition
            {
                Id = "farm",
                MaxEmployees = 10,
                ProductionType = "food",
                BaseOutput = 500f,
                UnitPrice = 2f,
                DemandMultiplier = 0.8f
            }
        });

        var economy = new EconomySystem();
        economy.ApplyBusinessTypes(world, catalog);

        Assert.Equal(10, business.MaxEmployees);
        Assert.Equal("food", business.ProductionType);
        Assert.Equal(500f, business.BaseOutput);
        Assert.Equal(2f, business.UnitPrice);
        Assert.Equal(0.8f, business.DemandMultiplier);
    }

    [Fact]
    public void ProcessProductionAndSales_Adds_Revenue_From_Staffed_Output()
    {
        var world = new WorldState();
        var business = new Business("Factory", "factory", 4)
        {
            BaseOutput = 100f,
            UnitPrice = 3f,
            DemandMultiplier = 0.5f,
            Revenue = 10f
        };
        world.Businesses.Add(business);
        var c1 = new Citizen("A", 30, "Worker") { Job = business.Name };
        var c2 = new Citizen("B", 30, "Worker") { Job = business.Name };
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);
        business.EmployeeIds.Add(c1.Id);
        business.EmployeeIds.Add(c2.Id);

        var economy = new EconomySystem();
        economy.ProcessProductionAndSales(world);

        Assert.Equal(50f, business.LastProducedUnits);
        Assert.Equal(25f, business.LastSoldUnits);
        Assert.Equal(75f, business.LastSalesRevenue);
        Assert.Equal(85f, business.Revenue);
    }

    [Fact]
    public void ProcessProductionAndSales_Skips_NonActive_Businesses()
    {
        var world = new WorldState();
        var business = new Business("Factory", "factory", 1)
        {
            BaseOutput = 100f,
            UnitPrice = 3f,
            DemandMultiplier = 1f,
            Revenue = 10f,
            Status = BusinessStatus.Bankrupt
        };
        business.EmployeeIds.Add(1);
        world.Businesses.Add(business);

        var economy = new EconomySystem();
        economy.ProcessProductionAndSales(world);

        Assert.Equal(0f, business.LastProducedUnits);
        Assert.Equal(10f, business.Revenue);
    }

    [Fact]
    public void AssignJobs_Fills_Vacancies()
    {
        var world = new WorldState();
        var business = new Business("Shop", "shop", 2) { Id = 1 };
        world.Businesses.Add(business);

        var c1 = new Citizen("A", 30, "Worker");
        var c2 = new Citizen("B", 25, "Worker");
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);

        var eco = new EconomySystem();
        var assigned = eco.AssignJobs(world);

        Assert.Equal(2, assigned);
        Assert.Equal(2, business.EmployeeIds.Count);
        Assert.Equal("Shop", c1.Job);
        Assert.Equal(EmploymentStatus.Employed, c1.EmploymentStatus);
    }

    [Fact]
    public void AssignJobs_Skips_Children_And_Retired_Citizens()
    {
        var world = new WorldState();
        var business = new Business("Shop", "shop", 3) { Id = 1 };
        world.Businesses.Add(business);

        var child = new Citizen("Child", 12, "Student");
        var retired = new Citizen("Retired", 70, "Retired") { IsRetired = true };
        var adult = new Citizen("Adult", 30, "Worker");
        world.Citizens.Add(child);
        world.Citizens.Add(retired);
        world.Citizens.Add(adult);

        var eco = new EconomySystem();
        var assigned = eco.AssignJobs(world);

        Assert.Equal(1, assigned);
        Assert.Null(child.Job);
        Assert.Null(retired.Job);
        Assert.Equal("Shop", adult.Job);
        Assert.Equal(EmploymentStatus.Student, child.EmploymentStatus);
        Assert.Equal(EmploymentStatus.Retired, retired.EmploymentStatus);
        Assert.Equal(EmploymentStatus.Employed, adult.EmploymentStatus);
        Assert.Single(business.EmployeeIds);
        Assert.Contains(adult.Id, business.EmployeeIds);
    }

    [Fact]
    public void AssignJobs_Skips_Closed_Businesses()
    {
        var world = new WorldState();
        var business = new Business("Closed Shop", "shop", 3) { Status = BusinessStatus.Closed };
        var adult = new Citizen("Adult", 30, "Worker");
        world.Businesses.Add(business);
        world.Citizens.Add(adult);

        var assigned = new EconomySystem().AssignJobs(world);

        Assert.Equal(0, assigned);
        Assert.Null(adult.Job);
        Assert.Empty(business.EmployeeIds);
    }

    [Fact]
    public void ProcessPayroll_Pays_Wages_And_Taxes()
    {
        var world = new WorldState { IncomeTaxRate = 0.1f };
        var business = new Business("Farm", "farm", 1) { Id = 1, WagePerEmployee = 500f, Revenue = 1000f };
        world.Businesses.Add(business);

        var c1 = new Citizen("A", 30, "Farmer");
        world.Citizens.Add(c1);
        var household = world.CreateHousehold(1, new[] { c1 });

        // assign manually
        business.EmployeeIds.Add(c1.Id);
        c1.Job = business.Name;

        var eco = new EconomySystem(taxRate: 0.1f);
        eco.ProcessPayroll(world);

        // gross 500, tax 50, net 450
        Assert.Equal(450f, c1.Income);
        Assert.Equal(500f, business.Revenue); // 1000-500 (business paid gross wages)
        // Default starting budget is 10000, tax 50 added -> 10050
        Assert.Equal(10050f, world.Budget);
        Assert.Equal(50f, world.LastIncomeTaxCollected);
        Assert.Equal(50f, world.LastNetBudgetChange);

        world.RecalculateHouseholds();
        Assert.Equal(450f, household.TotalIncome);
        Assert.Equal(450f, household.PerCapitaIncome);
    }

    [Fact]
    public void ProcessPayroll_Uses_Profession_BaseWage_When_Catalog_Provided()
    {
        var world = new WorldState { IncomeTaxRate = 0.1f };
        var business = new Business("Clinic", "clinic", 1) { Id = 1, WagePerEmployee = 500f, Revenue = 2000f };
        var doctor = new Citizen("A", 30, "Doctor") { Job = business.Name };
        world.Businesses.Add(business);
        world.Citizens.Add(doctor);
        business.EmployeeIds.Add(doctor.Id);
        var catalog = new ProfessionCatalog(new[]
        {
            new ProfessionDefinition { Id = "doctor", Name = "Doctor", BaseWage = 1200f, SkillLevel = 3 }
        });

        var eco = new EconomySystem(taxRate: 0.1f);
        eco.ProcessPayroll(world, catalog);

        Assert.Equal(1080f, doctor.Income);
        Assert.Equal(800f, business.Revenue);
        Assert.Equal(1200f, business.Expenses);
        Assert.Equal(10120f, world.Budget);
    }

    [Fact]
    public void ProcessPayroll_Falls_Back_To_Business_Wage_When_Profession_Missing()
    {
        var world = new WorldState { IncomeTaxRate = 0.1f };
        var business = new Business("Workshop", "factory", 1) { Id = 1, WagePerEmployee = 500f, Revenue = 1000f };
        var worker = new Citizen("A", 30, "UnknownRole") { Job = business.Name };
        world.Businesses.Add(business);
        world.Citizens.Add(worker);
        business.EmployeeIds.Add(worker.Id);
        var catalog = new ProfessionCatalog(Array.Empty<ProfessionDefinition>());

        var eco = new EconomySystem(taxRate: 0.1f);
        eco.ProcessPayroll(world, catalog);

        Assert.Equal(450f, worker.Income);
        Assert.Equal(500f, business.Revenue);
    }

    [Fact]
    public void UpdateBusinessViability_Closes_Business_After_Consecutive_Losses()
    {
        var world = new WorldState();
        var business = new Business("Weak Shop", "shop", 2)
        {
            Revenue = 100f,
            Expenses = 500f
        };
        var worker = new Citizen("Worker", 30, "Worker") { Job = business.Name };
        world.Businesses.Add(business);
        world.Citizens.Add(worker);
        business.EmployeeIds.Add(worker.Id);
        business.EmployeeCount = 1;

        var economy = new EconomySystem();
        var first = economy.UpdateBusinessViability(world, lossTicksToBankruptcy: 2);
        var second = economy.UpdateBusinessViability(world, lossTicksToBankruptcy: 2);

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal(BusinessStatus.Bankrupt, business.Status);
        Assert.Equal(world.Clock.CurrentTick, business.ClosedAtTick);
        Assert.Null(worker.Job);
        Assert.Empty(business.EmployeeIds);
        Assert.Single(world.Events);
    }

    [Fact]
    public void UpdateBusinessViability_Resets_Loss_Counter_When_Profitable()
    {
        var world = new WorldState();
        var business = new Business("Shop", "shop", 2) { Revenue = 100f, Expenses = 500f };
        world.Businesses.Add(business);
        var economy = new EconomySystem();

        economy.UpdateBusinessViability(world, lossTicksToBankruptcy: 2);
        business.Revenue = 1000f;
        economy.UpdateBusinessViability(world, lossTicksToBankruptcy: 2);

        Assert.Equal(BusinessStatus.Active, business.Status);
        Assert.Equal(0, business.ConsecutiveLossTicks);
    }

    [Fact]
    public void TryOpenBusiness_Creates_Business_When_Economy_Allows()
    {
        var world = new WorldState { Budget = 20000f };
        var employed = new Citizen("A", 30, "Worker") { Job = "Existing" };
        var existing = new Business("Existing", "shop", 1) { Id = 5 };
        world.Citizens.Add(employed);
        world.Businesses.Add(existing);
        existing.EmployeeIds.Add(employed.Id);
        var catalog = new BusinessTypeCatalog(new[]
        {
            new BusinessTypeDefinition
            {
                Id = "shop",
                Name = "Shop",
                MaxEmployees = 5,
                ProductionType = "trade",
                BaseOutput = 300f,
                UnitPrice = 5f,
                DemandMultiplier = 0.9f
            }
        });

        var created = new EconomySystem().TryOpenBusiness(world, catalog, "shop", districtId: 2, minimumBudget: 10000f, maximumUnemploymentRate: 10f);

        Assert.NotNull(created);
        Assert.Equal(6, created.Id);
        Assert.Equal("Shop 6", created.Name);
        Assert.Equal(BusinessStatus.Active, created.Status);
        Assert.Equal(2, created.DistrictId);
        Assert.Equal(300f, created.BaseOutput);
        Assert.Contains(created, world.Businesses);
    }

    [Fact]
    public void TryOpenBusiness_Returns_Null_When_Budget_Too_Low()
    {
        var world = new WorldState { Budget = 100f };
        var catalog = new BusinessTypeCatalog(new[]
        {
            new BusinessTypeDefinition { Id = "shop", Name = "Shop", MaxEmployees = 5 }
        });

        var created = new EconomySystem().TryOpenBusiness(world, catalog, "shop", minimumBudget: 10000f);

        Assert.Null(created);
    }

    [Fact]
    public void ProcessPayroll_Removes_Stale_Or_Ineligible_EmployeeIds()
    {
        var world = new WorldState { IncomeTaxRate = 0.1f };
        var business = new Business("Farm", "farm", 5) { Id = 1, WagePerEmployee = 500f, Revenue = 1000f };
        world.Businesses.Add(business);

        var retired = new Citizen("Old", 70, "Retired") { Job = business.Name, IsRetired = true };
        var wrongJob = new Citizen("Mismatch", 30, "Worker") { Job = "Shop" };
        var valid = new Citizen("Valid", 30, "Farmer") { Job = business.Name };
        world.Citizens.Add(retired);
        world.Citizens.Add(wrongJob);
        world.Citizens.Add(valid);

        business.EmployeeIds.Add(retired.Id);
        business.EmployeeIds.Add(wrongJob.Id);
        business.EmployeeIds.Add(valid.Id);
        business.EmployeeIds.Add(9999);

        var eco = new EconomySystem(taxRate: 0.1f);
        eco.ProcessPayroll(world);

        Assert.Equal(new[] { valid.Id }, business.EmployeeIds);
        Assert.Equal(1, business.EmployeeCount);
        Assert.Null(retired.Job);
        Assert.Equal(EmploymentStatus.Retired, retired.EmploymentStatus);
        Assert.Equal("Shop", wrongJob.Job);
        Assert.Equal(450f, valid.Income);
    }

    [Fact]
    public void ProcessBusinessTaxes_Taxes_Positive_Profit()
    {
        var world = new WorldState { BusinessTaxRate = 0.2f };
        var profitable = new Business("Shop", "shop", 1) { Revenue = 1000f, Expenses = 400f };
        var loss = new Business("Farm", "farm", 1) { Revenue = 100f, Expenses = 500f };
        world.Businesses.Add(profitable);
        world.Businesses.Add(loss);

        var collected = new EconomySystem().ProcessBusinessTaxes(world);

        Assert.Equal(120f, collected);
        Assert.Equal(120f, world.LastBusinessTaxCollected);
        Assert.Equal(10120f, world.Budget);
        Assert.Equal(880f, profitable.Revenue);
        Assert.Equal(520f, profitable.Expenses);
        Assert.Equal(100f, loss.Revenue);
        Assert.Equal(500f, loss.Expenses);
    }

    [Fact]
    public void ProcessGovernmentExpenses_Subtracts_Base_And_Project_Operating_Costs()
    {
        var world = new WorldState
        {
            Budget = 1000f,
            BaseOperatingExpensePerTick = 25f,
            ProjectOperatingExpensePerTick = 10f
        };
        world.Projects.Add(new GovernmentProject("Road", 100f, 10));
        world.Projects.Add(new GovernmentProject("Done", 100f, 10) { Completed = true });

        var expenses = new EconomySystem().ProcessGovernmentExpenses(world);

        Assert.Equal(35f, expenses);
        Assert.Equal(35f, world.LastOperatingExpenses);
        Assert.Equal(965f, world.Budget);
    }

    [Fact]
    public void GetUnemploymentRate_Computes_Correctly()
    {
        var world = new WorldState();
        var c1 = new Citizen("A", 30, "Worker") { Job = "Shop" };
        var c2 = new Citizen("B", 25, "Worker");
        world.Citizens.Add(c1);
        world.Citizens.Add(c2);

        var eco = new EconomySystem();
        var rate = eco.GetUnemploymentRate(world);

        Assert.Equal(50f, rate);
    }

    [Fact]
    public void GetUnemploymentRate_Ignores_Non_Working_Age_Citizens()
    {
        var world = new WorldState();
        world.Citizens.Add(new Citizen("Child", 10, "Student"));
        world.Citizens.Add(new Citizen("Retired", 70, "Retired") { IsRetired = true });
        world.Citizens.Add(new Citizen("Employed", 30, "Worker") { Job = "Shop" });
        world.Citizens.Add(new Citizen("Unemployed", 25, "Worker"));

        var eco = new EconomySystem();
        var rate = eco.GetUnemploymentRate(world);

        Assert.Equal(50f, rate);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Codex_plan.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
