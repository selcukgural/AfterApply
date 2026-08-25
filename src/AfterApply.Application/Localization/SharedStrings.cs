namespace AfterApply.Application.Localization;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;SharedStrings&gt;</c> — resolves to the co-located
/// <c>SharedStrings.resx</c> (neutral/English) and <c>SharedStrings.tr.resx</c> (Turkish). Every
/// backend-originated, user-facing error/validation string lives here, regardless of which layer
/// throws it, so translation stays in one place.
/// </summary>
public sealed class SharedStrings;
