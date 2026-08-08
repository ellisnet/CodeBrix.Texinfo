using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;

namespace CodeBrix.Texinfo2Pdf;

/// <summary>
/// The finished PDF and an account of how it was arrived at: where it was written or the bytes of
/// it, how long it turned out to be, and everything either stage of the conversion had to degrade.
/// </summary>
public sealed class TexinfoPdfResult
{
    internal TexinfoPdfResult(HtmlRenderResult pdf, TexinfoHtmlResult intermediate,
        TexinfoPdfWarnings warnings)
    {
        OutputFilePath = pdf.OutputFilePath ?? string.Empty;
        PdfBytes = pdf.PdfBytes;
        PageCount = pdf.PageCount;
        Title = pdf.Title ?? string.Empty;
        Intermediate = intermediate;
        Warnings = warnings;
    }

    /// <summary>
    /// Where the PDF was written, or an empty string when it was rendered to bytes instead.
    /// </summary>
    public string OutputFilePath { get; }

    /// <summary>
    /// The PDF itself, for a render that asked for bytes; null when it was written to a file.
    /// </summary>
    public byte[] PdfBytes { get; }

    /// <summary>How many pages the finished document has.</summary>
    public int PageCount { get; }

    /// <summary>The title the PDF carries in its metadata.</summary>
    public string Title { get; }

    /// <summary>
    /// The HTML and CSS the PDF was made from, for a caller who wants to keep or inspect the
    /// intermediate of a one-shot conversion. It is null when the PDF was rendered from markup the
    /// caller supplied, since then there was no Texinfo stage to produce one.
    /// </summary>
    public TexinfoHtmlResult Intermediate { get; }

    /// <summary>
    /// Everything both stages had to degrade, tagged with which stage said it. Never null, and
    /// empty for a document that came through untouched.
    /// </summary>
    public TexinfoPdfWarnings Warnings { get; }
}
