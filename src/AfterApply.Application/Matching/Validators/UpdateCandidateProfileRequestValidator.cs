using AfterApply.Application.Matching.Contracts;
using FluentValidation;

namespace AfterApply.Application.Matching.Validators;

public sealed class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator()
    {
        RuleFor(x => x.CvText).NotEmpty().MaximumLength(20_000);
    }
}
