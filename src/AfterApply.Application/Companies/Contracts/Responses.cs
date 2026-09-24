namespace AfterApply.Application.Companies.Contracts;

public sealed record CompanySearchResultResponse(Guid Id, string Name, string? Website);

/// <summary>Enough to name a company and link its page — no profile fields.</summary>
public sealed record CompanyReferenceResponse(Guid Id, string Slug, string Name);
