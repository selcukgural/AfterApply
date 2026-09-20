using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>
/// One blog post, in one language, with two slots of content on the same row.
///
/// The <b>draft slot</b> (<see cref="DraftTitle"/>, <see cref="DraftExcerpt"/>,
/// <see cref="DraftContentJson"/>, <see cref="DraftContentHtml"/>) is what the editor autosaves
/// every few seconds. The <b>published slot</b> (<see cref="Title"/>, <see cref="Excerpt"/>,
/// <see cref="ContentHtml"/>, <see cref="PublishedContentJson"/>) is what the public page renders,
/// and it only ever changes by <see cref="Publish"/> copying the draft over it. That split is the
/// whole point of the model: an autosave can never put a half-typed sentence on the site, and
/// "unpublish" is a status flip that keeps the snapshot and the slug for the day the post comes
/// back at the same URL.
///
/// Visibility (DECISIONS.md 2026-09-19): a post that is not <see cref="BlogPostStatus.Published"/>
/// belongs to its author alone — other admins get the same 404 a stranger gets. A published post
/// is any admin's to edit, unpublish or delete.
/// </summary>
public sealed class BlogPost : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxExcerptLength = 500;
    public const int MaxContentHtmlLength = 1_000_000;
    public const int MaxContentJsonLength = 2_000_000;

    /// <summary>What an editor document with nothing in it looks like — the value both JSON
    /// slots start with, so the column (jsonb) always holds a document.</summary>
    public const string EmptyDocumentJson = """{"type":"doc","content":[]}""";

    /// <summary>One of <see cref="BlogLanguage"/>. Fixed once the post has been published.</summary>
    public string Language { get; private set; } = BlogLanguage.Tr;

    public BlogPostStatus Status { get; private set; }

    /// <summary>Who created the post — the only admin who may see it while it is unpublished.
    /// Nullable because the account may be deleted while the post lives on: posts are site
    /// content, not a user's data, so the FK sets this to null rather than cascading.</summary>
    public Guid? AuthorUserId { get; private set; }

    /// <summary>The URL segment. Null until the first publish (or until the author types one);
    /// unique per language; never rewritten after the first publish.</summary>
    public string? Slug { get; private set; }

    /// <summary>The same post in the other language, when there is one — what the page's
    /// <c>hreflang</c> and its "read in English" link point at.</summary>
    public Guid? TranslationOfPostId { get; private set; }

    public Guid? CoverMediaId { get; private set; }

    /// <summary>How many times the published post was fetched by a reader (2026-09-20). A plain
    /// tally: every public fetch counts, bots and reloads included, and nothing about who fetched
    /// it is kept. Bumped in place by the public service (<c>ExecuteUpdate</c>), never through the
    /// aggregate, so a read does not touch <see cref="UpdatedAt"/> or the revision.</summary>
    public int ViewCount { get; private set; }

    // ---- draft slot ----

    public string DraftTitle { get; private set; } = string.Empty;

    public string DraftExcerpt { get; private set; } = string.Empty;

    /// <summary>The editor's own document (ProseMirror JSON), kept so the post can be reopened
    /// exactly as it was left. Never rendered.</summary>
    public string DraftContentJson { get; private set; } = EmptyDocumentJson;

    /// <summary>The draft as HTML, already through the server-side sanitizer. Stored so a
    /// preview can render it without a second sanitising pass.</summary>
    public string DraftContentHtml { get; private set; } = string.Empty;

    public DateTimeOffset DraftUpdatedAt { get; private set; }

    /// <summary>Bumped on every draft save; the editor sends the one it last saw and a mismatch
    /// is a <see cref="BlogPostRevisionConflictException"/> — two tabs cannot silently overwrite
    /// each other.</summary>
    public int Revision { get; private set; }

    // ---- published slot ----

    public string Title { get; private set; } = string.Empty;

    public string Excerpt { get; private set; } = string.Empty;

    /// <summary>What the public page renders — sanitized HTML, copied from the draft on publish.</summary>
    public string ContentHtml { get; private set; } = string.Empty;

    public string PublishedContentJson { get; private set; } = EmptyDocumentJson;

    /// <summary>First time the post went live. Set once; the URL lock (<see cref="SetSlug"/>,
    /// <see cref="SetLanguage"/>) keys off this, not off <see cref="Status"/>, so an unpublished
    /// post keeps its URL too.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Last time the published slot changed — the page's "updated" date.</summary>
    public DateTimeOffset? PublishedUpdatedAt { get; private set; }

    private BlogPost()
    {
    }

    public static BlogPost Create(Guid authorUserId, string language, DateTimeOffset now)
    {
        if (!BlogLanguage.IsSupported(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Not a supported blog language.");
        }

        return new BlogPost
        {
            AuthorUserId = authorUserId,
            Language = language,
            Status = BlogPostStatus.Draft,
            Revision = 1,
            DraftUpdatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public bool IsPublished => Status == BlogPostStatus.Published;

    public bool HasEverBeenPublished => PublishedAt is not null;

    public bool IsAuthor(Guid? userId) => userId is not null && AuthorUserId == userId;

    /// <summary>
    /// Whether an admin may open, edit or delete this post. Published: any admin. Otherwise: the
    /// author only. The public side never asks this — it filters on <see cref="Status"/>.
    /// </summary>
    public bool IsEditableBy(Guid adminUserId) => IsPublished || IsAuthor(adminUserId);

    /// <summary>The autosave. Refuses a stale revision before touching anything.</summary>
    public void SaveDraft(BlogDraftContent content, int expectedRevision, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
        {
            throw new BlogPostRevisionConflictException();
        }

        content.Validate();

        DraftTitle = content.Title;
        DraftExcerpt = content.Excerpt;
        DraftContentJson = content.ContentJson;
        DraftContentHtml = content.ContentHtml;
        DraftUpdatedAt = now;
        Revision++;
        Touch(now);
    }

    /// <summary>A hand-chosen slug. Only before the first publish; validated the way a generated
    /// one would be. Null clears it so the next publish generates one from the title.</summary>
    public void SetSlug(string? slug, DateTimeOffset now)
    {
        if (string.Equals(slug, Slug, StringComparison.Ordinal))
        {
            return;
        }

        if (HasEverBeenPublished)
        {
            throw new BlogPostFieldLockedException();
        }

        if (slug is not null && !BlogSlugGenerator.IsValid(slug))
        {
            throw new BlogSlugInvalidException();
        }

        Slug = slug;
        Touch(now);
    }

    /// <summary>Assigns the slug the allocator picked at publish time. Same lock as
    /// <see cref="SetSlug"/>, without re-validating what the generator built.</summary>
    public void AssignGeneratedSlug(string slug, DateTimeOffset now)
    {
        if (HasEverBeenPublished)
        {
            throw new BlogPostFieldLockedException();
        }

        Slug = slug;
        Touch(now);
    }

    public void SetLanguage(string language, DateTimeOffset now)
    {
        if (string.Equals(language, Language, StringComparison.Ordinal))
        {
            return;
        }

        if (HasEverBeenPublished)
        {
            throw new BlogPostFieldLockedException();
        }

        if (!BlogLanguage.IsSupported(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Not a supported blog language.");
        }

        Language = language;
        // A translation link is a link to the *other* language; changing this side's language
        // makes whatever it pointed at the same language, so the link is dropped rather than
        // left contradicting itself.
        TranslationOfPostId = null;
        Touch(now);
    }

    /// <summary>Links (or unlinks, with null) the same post in the other language. The caller has
    /// checked the target exists and is in the other language — this only refuses self-links.</summary>
    public void SetTranslationOf(Guid? postId, DateTimeOffset now)
    {
        if (postId == TranslationOfPostId)
        {
            return;
        }

        if (postId == Id)
        {
            throw new BlogTranslationInvalidException();
        }

        TranslationOfPostId = postId;
        Touch(now);
    }

    public void SetCover(Guid? mediaId, DateTimeOffset now)
    {
        if (mediaId == CoverMediaId)
        {
            return;
        }

        CoverMediaId = mediaId;
        Touch(now);
    }

    /// <summary>
    /// Copies the draft slot over the published slot and puts the post on the site. Works the same
    /// for a first publish and for "update the live version" — the difference is only that
    /// <see cref="PublishedAt"/> is set once. The caller has made sure <see cref="Slug"/> is set.
    /// </summary>
    public void Publish(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(DraftTitle) || string.IsNullOrWhiteSpace(DraftContentHtml))
        {
            throw new BlogPostIncompleteException();
        }

        if (Slug is null)
        {
            throw new InvalidOperationException("A post needs a slug before it can be published.");
        }

        Title = DraftTitle;
        Excerpt = DraftExcerpt;
        ContentHtml = DraftContentHtml;
        PublishedContentJson = DraftContentJson;
        PublishedAt ??= now;
        PublishedUpdatedAt = now;
        Status = BlogPostStatus.Published;
        Touch(now);
    }

    /// <summary>Takes the post off the site. The published slot and the slug stay, so a later
    /// <see cref="Publish"/> puts it back at the same URL.</summary>
    public void Unpublish(DateTimeOffset now)
    {
        if (!IsPublished)
        {
            throw new BlogPostNotPublishedException();
        }

        Status = BlogPostStatus.Draft;
        Touch(now);
    }
}

/// <summary>
/// Everything an autosave carries, in one value so the length rules live in one place. The
/// request validator says the same things earlier and in the user's language; this is the
/// boundary that stores the row, so it checks again.
/// </summary>
public readonly record struct BlogDraftContent(string Title, string Excerpt, string ContentJson, string ContentHtml)
{
    public void Validate()
    {
        if (Title.Length > BlogPost.MaxTitleLength
            || Excerpt.Length > BlogPost.MaxExcerptLength
            || ContentJson.Length > BlogPost.MaxContentJsonLength
            || ContentHtml.Length > BlogPost.MaxContentHtmlLength)
        {
            throw new BlogPostContentInvalidException();
        }
    }
}
