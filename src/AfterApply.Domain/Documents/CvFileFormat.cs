namespace AfterApply.Domain.Documents;

/// <summary>
/// The file formats a CV may be stored in. Deliberately a closed enum rather than a free-text
/// content type: the extension and the Content-Type a download is served with are both derived
/// from this value (see <see cref="CvFileFormats"/>), never echoed back from what the client
/// claimed at upload time — a browser that is handed an attacker-chosen Content-Type is the whole
/// problem this closes.
/// </summary>
public enum CvFileFormat
{
    Pdf = 1,
    Doc = 2,
    Docx = 3
}

public static class CvFileFormats
{
    public static string Extension(this CvFileFormat format) => format switch
    {
        CvFileFormat.Pdf => ".pdf",
        CvFileFormat.Doc => ".doc",
        CvFileFormat.Docx => ".docx",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    public static string ContentType(this CvFileFormat format) => format switch
    {
        CvFileFormat.Pdf => "application/pdf",
        CvFileFormat.Doc => "application/msword",
        CvFileFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}
