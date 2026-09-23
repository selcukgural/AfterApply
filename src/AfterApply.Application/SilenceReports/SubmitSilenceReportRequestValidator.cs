using FluentValidation;

namespace AfterApply.Application.SilenceReports;

public sealed class SubmitSilenceReportRequestValidator : AbstractValidator<SubmitSilenceReportRequest>
{
    public SubmitSilenceReportRequestValidator()
    {
        RuleFor(r => r.Stage).NotNull().IsInEnum();
        RuleFor(r => r.Wait).NotNull().IsInEnum();
        RuleFor(r => r.Source).IsInEnum().When(r => r.Source.HasValue);
        RuleFor(r => r.Locale).NotEmpty().Must(l => l is "tr" or "en");

        // Filled means something walked the form filling every input. A human never sees it.
        RuleFor(r => r.Website).Empty();
    }
}
