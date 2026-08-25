using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports.Contracts;

public sealed record ImportRowErrorResponse(int RowNumber, string RawRow, string ErrorMessage);

public sealed record ImportSummaryResponse(
    Guid Id,
    Source Source,
    string FileName,
    int TotalRecords,
    int NewApplications,
    int DuplicateRecords,
    int InvalidRecords,
    DateTimeOffset CompletedAt,
    IReadOnlyCollection<ImportRowErrorResponse> Errors);
