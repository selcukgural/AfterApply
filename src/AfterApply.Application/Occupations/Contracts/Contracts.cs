namespace AfterApply.Application.Occupations.Contracts;

public sealed record SearchOccupationsQuery(string Q = "");

/// <summary>Both names always: the web shows the one for its language and the other underneath,
/// so a reader who searched in English still sees what the Turkish screen will call it.</summary>
public sealed record OccupationSearchResultResponse(Guid Id, string Code, string NameTr, string NameEn);

/// <summary>An occupation as it rides on a salary row — the same shape on every response.</summary>
public sealed record OccupationRefResponse(Guid Id, string Code, string NameTr, string NameEn);
