using System;
using System.Collections.Generic;
using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;

namespace CodeBrix.Texinfo2Pdf;

/// <summary>
/// Everything both halves of the conversion had to degrade, gathered into one list and tagged with
/// which half said it. A conversion runs two libraries over the same document, and a message means
/// something different depending on which one produced it: a Texinfo-stage message is about the
/// source, a PDF-stage message is about the markup or the fonts.
/// </summary>
/// <remarks>
/// Nothing here is an error. Both stages degrade rather than throw, so a document that produces a
/// hundred messages still produces a PDF.
/// </remarks>
public sealed class TexinfoPdfWarnings
{
    /// <summary>The tag on every message that came from reading the Texinfo source.</summary>
    public const string TexinfoStageTag = "[texinfo]";

    /// <summary>The tag on every message that came from rendering the markup to a PDF.</summary>
    public const string PdfStageTag = "[pdf]";

    private static readonly IReadOnlyList<RenderWarning> NoPdfItems = new List<RenderWarning>();

    private readonly List<string> _messages = new List<string>();
    private readonly List<string> _texinfoMessages = new List<string>();
    private readonly List<string> _pdfMessages = new List<string>();
    private readonly IReadOnlyList<RenderWarning> _pdfItems;

    internal TexinfoPdfWarnings(TexinfoRenderWarnings texinfo, RenderWarnings pdf)
    {
        //Source order within each stage is kept, and the Texinfo stage comes first because it ran
        //first - so reading the list top to bottom follows the document through the conversion.
        if (texinfo != null)
        {
            foreach (string message in texinfo.Messages)
            {
                _texinfoMessages.Add(message);
                _messages.Add(TexinfoStageTag + " " + message);
            }
        }
        if (pdf != null)
        {
            foreach (string message in pdf.Messages)
            {
                _pdfMessages.Add(message);
                _messages.Add(PdfStageTag + " " + message);
            }
        }
        _pdfItems = pdf != null ? pdf.Items : NoPdfItems;
    }

    /// <summary>
    /// Every message from both stages, each prefixed with <see cref="TexinfoStageTag"/> or
    /// <see cref="PdfStageTag"/>. This is the list to print.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// The messages from reading the Texinfo source, untagged and exactly as
    /// CodeBrix.Texinfo2Html produced them. Each opens with its category - Include, Conditional,
    /// Macro, Value, RawBlockSkipped, Encoding, Syntax, UnknownCommand, Reference or Emit - which
    /// is what to filter on.
    /// </summary>
    public IReadOnlyList<string> TexinfoMessages => _texinfoMessages;

    /// <summary>
    /// The messages from rendering the markup to a PDF, untagged and exactly as
    /// CodeBrix.PdfDocCreate.Html2Pdf produced them. Font-coverage messages open with
    /// <c>[font]</c>, and on a manual quoting symbols no text font carries they are the expected
    /// ones.
    /// </summary>
    public IReadOnlyList<string> PdfMessages => _pdfMessages;

    /// <summary>
    /// The PDF stage's warnings in structured form: each item carries a category, a
    /// stable machine-readable code (e.g. <c>font.uncovered.removed</c>,
    /// <c>font.svg-text.notdef</c>), an occurrence count, and - for glyph-coverage
    /// warnings - the code point involved. Warnings sharing one display message but
    /// concerning different code points are separate items, so a test can assert an
    /// exact drop baseline (distinct code points AND occurrences) without matching
    /// display prose. The Texinfo stage has no structured form; filter its
    /// <see cref="TexinfoMessages"/> by their leading category instead.
    /// </summary>
    public IReadOnlyList<RenderWarning> PdfItems => _pdfItems;

    /// <summary>How many messages there are in total, across both stages.</summary>
    public int Count => _messages.Count;

    /// <summary>All the messages, one per line, ready to print or to put in a test failure.</summary>
    public override string ToString() => string.Join(Environment.NewLine, _messages);
}
