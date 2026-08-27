namespace AfterApply.Application.Companies.Contracts;

public sealed record CompanySearchResultResponse(Guid Id, string Name, string? Website);
