using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>The draft was saved from a stale copy — another tab (or the same one, after a
/// reload) has saved since. Answered as 409, not 400, so the editor can tell "reload" apart
/// from "fix the field".</summary>
public sealed class BlogPostRevisionConflictException()
    : DomainException("BLOG_POST_REVISION_CONFLICT", "The draft was saved from an outdated revision.");

/// <summary>The slug or the language of a post that has been published at least once. Both are
/// the URL, and a URL that has been live is a promise to whoever linked it.</summary>
public sealed class BlogPostFieldLockedException()
    : DomainException("BLOG_POST_FIELD_LOCKED", "The slug and language are fixed once a post has been published.");

/// <summary>Publish asked of a draft with no title or no body.</summary>
public sealed class BlogPostIncompleteException()
    : DomainException("BLOG_POST_INCOMPLETE", "A post needs a title and a body before it can be published.");

/// <summary>Create asked with nothing written — no title, no summary, no text in the body. A
/// post row exists only once there is at least one character to keep (2026-09-19).</summary>
public sealed class BlogPostEmptyException()
    : DomainException("BLOG_POST_EMPTY", "A post is created once something has been written in it.");

/// <summary>Another post in the same language already lives at that slug.</summary>
public sealed class BlogSlugTakenException()
    : DomainException("BLOG_SLUG_TAKEN", "Another post in this language already uses that slug.");

/// <summary>A hand-edited slug that is not lowercase-ascii-and-dashes, is too long, or is one of
/// the segments the web app mounts under <c>/blog</c>.</summary>
public sealed class BlogSlugInvalidException()
    : DomainException("BLOG_SLUG_INVALID", "The slug must be lowercase letters, digits and dashes.");

/// <summary>Unpublish asked of a post that is not on the site.</summary>
public sealed class BlogPostNotPublishedException()
    : DomainException("BLOG_POST_NOT_PUBLISHED", "The post is not published.");

/// <summary>A translation link pointing at a post that cannot be this one's translation: the
/// post itself, one in the same language, or one that does not exist.</summary>
public sealed class BlogTranslationInvalidException()
    : DomainException("BLOG_TRANSLATION_INVALID", "A translation must be a different post in the other language.");

/// <summary>Draft content past the length caps — a request the validator should already have
/// refused; repeated here because this is the boundary that stores the row.</summary>
public sealed class BlogPostContentInvalidException()
    : DomainException("BLOG_POST_CONTENT_INVALID", "The draft's title, excerpt or body is over the length cap.");

/// <summary>A cover image that is not one of this post's own uploads.</summary>
public sealed class BlogCoverInvalidException()
    : DomainException("BLOG_COVER_INVALID", "The cover must be an image uploaded to this post.");

/// <summary>An edit on a comment that is no longer pending: approved text is what others replied
/// to, rejected text has nothing left to fix. Answered as 409, not 400 — the form must tell
/// "you can't any more" apart from "fix the field".</summary>
public sealed class BlogCommentLockedException()
    : DomainException("BLOG_COMMENT_LOCKED", "A comment can only be edited while it is waiting for approval.");

/// <summary>The same reader sending the same text to the same post again — a double click or a
/// paste-and-resend, never a second comment.</summary>
public sealed class BlogCommentDuplicateException()
    : DomainException("BLOG_COMMENT_DUPLICATE", "You have already posted this comment.");

/// <summary>A report with reason "Other" and nothing written.</summary>
public sealed class BlogCommentReportNoteRequiredException()
    : DomainException("BLOG_COMMENT_REPORT_NOTE_REQUIRED", "A report with the reason \"Other\" needs a short explanation.");
