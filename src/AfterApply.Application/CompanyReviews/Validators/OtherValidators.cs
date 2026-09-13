using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.CompanyReviews;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.CompanyReviews.Validators;

public sealed class ResolveCompanyRequestValidator : AbstractValidator<ResolveCompanyRequest>
{
    public ResolveCompanyRequestValidator()
    {
        // Same width as Company.Name's column and the application form's rule.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
    }
}

public sealed class ReportCompanyReviewRequestValidator : AbstractValidator<ReportCompanyReviewRequest>
{
    public ReportCompanyReviewRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(CompanyReviewReport.MaxNoteLength);
        // "Other" with no explanation gives the admin nothing to act on.
        RuleFor(x => x.Note).NotEmpty()
            .When(x => x.Reason == ReviewReportReason.Other)
            .WithMessage(_ => localizer["VALIDATION_REPORT_NOTE_REQUIRED_FOR_OTHER"]);
    }
}

public sealed class RejectCompanyReviewRequestValidator : AbstractValidator<RejectCompanyReviewRequest>
{
    public RejectCompanyReviewRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().Length(3, CompanyReview.MaxRejectionReasonLength);
    }
}

public sealed class ResolveReviewReportRequestValidator : AbstractValidator<ResolveReviewReportRequest>
{
    public ResolveReviewReportRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Resolution).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(CompanyReviewReport.MaxResolutionReasonLength);
        RuleFor(x => x.Reason).NotEmpty()
            .When(x => x.Resolution != ReviewReportResolution.Dismissed)
            .WithMessage(_ => localizer["VALIDATION_RESOLUTION_REASON_REQUIRED"]);
    }
}

public sealed class SetReviewQuotaRequestValidator : AbstractValidator<SetReviewQuotaRequest>
{
    public SetReviewQuotaRequestValidator()
    {
        RuleFor(x => x.ReviewQuotaOverride).InclusiveBetween(0, 1000).When(x => x.ReviewQuotaOverride.HasValue);
    }
}

public sealed class PublicCompanyListQueryValidator : AbstractValidator<PublicCompanyListQuery>
{
    public PublicCompanyListQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class PublicReviewListQueryValidator : AbstractValidator<PublicReviewListQuery>
{
    public PublicReviewListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
        RuleFor(x => x.Sort).IsInEnum();
    }
}

public sealed class AdminReviewListQueryValidator : AbstractValidator<AdminReviewListQuery>
{
    public AdminReviewListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Company).MaximumLength(100);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From!.Value).When(x => x.From.HasValue && x.To.HasValue);
    }
}

public sealed class AdminReportListQueryValidator : AbstractValidator<AdminReportListQuery>
{
    public AdminReportListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
