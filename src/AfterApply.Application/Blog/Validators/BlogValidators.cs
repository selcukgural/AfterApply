using System.Text.Json;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Blog.Validators;

/// <summary>The form's own rules, shared by create and the autosave. Field by field so the
/// ProblemDetails names the field the editor has to highlight. The domain's
/// <see cref="BlogDraftContent.Validate"/> repeats the length caps at the boundary that stores
/// the row; this layer is the one that speaks the user's language.</summary>
public sealed class BlogDraftFieldsValidator<T> : AbstractValidator<T> where T : IBlogDraftFields
{
    public BlogDraftFieldsValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Title).NotNull().MaximumLength(BlogPost.MaxTitleLength);
        RuleFor(x => x.Excerpt).MaximumLength(BlogPost.MaxExcerptLength);
        RuleFor(x => x.ContentHtml).NotNull().MaximumLength(BlogPost.MaxContentHtmlLength);
        RuleFor(x => x.ContentJson)
            .NotNull()
            .MaximumLength(BlogPost.MaxContentJsonLength)
            // The editor's document, opaque to the server except for this: it has to be the one
            // shape the editor can reopen, or the next visit to the post would open a blank page.
            .Must(BeAnEditorDocument)
            .WithMessage(_ => localizer["VALIDATION_BLOG_CONTENT_JSON_INVALID"]);
        RuleFor(x => x.Language)
            .Must(BlogLanguage.IsSupported)
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        // Blank is "none" (the editor sends the field back empty before the first publish and
        // the locked value after it); anything else has to be a slug the generator could make.
        RuleFor(x => x.Slug)
            .Must(slug => string.IsNullOrWhiteSpace(slug) || BlogSlugGenerator.IsValid(slug.Trim()))
            .WithMessage(_ => localizer["BLOG_SLUG_INVALID"]);
        // The store's caps, named field by field so the editor can point at the one that is over.
        RuleFor(x => x.Seo!.SeoTitle).MaximumLength(BlogSeo.MaxSeoTitleLength)
            .OverridePropertyName("Seo.SeoTitle").When(x => x.Seo is not null);
        RuleFor(x => x.Seo!.PrimaryKeyword).MaximumLength(BlogSeo.MaxKeywordLength)
            .OverridePropertyName("Seo.PrimaryKeyword").When(x => x.Seo is not null);
        RuleFor(x => x.Seo!.CoverAlt).MaximumLength(BlogSeo.MaxCoverAltLength)
            .OverridePropertyName("Seo.CoverAlt").When(x => x.Seo is not null);
        RuleFor(x => x.Seo!.SecondaryKeywords)
            .Must(keywords => keywords is null
                              || (keywords.Count <= BlogSeo.MaxSecondaryKeywords
                                  && keywords.All(k => k is not null && k.Length <= BlogSeo.MaxKeywordLength)))
            .OverridePropertyName("Seo.SecondaryKeywords")
            .WithMessage(_ => localizer["VALIDATION_BLOG_SEO_KEYWORDS_INVALID"])
            .When(x => x.Seo is not null);
        RuleFor(x => x.Guide!.RelatedPostIds)
            .Must(ids => ids is null
                         || (ids.Count <= BlogGuideOptions.MaxRelatedPosts && ids.Distinct().Count() == ids.Count && !ids.Contains(Guid.Empty)))
            .OverridePropertyName("Guide.RelatedPostIds")
            .WithMessage(_ => localizer["BLOG_RELATED_INVALID"])
            .When(x => x.Guide is not null);
    }

    private static bool BeAnEditorDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("type", out var type)
                   && type.ValueKind == JsonValueKind.String
                   && type.GetString() == "doc";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>The form's rules plus the one that makes create different from a save: something
/// has to have been written. The message hangs off <see cref="CreateBlogPostRequest.Title"/> so
/// the editor has a field to point at, though any of the three would have satisfied it.</summary>
public sealed class CreateBlogPostRequestValidator : AbstractValidator<CreateBlogPostRequest>
{
    public CreateBlogPostRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        Include(new BlogDraftFieldsValidator<CreateBlogPostRequest>(localizer));
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Title)
            .Must((request, _) => BlogDraftText.HasAny(request.Title, request.Excerpt, request.ContentHtml))
            .WithMessage(_ => localizer["BLOG_POST_EMPTY"]);
    }
}

public sealed class SaveBlogDraftRequestValidator : AbstractValidator<SaveBlogDraftRequest>
{
    public SaveBlogDraftRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        Include(new BlogDraftFieldsValidator<SaveBlogDraftRequest>(localizer));
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(1);
    }
}

public sealed class PublicBlogListQueryValidator : AbstractValidator<PublicBlogListQuery>
{
    public PublicBlogListQueryValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Lang)
            .Must(BlogLanguage.IsSupported)
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue)
            .OverridePropertyName(nameof(PublicBlogListQuery.Kind));
    }
}

public sealed class PublicBlogSlugsQueryValidator : AbstractValidator<PublicBlogSlugsQuery>
{
    public PublicBlogSlugsQueryValidator()
    {
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue)
            .OverridePropertyName(nameof(PublicBlogSlugsQuery.Kind));
    }
}

public sealed class AdminBlogListQueryValidator : AbstractValidator<AdminBlogListQuery>
{
    public AdminBlogListQueryValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue)
            .OverridePropertyName(nameof(AdminBlogListQuery.Status));
        RuleFor(x => x.Lang)
            .Must(lang => lang is null || BlogLanguage.IsSupported(lang))
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue)
            .OverridePropertyName(nameof(AdminBlogListQuery.Kind));
    }
}

public sealed class AdminBlogGroupedListQueryValidator : AbstractValidator<AdminBlogGroupedListQuery>
{
    public AdminBlogGroupedListQueryValidator()
    {
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue)
            .OverridePropertyName(nameof(AdminBlogGroupedListQuery.Status));
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue)
            .OverridePropertyName(nameof(AdminBlogGroupedListQuery.Kind));
    }
}
