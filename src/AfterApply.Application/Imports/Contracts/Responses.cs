using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;

namespace AfterApply.Application.Imports.Contracts;

public sealed record ImportRowErrorResponse(int RowNumber, string RawRow, string ErrorMessage);

public sealed record ImportAcceptedResponse(Guid Id);

public sealed record ImportSummaryResponse(
    Guid Id,
    Source Source,
    string FileName,
    ImportBatchStatus Status,
    int ProcessedRows,
    int? TotalRows,
    int TotalRecords,
    int NewApplications,
    int DuplicateRecords,
    int InvalidRecords,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    IReadOnlyCollection<ImportRowErrorResponse> Errors);
