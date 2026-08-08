using System;
using System.IO;
using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;
using CodeBrix.Texinfo2Pdf.Rendering;

namespace CodeBrix.Texinfo2Pdf;

/// <summary>
/// Turns GNU Texinfo source - a standard <c>.texi</c> document, or the <c>.tely</c> dialect
/// LilyPond and CodeBrix.LilyPort write - into a finished PDF, in one call or in two.
/// </summary>
/// <remarks>
/// <para>
/// There are two ways to use this. Point <see cref="RenderFile(string, string)"/> at a manual and a
/// PDF comes out; that is the whole of it for a caller who wants the document rendered. Or call
/// <see cref="GenerateHtmlFromFile"/> to get the intermediate HTML and CSS, change either of them,
/// and hand the result back to <see cref="RenderHtml"/> - which is how to control what the PDF
/// looks like beyond what the page options reach.
/// </para>
/// <para>
/// This library does no parsing and no emission of its own. It owns the hand-off: it runs
/// CodeBrix.Texinfo2Html over the source, gives the markup and the document's pictures to
/// CodeBrix.PdfDocCreate.Html2Pdf, and merges what both of them had to say into one list.
/// </para>
/// <para>
/// Nothing about a document's contents throws. Both stages degrade to the nearest readable thing
/// and report it in the result's warnings, so a manual with a broken construct in it still produces
/// a PDF of everything else. Exceptions are reserved for the caller's own mistakes - a blank path,
/// a source file that is not there.
/// </para>
/// <para>
/// One renderer can be reused for many documents; set <see cref="Options"/> before calling. Like
/// the renderers underneath it, an instance is not safe to use from several threads at once.
/// </para>
/// </remarks>
public sealed class TexinfoPdfRenderer
{
    private readonly TexinfoHtmlRenderer _texinfoRenderer = new TexinfoHtmlRenderer();
    private readonly HtmlPdfRenderer _pdfRenderer = new HtmlPdfRenderer();

    /// <summary>Creates a renderer with the defaults a printed manual wants.</summary>
    public TexinfoPdfRenderer()
    {
        Options = new TexinfoPdfOptions(_texinfoRenderer.Options, _pdfRenderer.Options);
    }

    /// <summary>
    /// Settings for the next conversion, in two groups: <see cref="TexinfoPdfOptions.Texinfo"/> for
    /// how the source is read, <see cref="TexinfoPdfOptions.Html"/> for what the PDF looks like.
    /// Change them before calling.
    /// </summary>
    public TexinfoPdfOptions Options { get; }

    // --- Workflow one: source in, PDF out -------------------------------------------------------

    /// <summary>Renders a Texinfo file to a PDF.</summary>
    /// <param name="texinfoFilePath">
    /// Path of the <c>.texi</c> or <c>.tely</c> file. Its directory and that directory's parent
    /// become the first places <c>@include</c> and <c>@image</c> look, which is what lets a manual
    /// written as a tree of included files render from its top-level source.
    /// </param>
    /// <param name="outputPdfPath">
    /// Where to write the PDF. Left null, it is written beside the source file under the same name.
    /// Directories that do not exist are created.
    /// </param>
    public TexinfoPdfResult RenderFile(string texinfoFilePath, string outputPdfPath = null)
    {
        string sourcePath = RequireFile(texinfoFilePath, nameof(texinfoFilePath));
        string pdfPath = string.IsNullOrWhiteSpace(outputPdfPath)
            ? Path.ChangeExtension(sourcePath, ".pdf")
            : outputPdfPath;
        return RenderHtml(_texinfoRenderer.GenerateFromFile(sourcePath), pdfPath);
    }

    /// <summary>Renders Texinfo source held in memory to a PDF.</summary>
    /// <param name="texinfoSource">The Texinfo source text.</param>
    /// <param name="outputPdfPath">Where to write the PDF; directories are created as needed.</param>
    /// <param name="baseDirectory">
    /// Directory that <c>@include</c> and <c>@image</c> references resolve against, or null when
    /// the source needs no files of its own.
    /// </param>
    public TexinfoPdfResult RenderTexinfo(string texinfoSource, string outputPdfPath,
        string baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(texinfoSource);
        RequirePath(outputPdfPath, nameof(outputPdfPath));
        return RenderHtml(_texinfoRenderer.Generate(texinfoSource, baseDirectory), outputPdfPath);
    }

    /// <summary>
    /// Renders a Texinfo file to a PDF held in memory, for a caller who is sending the document
    /// somewhere rather than storing it.
    /// </summary>
    /// <param name="texinfoFilePath">Path of the <c>.texi</c> or <c>.tely</c> file.</param>
    public TexinfoPdfResult RenderFileToBytes(string texinfoFilePath)
        => RenderHtmlToBytes(
            _texinfoRenderer.GenerateFromFile(RequireFile(texinfoFilePath, nameof(texinfoFilePath))));

    /// <summary>Renders Texinfo source held in memory to a PDF held in memory.</summary>
    /// <param name="texinfoSource">The Texinfo source text.</param>
    /// <param name="baseDirectory">
    /// Directory that <c>@include</c> and <c>@image</c> references resolve against, or null when
    /// the source needs no files of its own.
    /// </param>
    public TexinfoPdfResult RenderTexinfoToBytes(string texinfoSource, string baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(texinfoSource);
        return RenderHtmlToBytes(_texinfoRenderer.Generate(texinfoSource, baseDirectory));
    }

    // --- Workflow two, step A: source in, markup out ---------------------------------------------

    /// <summary>
    /// Renders a Texinfo file to HTML and CSS without going on to a PDF, so the markup can be
    /// changed first. Hand what comes back - altered or not - to <see cref="RenderHtml"/>.
    /// </summary>
    /// <param name="texinfoFilePath">Path of the <c>.texi</c> or <c>.tely</c> file.</param>
    public TexinfoHtmlResult GenerateHtmlFromFile(string texinfoFilePath)
        => _texinfoRenderer.GenerateFromFile(RequireFile(texinfoFilePath, nameof(texinfoFilePath)));

    /// <summary>Renders Texinfo source held in memory to HTML and CSS.</summary>
    /// <param name="texinfoSource">The Texinfo source text.</param>
    /// <param name="baseDirectory">
    /// Directory that <c>@include</c> and <c>@image</c> references resolve against, or null when
    /// the source needs no files of its own.
    /// </param>
    public TexinfoHtmlResult GenerateHtml(string texinfoSource, string baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(texinfoSource);
        return _texinfoRenderer.Generate(texinfoSource, baseDirectory);
    }

    // --- Workflow two, step B: markup in, PDF out ------------------------------------------------

    /// <summary>
    /// Renders markup produced by <see cref="GenerateHtmlFromFile"/> or <see cref="GenerateHtml"/>
    /// to a PDF, optionally against a stylesheet of the caller's own. The document's pictures travel
    /// with it, so nothing has to be written out first.
    /// </summary>
    /// <param name="htmlResult">The markup to render.</param>
    /// <param name="outputPdfPath">Where to write the PDF; directories are created as needed.</param>
    /// <param name="replacementCss">
    /// A stylesheet to use in place of the generated one - take
    /// <see cref="TexinfoHtmlResult.Css"/>, change it or replace it outright, and pass it here.
    /// Left null, the generated stylesheet is used.
    /// </param>
    public TexinfoPdfResult RenderHtml(TexinfoHtmlResult htmlResult, string outputPdfPath,
        string replacementCss = null)
    {
        ArgumentNullException.ThrowIfNull(htmlResult);
        string pdfPath = RequirePath(outputPdfPath, nameof(outputPdfPath));
        using (ImageStagingArea images = ImageStagingArea.For(htmlResult))
        {
            return Render(htmlResult,
                () => _pdfRenderer.RenderHtml(DocumentFor(htmlResult, replacementCss), pdfPath,
                    images.BaseDirectory));
        }
    }

    /// <summary>
    /// Renders markup produced by <see cref="GenerateHtmlFromFile"/> or <see cref="GenerateHtml"/>
    /// to a PDF held in memory.
    /// </summary>
    /// <param name="htmlResult">The markup to render.</param>
    /// <param name="replacementCss">
    /// A stylesheet to use in place of the generated one, or null to use the generated one.
    /// </param>
    public TexinfoPdfResult RenderHtmlToBytes(TexinfoHtmlResult htmlResult,
        string replacementCss = null)
    {
        ArgumentNullException.ThrowIfNull(htmlResult);
        using (ImageStagingArea images = ImageStagingArea.For(htmlResult))
        {
            return Render(htmlResult,
                () => _pdfRenderer.RenderHtmlToBytes(DocumentFor(htmlResult, replacementCss),
                    images.BaseDirectory));
        }
    }

    /// <summary>
    /// Renders an HTML file to a PDF - the way back in for a caller who wrote the document out with
    /// <see cref="TexinfoHtmlResult.WriteToDirectory"/> and then edited the files by hand. A linked
    /// stylesheet and the document's pictures are picked up from the file's own directory, so an
    /// edited pair renders exactly as it stands.
    /// </summary>
    /// <param name="htmlFilePath">Path of the HTML file.</param>
    /// <param name="outputPdfPath">Where to write the PDF; directories are created as needed.</param>
    public TexinfoPdfResult RenderHtmlFile(string htmlFilePath, string outputPdfPath)
    {
        string sourcePath = RequireFile(htmlFilePath, nameof(htmlFilePath));
        string pdfPath = RequirePath(outputPdfPath, nameof(outputPdfPath));
        return Render(null, () => _pdfRenderer.RenderFile(sourcePath, pdfPath));
    }

    /// <summary>
    /// Renders an HTML document held in memory to a PDF, for a caller who assembled the markup
    /// themselves - most often from <see cref="TexinfoHtmlResult.BodyHtml"/> inside a page of their
    /// own.
    /// </summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="outputPdfPath">Where to write the PDF; directories are created as needed.</param>
    /// <param name="baseDirectory">
    /// Directory that relative stylesheet and picture references resolve against, or null when the
    /// document refers to no files.
    /// </param>
    public TexinfoPdfResult RenderHtmlDocument(string html, string outputPdfPath,
        string baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        string pdfPath = RequirePath(outputPdfPath, nameof(outputPdfPath));
        return Render(null, () => _pdfRenderer.RenderHtml(html, pdfPath, baseDirectory));
    }

    // --- The hand-off itself ----------------------------------------------------------------------

    /// <summary>
    /// Builds the document to hand over, always as one self-contained file. The generated markup
    /// links to a stylesheet beside it by default, which would mean writing that stylesheet out
    /// somewhere for the render to find; embedding it instead says exactly the same thing to
    /// Html2Pdf and leaves only the pictures needing a home.
    /// </summary>
    private static string DocumentFor(TexinfoHtmlResult htmlResult, string replacementCss)
        => htmlResult.ToHtmlDocument(replacementCss ?? htmlResult.Css);

    /// <summary>
    /// Runs one PDF render with the document's own title and author standing in for any that the
    /// caller did not set, and puts the two stages' warnings together.
    /// </summary>
    private TexinfoPdfResult Render(TexinfoHtmlResult intermediate, Func<HtmlRenderResult> render)
    {
        //The options object belongs to the caller and is reused across documents, so anything
        //filled in from THIS document has to be taken back out again afterwards - otherwise the
        //first manual's title would follow the renderer to the second one.
        string callerTitle = Options.Html.DocumentTitle;
        string callerAuthor = Options.Html.DocumentAuthor;
        try
        {
            if (intermediate != null)
            {
                if (string.IsNullOrWhiteSpace(callerTitle) && intermediate.Title.Length > 0)
                {
                    Options.Html.DocumentTitle = intermediate.Title;
                }
                if (string.IsNullOrWhiteSpace(callerAuthor) && intermediate.Author.Length > 0)
                {
                    Options.Html.DocumentAuthor = intermediate.Author;
                }
            }
            HtmlRenderResult pdf = render();
            TexinfoRenderWarnings texinfoWarnings = intermediate == null ? null : intermediate.Warnings;
            return new TexinfoPdfResult(pdf, intermediate,
                new TexinfoPdfWarnings(texinfoWarnings, pdf.Warnings));
        }
        finally
        {
            Options.Html.DocumentTitle = callerTitle;
            Options.Html.DocumentAuthor = callerAuthor;
        }
    }

    private static string RequireFile(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Value cannot be null or blank.", parameterName);
        }
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The file to render was not found.", fullPath);
        }
        return fullPath;
    }

    /// <summary>
    /// Checks an output path and makes sure the directory it names exists, because a caller who
    /// asked for "out/manual.pdf" meant the directory too.
    /// </summary>
    private static string RequirePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Value cannot be null or blank.", parameterName);
        }
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return fullPath;
    }
}
