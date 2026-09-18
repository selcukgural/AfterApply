namespace AfterApply.Application.Notifications.Contracts;

/// <summary>
/// Which reminders a bulk answer covers. The same two shapes as the applications list's
/// <c>BulkSelection</c>, for the same reason: <paramref name="Ids"/> is the handful of rows the
/// user ticked on the page in front of them, <paramref name="All"/> is "every active reminder I
/// have", resolved on the server — including the pages the user never opened — which is why the
/// request carries an <c>ExpectedCount</c> for that form.
/// </summary>
public sealed record ReminderSelection(IReadOnlyList<Guid>? Ids = null, bool All = false);

/// <param name="ExpectedCount">How many reminders the user was told they were answering. Required
/// for an <c>All</c> selection and refused when the count no longer holds — a reminder the nightly
/// scan created between the card being drawn and the button being pressed must not be answered by
/// a click that never saw it. Ignored for an explicit id list.</param>
public sealed record BulkReminderRequest(ReminderSelection Selection, int? ExpectedCount = null);

/// <summary>The paged list's page. Five rows is the card's height on the dashboard; the ceiling is
/// there so a client cannot ask for the whole list back in one response.</summary>
public sealed record GetRemindersQuery(int Page = 1, int PageSize = 5);

/// <summary>How long a break lasts. Three fixed lengths rather than a date picker: the choice is
/// "a bit" / "a while" / "a month", not a calendar decision, and a person taking a break should not
/// have to plan its end to the day (<see cref="PauseRemindersRequestValidator"/> pins the set).</summary>
public sealed record PauseRemindersRequest(int Days);
