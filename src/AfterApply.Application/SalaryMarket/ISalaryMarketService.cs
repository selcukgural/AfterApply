using AfterApply.Application.SalaryMarket.Contracts;

namespace AfterApply.Application.SalaryMarket;

/// <summary>
/// The public salary pages' data: survey figures compiled into the build (salary-market.vN
/// seed), so reading them is a lookup in memory, not a query.
/// </summary>
public interface ISalaryMarketService
{
    SalaryOccupationsResponse GetOccupations();

    /// <summary>Null for an unknown slug, and for a known one with no year at or above the threshold.</summary>
    SalaryOccupationResponse? GetOccupation(string slug);
}
