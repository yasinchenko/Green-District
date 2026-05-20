using System;
using System.Collections.Generic;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Economy;

public enum EconomyTrend
{
    Growing,
    Stagnant,
    Shrinking
}

public enum EconomyDiagnosisReason
{
    Balanced,
    ExternalInflow,
    LocalSpending,
    UnmetDemand,
    ImportLeakage,
    BusinessRisk,
    Unemployment,
    PublicDeficit,
    LowCash
}

public enum EconomyDiagnosisSeverity
{
    Info,
    Warning,
    Critical,
    Positive
}

public sealed record EconomyDiagnosisFactor(
    EconomyDiagnosisReason Reason,
    EconomyDiagnosisSeverity Severity,
    float Value,
    string Detail);

public sealed record EconomyDiagnosis(
    EconomyTrend Trend,
    EconomyDiagnosisReason PrimaryReason,
    float NetExternalFlow,
    float LocalConsumerSpending,
    float UnmetDemand,
    float BusinessCash,
    float CitizenCash,
    int AtRiskBusinesses,
    int ClosedBusinesses,
    int UnemployedWorkers,
    IReadOnlyList<EconomyDiagnosisFactor> Factors)
{
    public static EconomyDiagnosis Analyze(WorldState world, int? districtId = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        var activeBusinesses = world.Businesses
            .Where(business => business.Status == BusinessStatus.Active)
            .Where(business => !districtId.HasValue || business.DistrictId == districtId.Value)
            .ToList();
        var allBusinesses = world.Businesses
            .Where(business => !districtId.HasValue || business.DistrictId == districtId.Value)
            .ToList();
        var citizens = world.Citizens
            .Where(citizen => !districtId.HasValue || citizen.DistrictId == districtId.Value)
            .ToList();
        var demand = world.Economy.EstimateConsumerDemand(world, districtId);

        var netExternalFlow = world.LastExternalInflow - world.LastExternalOutflow;
        var localConsumerSpending = world.LastConsumerSpending;
        var unmetDemand = demand.Sum(item => item.UnmetDemand);
        var businessCash = activeBusinesses.Sum(business => business.Cash);
        var citizenCash = citizens.Sum(citizen => Math.Max(0f, citizen.Cash));
        var atRiskBusinesses = activeBusinesses.Count(IsBusinessAtRisk);
        var closedBusinesses = allBusinesses.Count(business => business.Status != BusinessStatus.Active);
        var unemployedWorkers = citizens.Count(citizen => citizen.EmploymentStatus == EmploymentStatus.Unemployed);

        var factors = BuildFactors(
            world,
            netExternalFlow,
            localConsumerSpending,
            unmetDemand,
            businessCash,
            citizenCash,
            atRiskBusinesses,
            closedBusinesses,
            unemployedWorkers).ToList();

        var primary = factors
            .OrderByDescending(FactorPriority)
            .ThenByDescending(factor => Math.Abs(factor.Value))
            .FirstOrDefault();

        var trend = DetermineTrend(
            world,
            netExternalFlow,
            localConsumerSpending,
            unmetDemand,
            atRiskBusinesses,
            closedBusinesses);

        return new EconomyDiagnosis(
            trend,
            primary?.Reason ?? EconomyDiagnosisReason.Balanced,
            netExternalFlow,
            localConsumerSpending,
            unmetDemand,
            businessCash,
            citizenCash,
            atRiskBusinesses,
            closedBusinesses,
            unemployedWorkers,
            factors);
    }

    private static IEnumerable<EconomyDiagnosisFactor> BuildFactors(
        WorldState world,
        float netExternalFlow,
        float localConsumerSpending,
        float unmetDemand,
        float businessCash,
        float citizenCash,
        int atRiskBusinesses,
        int closedBusinesses,
        int unemployedWorkers)
    {
        if (closedBusinesses > 0)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.BusinessRisk,
                EconomyDiagnosisSeverity.Critical,
                closedBusinesses,
                "closed-businesses");
        }

        if (atRiskBusinesses > 0)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.BusinessRisk,
                EconomyDiagnosisSeverity.Warning,
                atRiskBusinesses,
                "at-risk-businesses");
        }

        if (netExternalFlow < -1f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.ImportLeakage,
                EconomyDiagnosisSeverity.Warning,
                netExternalFlow,
                "external-outflow");
        }

        if (world.LastNetBudgetChange < -1f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.PublicDeficit,
                EconomyDiagnosisSeverity.Warning,
                world.LastNetBudgetChange,
                "budget-deficit");
        }

        if (unmetDemand > 10f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.UnmetDemand,
                EconomyDiagnosisSeverity.Warning,
                unmetDemand,
                "unmet-demand");
        }

        if (unemployedWorkers > 0)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.Unemployment,
                EconomyDiagnosisSeverity.Warning,
                unemployedWorkers,
                "unemployed-workers");
        }

        if (businessCash < 0f || citizenCash < 1f && world.Citizens.Count > 0)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.LowCash,
                EconomyDiagnosisSeverity.Warning,
                businessCash + citizenCash,
                "low-liquid-cash");
        }

        if (netExternalFlow > 1f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.ExternalInflow,
                EconomyDiagnosisSeverity.Positive,
                netExternalFlow,
                "external-inflow");
        }

        if (localConsumerSpending > 1f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.LocalSpending,
                EconomyDiagnosisSeverity.Positive,
                localConsumerSpending,
                "consumer-spending");
        }

        if (Math.Abs(netExternalFlow) <= 1f && localConsumerSpending <= 1f && unmetDemand <= 10f)
        {
            yield return new EconomyDiagnosisFactor(
                EconomyDiagnosisReason.Balanced,
                EconomyDiagnosisSeverity.Info,
                0f,
                "balanced");
        }
    }

    private static EconomyTrend DetermineTrend(
        WorldState world,
        float netExternalFlow,
        float localConsumerSpending,
        float unmetDemand,
        int atRiskBusinesses,
        int closedBusinesses)
    {
        if (closedBusinesses > 0 ||
            atRiskBusinesses > 0 && netExternalFlow < 0f ||
            netExternalFlow < -Math.Max(25f, localConsumerSpending * 0.35f) ||
            world.LastNetBudgetChange < -500f)
        {
            return EconomyTrend.Shrinking;
        }

        if (netExternalFlow > 10f ||
            localConsumerSpending > 10f && unmetDemand <= localConsumerSpending * 1.5f ||
            world.LastBusinessTaxCollected + world.LastIncomeTaxCollected > 10f)
        {
            return EconomyTrend.Growing;
        }

        return EconomyTrend.Stagnant;
    }

    private static bool IsBusinessAtRisk(Business business)
    {
        var payrollReserve = Math.Max(0f, business.WagePerEmployee) * Math.Max(1, business.EmployeeIds.Count);
        return business.Cash < payrollReserve ||
               business.ConsecutiveLossTicks > 0 ||
               business.ProfitThisTick < 0f ||
               business.LastProducedUnits > 0.001f && business.LastSoldUnits <= 0.001f && business.RevenueThisTick <= 0.001f;
    }

    private static int FactorPriority(EconomyDiagnosisFactor factor)
    {
        return factor.Severity switch
        {
            EconomyDiagnosisSeverity.Critical => 4,
            EconomyDiagnosisSeverity.Warning => 3,
            EconomyDiagnosisSeverity.Positive => 2,
            _ => 1
        };
    }
}
