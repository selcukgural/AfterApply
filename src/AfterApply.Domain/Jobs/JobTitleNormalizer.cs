using AfterApply.Domain.Common;

namespace AfterApply.Domain.Jobs;

public static class JobTitleNormalizer
{
    public static string Normalize(string title)
    {
        return string.Join(' ', TurkishTextNormalizer.FoldCase(title.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
